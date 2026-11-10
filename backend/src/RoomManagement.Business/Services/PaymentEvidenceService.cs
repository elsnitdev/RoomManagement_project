using System.Security.Cryptography;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class PaymentEvidenceService(ICurrentLandlord current, ILandlordStore landlords,
    IPaymentEvidenceStore evidence, IObjectStorage storage)
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private async Task<string> AuthorizeAsync(Guid paymentId, CancellationToken ct)
    {
        var owner = current.GetUserId();
        if (paymentId == Guid.Empty || string.IsNullOrWhiteSpace(owner) ||
            !await landlords.IsActiveLandlordAsync(owner, ct) ||
            !await evidence.OwnsConfirmedPaymentAsync(paymentId, owner, ct))
            throw new BusinessException(BusinessErrorType.NotFound, "PAYMENT_NOT_FOUND", "Không tìm thấy khoản thanh toán đã xác nhận.");
        return owner;
    }

    public async Task<IReadOnlyList<PaymentEvidence>> ListAsync(Guid paymentId, CancellationToken ct)
    {
        await AuthorizeAsync(paymentId, ct);
        return await evidence.ListAsync(paymentId, ct);
    }

    public async Task<PaymentEvidence> UploadAsync(Guid paymentId, Guid imageId, string fileName,
        long length, Stream stream, CancellationToken ct)
    {
        var owner = await AuthorizeAsync(paymentId, ct);
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
        if (content.Length != length || content.Length > MaxFileBytes)
            throw new BusinessException(BusinessErrorType.Validation, "EVIDENCE_INVALID", "Dung lượng ảnh không hợp lệ.");
        var bytes = content.ToArray();
        var mime = bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff ? "image/jpeg"
            : bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? "image/webp" : null;
        if (mime is null)
            throw new BusinessException(BusinessErrorType.Validation, "EVIDENCE_FORMAT_INVALID", "Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var existing = await evidence.GetAsync(imageId, ct);
        if (existing is not null) return VerifyRetry(existing, paymentId, owner, hash);
        var suffix = mime == "image/jpeg" ? "jpg" : mime == "image/png" ? "png" : "webp";
        var blobName = $"landlords/{owner}/payments/{paymentId}/{Guid.NewGuid():N}.{suffix}";
        var cleanName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(cleanName)) cleanName = $"evidence.{suffix}";
        if (cleanName.Length > 255) cleanName = cleanName[..255];
        var image = new PaymentEvidence { Id = imageId, PaymentId = paymentId, BlobName = blobName,
            OriginalFileName = cleanName, ContentType = mime, SizeBytes = length, Sha256 = hash,
            UploadedByUserId = owner, CreatedAt = DateTime.UtcNow };
        content.Position = 0;
        await storage.UploadAsync(blobName, content, mime, ct);
        try
        {
            var result = await evidence.AddOrGetAsync(image, ct);
            if (result.BlobName != blobName)
            {
                try { await storage.DeleteAsync(blobName, CancellationToken.None); } catch { }
                return VerifyRetry(result, paymentId, owner, hash);
            }
            return result;
        }
        catch { try { await storage.DeleteAsync(blobName, CancellationToken.None); } catch { } throw; }
    }

    public async Task<(PaymentEvidence Image, Stream Content)> OpenAsync(Guid paymentId, Guid imageId, CancellationToken ct)
    {
        await AuthorizeAsync(paymentId, ct);
        var image = await evidence.GetAsync(imageId, ct);
        if (image is null || image.PaymentId != paymentId)
            throw new BusinessException(BusinessErrorType.NotFound, "EVIDENCE_NOT_FOUND", "Không tìm thấy ảnh bằng chứng.");
        return (image, await storage.OpenReadAsync(image.BlobName, ct));
    }

    private static PaymentEvidence VerifyRetry(PaymentEvidence existing, Guid paymentId, string owner, string hash) =>
        existing.PaymentId == paymentId && existing.UploadedByUserId == owner && existing.Sha256 == hash ? existing
        : throw new BusinessException(BusinessErrorType.Conflict, "EVIDENCE_ID_CONFLICT", "Mã ảnh đã được sử dụng cho ảnh khác.");
}
