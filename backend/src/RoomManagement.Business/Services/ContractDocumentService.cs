using System.Security.Cryptography;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class ContractDocumentService(
    ICurrentLandlord current, ILandlordStore landlords, IContractDocumentStore documents, IObjectStorage storage)
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private static BusinessException NotFound() => new(BusinessErrorType.NotFound,
        "CONTRACT_DOCUMENT_NOT_FOUND", "Không tìm thấy tệp hợp đồng.");

    private async Task<(Contract Contract, string ActorUserId)> OwnerAsync(Guid propertyId, Guid contractId, CancellationToken ct)
    {
        var context = await OperatorAccessGuard.RequireOperatorAsync(current, landlords, ct);
        if (!await landlords.HasPropertyAccessAsync(context.ActorUserId, propertyId, ct)) throw NotFound();
        var contract = await documents.GetOwnedAsync(propertyId, contractId, context.OwnerLandlordUserId, ct)
            ?? throw NotFound();
        return (contract, context.ActorUserId);
    }

    public async Task<IReadOnlyList<ContractDocument>> OwnerListAsync(Guid propertyId, Guid contractId, CancellationToken ct)
    {
        await OwnerAsync(propertyId, contractId, ct);
        return await documents.ListAsync(contractId, ct);
    }

    public async Task<IReadOnlyList<ContractDocument>> TenantListAsync(Guid contractId, string userId, CancellationToken ct)
    {
        var offerVersion = string.IsNullOrWhiteSpace(userId) ? null
            : await documents.TenantVisibleOfferVersionAsync(contractId, userId, ct);
        if (offerVersion is null or < 1)
            throw NotFound();
        return (await documents.ListAsync(contractId, ct)).Where(x => x.OfferVersion == offerVersion).ToList();
    }

    public async Task<ContractDocument> UploadAsync(Guid propertyId, Guid contractId,
        string fileName, long length, Stream stream, CancellationToken ct)
    {
        var (contract, actorUserId) = await OwnerAsync(propertyId, contractId, ct);
        if (contract.Workflow != ContractWorkflow.Invitation || contract.Status != ContractStatus.Draft)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_DOCUMENT_LOCKED",
                "Chỉ có thể sửa tệp của hợp đồng nháp theo luồng lời mời.");
        if (length <= 0 || length > MaxFileBytes)
            throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_DOCUMENT_SIZE_INVALID",
                "Tệp hợp đồng phải có dung lượng từ 1 byte đến 10 MiB.");

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > length || buffer.Length + read > MaxFileBytes)
                throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_DOCUMENT_SIZE_INVALID",
                    "Dung lượng tệp hợp đồng không hợp lệ.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (buffer.Length != length || buffer.Length > MaxFileBytes)
            throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_DOCUMENT_SIZE_INVALID",
                "Dung lượng tệp hợp đồng không hợp lệ.");
        var bytes = buffer.ToArray();
        var mime = bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff ? "image/jpeg"
            : bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? "image/webp"
            : bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8) ? "application/pdf" : null;
        if (mime is null)
            throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_DOCUMENT_FORMAT_INVALID",
                "Chỉ hỗ trợ ảnh JPEG, PNG, WebP hoặc PDF.");

        var existing = (await documents.ListAsync(contractId, ct)).Where(x => x.OfferVersion == null).ToList();
        if (existing.Count > 0 && (mime == "application/pdf" || existing.Any(x => x.FileType == "application/pdf")))
            throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_DOCUMENT_FORMAT_MIXED",
                "Một hợp đồng có thể dùng nhiều ảnh hoặc một PDF.");

        var id = Guid.NewGuid();
        var suffix = mime switch { "image/jpeg" => "jpg", "image/png" => "png", "image/webp" => "webp", _ => "pdf" };
        var storageKey = $"contracts/{contractId:N}/{id:N}.{suffix}";
        var cleanName = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(cleanName)) cleanName = $"hop-dong.{suffix}";
        if (cleanName.Length > 255) cleanName = cleanName[..255];
        var document = new ContractDocument
        {
            Id = id, ContractId = contractId, DocumentType = ContractDocumentType.ContractPage,
            FileName = cleanName, StorageKey = storageKey, FileType = mime,
            SizeBytes = length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            PageOrder = existing.Count, UploadedByUserId = actorUserId, UploadedAt = DateTime.UtcNow
        };
        buffer.Position = 0;
        await storage.UploadAsync(storageKey, buffer, mime, ct);
        try { await documents.AddAsync(document, ct); }
        catch { try { await storage.DeleteAsync(storageKey, CancellationToken.None); } catch { } throw; }
        return document;
    }

    public async Task<(ContractDocument Document, Stream Stream)> OpenOwnerAsync(
        Guid propertyId, Guid contractId, Guid documentId, CancellationToken ct)
    {
        await OwnerAsync(propertyId, contractId, ct);
        var document = await documents.GetAsync(contractId, documentId, ct) ?? throw NotFound();
        return (document, await storage.OpenReadAsync(document.StorageKey, ct));
    }

    public async Task<(ContractDocument Document, Stream Stream)> OpenTenantAsync(
        Guid contractId, Guid documentId, string userId, CancellationToken ct)
    {
        var authorized = await TenantListAsync(contractId, userId, ct);
        var document = authorized.SingleOrDefault(x => x.Id == documentId) ?? throw NotFound();
        return (document, await storage.OpenReadAsync(document.StorageKey, ct));
    }

    public async Task DeleteAsync(Guid propertyId, Guid contractId, Guid documentId, CancellationToken ct)
    {
        var (contract, _) = await OwnerAsync(propertyId, contractId, ct);
        if (contract.Workflow != ContractWorkflow.Invitation || contract.Status != ContractStatus.Draft)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_DOCUMENT_LOCKED",
                "Chỉ có thể sửa tệp của hợp đồng nháp theo luồng lời mời.");
        var document = await documents.GetAsync(contractId, documentId, ct) ?? throw NotFound();
        if (document.OfferVersion != null) throw NotFound();
        await documents.DeleteDraftAsync(document, ct);
    }
}
