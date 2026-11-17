using System.Data;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class ContractDocumentStore(RoomManagementDbContext db) : IContractDocumentStore
{
    public Task<Contract?> GetOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct) =>
        (from contract in db.Contracts.AsNoTracking()
         join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
         join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
         where contract.Id == contractId && property.Id == propertyId && property.OwnerUserId == ownerId
         select contract).SingleOrDefaultAsync(ct);

    public Task<int?> TenantVisibleOfferVersionAsync(Guid contractId, string userId, CancellationToken ct) =>
        (from link in db.ContractTenants.AsNoTracking()
         join profile in db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
         join contract in db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
         where contract.Id == contractId && contract.Workflow == ContractWorkflow.Invitation &&
               contract.Status != ContractStatus.Draft && contract.Status != ContractStatus.Cancelled &&
               link.LeaveDate == null && profile.UserId == userId
         select (int?)contract.OfferVersion).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ContractDocument>> ListAsync(Guid contractId, CancellationToken ct) =>
        await db.ContractDocuments.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.DocumentType == ContractDocumentType.ContractPage)
            .OrderBy(x => x.PageOrder).ThenBy(x => x.UploadedAt).ThenBy(x => x.Id).ToListAsync(ct);

    public Task<ContractDocument?> GetAsync(Guid contractId, Guid documentId, CancellationToken ct) =>
        db.ContractDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.ContractId == contractId &&
            x.Id == documentId && x.DocumentType == ContractDocumentType.ContractPage, ct);

    public async Task AddAsync(ContractDocument document, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var contract = await db.Contracts.FromSqlInterpolated($"SELECT * FROM dbo.Contracts WITH (UPDLOCK, HOLDLOCK) WHERE Id = {document.ContractId}")
            .SingleOrDefaultAsync(ct);
        if (contract?.Workflow != ContractWorkflow.Invitation || contract.Status != ContractStatus.Draft)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_DOCUMENT_LOCKED",
                "Chỉ có thể sửa tệp của hợp đồng nháp theo luồng lời mời.");
        var existing = await db.ContractDocuments.Where(x => x.ContractId == document.ContractId &&
            x.DocumentType == ContractDocumentType.ContractPage && x.OfferVersion == null).ToListAsync(ct);
        if (existing.Count > 0 && (document.FileType == "application/pdf" || existing.Any(x => x.FileType == "application/pdf")))
            throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_DOCUMENT_FORMAT_MIXED",
                "Một hợp đồng có thể dùng nhiều ảnh hoặc một PDF.");
        document.PageOrder = existing.Count;
        db.ContractDocuments.Add(document);
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DeleteDraftAsync(ContractDocument document, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var contract = await db.Contracts.FromSqlInterpolated($"SELECT * FROM dbo.Contracts WITH (UPDLOCK, HOLDLOCK) WHERE Id = {document.ContractId}")
            .SingleOrDefaultAsync(ct);
        if (contract?.Workflow != ContractWorkflow.Invitation || contract.Status != ContractStatus.Draft)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_DOCUMENT_LOCKED",
                "Chỉ có thể sửa tệp của hợp đồng nháp theo luồng lời mời.");
        var tracked = await db.ContractDocuments.SingleOrDefaultAsync(x => x.Id == document.Id &&
            x.ContractId == document.ContractId && x.OfferVersion == null, ct);
        if (tracked is null)
            throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_DOCUMENT_NOT_FOUND", "Không tìm thấy tệp hợp đồng.");
        db.ContractDocuments.Remove(tracked);
        contract.UpdatedAt = DateTime.UtcNow;
        db.BlobCleanupJobs.Add(new BlobCleanupJob { Id = Guid.NewGuid(), BlobName = document.StorageKey,
            CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
