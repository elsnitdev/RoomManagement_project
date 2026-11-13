using System.Security.Cryptography;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class SettlementRefundEvidenceService(ContractService contracts, IContractStore store,
    IObjectStorage storage)
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private async Task<ContractSettlement> SettlementAsync(Guid propertyId, Guid contractId, CancellationToken ct)
    {
        await contracts.GetDetailAsync(propertyId, contractId, ct);
        var settlement = await store.GetSettlementAsync(contractId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "SETTLEMENT_NOT_FOUND", "Chưa có bản ghi tất toán.");
        if (settlement.DepositDisposition != "Refunded" || settlement.RefundAmount <= 0)
            throw new BusinessException(BusinessErrorType.Conflict, "SETTLEMENT_NO_REFUND", "Bản tất toán không có khoản hoàn cọc.");
        return settlement;
    }

    public Task<ContractSettlement> GetAsync(Guid propertyId, Guid contractId, CancellationToken ct) =>
        SettlementAsync(propertyId, contractId, ct);

    public async Task<ContractSettlement> UploadAsync(Guid propertyId, Guid contractId, Guid imageId,
        string fileName, long length, Stream stream, CancellationToken ct)
    {
        var settlement = await SettlementAsync(propertyId, contractId, ct);
        if (imageId == Guid.Empty || length <= 0 || length > MaxFileBytes)
            throw new BusinessException(BusinessErrorType.Validation, "EVIDENCE_INVALID", "Ảnh phải có dung lượng từ 1 byte đến 10 MiB.");
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (content.Length + read > MaxFileBytes || content.Length + read > length)
                throw new BusinessException(BusinessErrorType.Validation, "EVIDENCE_INVALID", "Dung lượng ảnh không hợp lệ.");
            await content.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (content.Length != length)
            throw new BusinessException(BusinessErrorType.Validation, "EVIDENCE_INVALID", "Dung lượng ảnh không hợp lệ.");
        var bytes = content.ToArray();
        var mime = bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff ? "image/jpeg"
            : bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? "image/webp" : null;
        if (mime is null)
            throw new BusinessException(BusinessErrorType.Validation, "EVIDENCE_FORMAT_INVALID", "Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (settlement.RefundEvidenceId is not null)
        {
            if (settlement.RefundEvidenceId == imageId && settlement.RefundEvidenceSha256 == hash) return settlement;
            throw new BusinessException(BusinessErrorType.Conflict, "SETTLEMENT_EVIDENCE_EXISTS", "Khoản hoàn cọc đã có ảnh bằng chứng.");
        }
        var suffix = mime == "image/jpeg" ? "jpg" : mime == "image/png" ? "png" : "webp";
        var blobName = $"settlements/{contractId}/{Guid.NewGuid():N}.{suffix}";
        var cleanName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(cleanName)) cleanName = $"refund.{suffix}";
        if (cleanName.Length > 255) cleanName = cleanName[..255];
        content.Position = 0;
        await storage.UploadAsync(blobName, content, mime, ct);
        try
        {
            var saved = await store.SaveRefundEvidenceAsync(contractId, imageId, blobName, cleanName, mime,
                length, hash, DateTime.UtcNow, ct);
            if (saved.RefundEvidenceBlobName != blobName)
            {
                try { await storage.DeleteAsync(blobName, CancellationToken.None); } catch { }
            }
            return saved;
        }
        catch { try { await storage.DeleteAsync(blobName, CancellationToken.None); } catch { } throw; }
    }

    public async Task<(ContractSettlement Settlement, Stream Content)> OpenAsync(Guid propertyId,
        Guid contractId, Guid imageId, CancellationToken ct)
    {
        var settlement = await SettlementAsync(propertyId, contractId, ct);
        if (settlement.RefundEvidenceId != imageId || string.IsNullOrEmpty(settlement.RefundEvidenceBlobName))
            throw new BusinessException(BusinessErrorType.NotFound, "EVIDENCE_NOT_FOUND", "Không tìm thấy ảnh hoàn cọc.");
        return (settlement, await storage.OpenReadAsync(settlement.RefundEvidenceBlobName, ct));
    }
}
