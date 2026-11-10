using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class ContractRulesEvidenceService(IContractRulesEvidenceStore store, IObjectStorage storage,
    ICurrentLandlord current, ILandlordStore landlords)
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    private static BusinessException NotFound() => new(BusinessErrorType.NotFound, "RULES_EVIDENCE_NOT_FOUND", "Không tìm thấy bằng chứng cam kết.");

    private async Task TenantAccess(Guid contractId, Guid linkId, string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || !await store.TenantCanAccessAsync(contractId, linkId, userId, ct)) throw NotFound();
    }
    private async Task OwnerAccess(Guid propertyId, Guid contractId, Guid linkId, CancellationToken ct)
    {
        var userId = current.GetUserId();
        if (string.IsNullOrWhiteSpace(userId) || !await landlords.IsActiveLandlordAsync(userId, ct) ||
            !await store.OwnerCanAccessAsync(propertyId, contractId, linkId, userId, ct)) throw NotFound();
    }
    public async Task<IReadOnlyList<RulesEvidenceItem>> TenantListAsync(Guid contractId, Guid linkId, string userId, CancellationToken ct)
    {
        await TenantAccess(contractId, linkId, userId, ct);
        return (await store.ListAsync(contractId, linkId, ct)).Where(x => x.Evidence.UploadedByUserId == userId).ToList();
    }
    public async Task<IReadOnlyList<RulesEvidenceItem>> OwnerListAsync(Guid propertyId, Guid contractId, Guid linkId, CancellationToken ct)
    {
        await OwnerAccess(propertyId, contractId, linkId, ct);
        return await store.ListAsync(contractId, linkId, ct);
    }
    public async Task<ContractRulesEvidence> UploadAsync(Guid contractId, Guid linkId, string userId, string fileName, long length, Stream stream, CancellationToken ct)
    {
        await TenantAccess(contractId, linkId, userId, ct);
        var acceptanceId = await store.CurrentAcceptanceAsync(contractId, linkId, userId, ct);
        if (acceptanceId is null) throw new BusinessException(BusinessErrorType.InvalidOperation, "RULES_ACCEPTANCE_REQUIRED", "Hãy chấp thuận nội quy hiện tại trước khi tải bằng chứng.");
        if (length <= 0 || length > MaxFileBytes) throw new BusinessException(BusinessErrorType.Validation, "RULES_EVIDENCE_INVALID", "Tệp phải có dung lượng từ 1 byte đến 10 MiB.");
        using var content = new MemoryStream();
        var buffer = new byte[81920]; int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (content.Length + read > length || content.Length + read > MaxFileBytes) throw new BusinessException(BusinessErrorType.Validation, "RULES_EVIDENCE_INVALID", "Dung lượng tệp không hợp lệ.");
            await content.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (content.Length != length) throw new BusinessException(BusinessErrorType.Validation, "RULES_EVIDENCE_INVALID", "Dung lượng tệp không hợp lệ.");
        var bytes = content.ToArray();
        var mime = bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff ? "image/jpeg"
            : bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? "image/webp"
            : bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8) ? "application/pdf" : null;
        if (mime is null) throw new BusinessException(BusinessErrorType.Validation, "RULES_EVIDENCE_FORMAT_INVALID", "Chỉ hỗ trợ JPEG, PNG, WebP hoặc PDF.");
        var suffix = mime switch { "image/jpeg" => "jpg", "image/png" => "png", "image/webp" => "webp", _ => "pdf" };
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name)) name = $"cam-ket.{suffix}";
        if (name.Length > 255) name = name[..255];
        var evidence = new ContractRulesEvidence { Id = Guid.NewGuid(), AcceptanceId = acceptanceId.Value,
            BlobName = $"contract-rules/{contractId:N}/{linkId:N}/{Guid.NewGuid():N}.{suffix}", OriginalFileName = name,
            ContentType = mime, SizeBytes = length, UploadedByUserId = userId, CreatedAt = DateTime.UtcNow };
        content.Position = 0;
        await storage.UploadAsync(evidence.BlobName, content, mime, ct);
        try { await store.AddAsync(evidence, ct); }
        catch { try { await storage.DeleteAsync(evidence.BlobName, CancellationToken.None); } catch { } throw; }
        return evidence;
    }
    public async Task<(ContractRulesEvidence Evidence, Stream Stream)> OpenAsync(IReadOnlyList<RulesEvidenceItem> authorized, Guid id, CancellationToken ct)
    {
        var evidence = authorized.SingleOrDefault(x => x.Evidence.Id == id)?.Evidence ?? throw NotFound();
        return (evidence, await storage.OpenReadAsync(evidence.BlobName, ct));
    }
}
