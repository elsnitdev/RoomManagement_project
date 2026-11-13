using System.Data;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class ContractRulesStore(RoomManagementDbContext db) : IContractRulesStore
{
    private IQueryable<Property> Owned(Guid propertyId, string ownerId) =>
        db.Properties.Where(x => x.Id == propertyId && x.OwnerUserId == ownerId);

    private static BusinessException NotFound() => new(BusinessErrorType.NotFound, "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");

    public async Task<ContractRulesVersion?> GetOwnedAsync(Guid propertyId, string ownerId, CancellationToken ct)
    {
        if (!await Owned(propertyId, ownerId).AnyAsync(ct)) throw NotFound();
        return await db.ContractRulesVersions.AsNoTracking().Where(x => x.PropertyId == propertyId && x.IsActive)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
    }

    public async Task<ContractRulesVersion> SaveAsync(Guid propertyId, string ownerId,
        string content, Guid? expectedVersionId, CancellationToken ct)
    {
        if (!await Owned(propertyId, ownerId).AnyAsync(ct)) throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Properties.FromSqlInterpolated($"SELECT * FROM dbo.Properties WITH (UPDLOCK, HOLDLOCK) WHERE Id = {propertyId}").AsNoTracking().SingleAsync(ct);
        var property = await Owned(propertyId, ownerId).AsNoTracking().SingleOrDefaultAsync(ct) ?? throw NotFound();
        if (!property.IsActive) throw new BusinessException(BusinessErrorType.InvalidOperation, "RULES_PROPERTY_INACTIVE", "Không thể sửa nội quy của khu trọ ngừng hoạt động.");
        var current = await db.ContractRulesVersions.Where(x => x.PropertyId == propertyId && x.IsActive).SingleOrDefaultAsync(ct);
        if (current?.Id != expectedVersionId)
            throw new BusinessException(BusinessErrorType.Conflict, "RULES_VERSION_CONFLICT", "Nội quy đã thay đổi. Vui lòng tải lại trước khi lưu.");
        if (current?.Content == content) { await tx.CommitAsync(ct); return current; }
        var lastVersion = await db.ContractRulesVersions.Where(x => x.PropertyId == propertyId).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0;
        if (current is not null) { current.IsActive = false; await db.SaveChangesAsync(ct); }
        var version = new ContractRulesVersion { Id = Guid.NewGuid(), PropertyId = propertyId,
            VersionNumber = lastVersion + 1, IsActive = true, Content = content, CreatedByUserId = ownerId, CreatedAt = DateTime.UtcNow };
        db.ContractRulesVersions.Add(version);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return version;
    }

    public async Task<IReadOnlyList<ContractRulesTenantView>> ListMineAsync(string userId, CancellationToken ct)
    {
        var links = await (from link in db.ContractTenants.AsNoTracking()
            join profile in db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
            join contract in db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            where profile.UserId == userId && link.Role == RoomManagement.Domain.Enums.ContractTenantRole.ContractParty &&
                link.LeaveDate == null &&
                (contract.Status == ContractStatus.Draft || contract.Status == ContractStatus.Active)
            select new { contract.Id, contract.ContractCode, room.PropertyId, LinkId = link.Id, profile.FullName }).ToListAsync(ct);
        var results = new List<ContractRulesTenantView>();
        foreach (var link in links)
        {
            var rules = await db.ContractRulesVersions.AsNoTracking().Where(x => x.PropertyId == link.PropertyId && x.IsActive)
                .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
            var acceptance = rules is null ? null : await db.ContractRulesAcceptances.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RulesVersionId == rules.Id && x.ContractTenantId == link.LinkId && x.AcceptedByUserId == userId, ct);
            results.Add(new ContractRulesTenantView(link.Id, link.ContractCode, link.LinkId, link.FullName, rules, acceptance != null, acceptance?.AcceptedAt));
        }
        return results;
    }

    public async Task<ContractRulesAcceptance> AcceptAsync(Guid contractId, Guid linkId, Guid versionId, string userId, CancellationToken ct)
    {
        var ownedLink = from link in db.ContractTenants.AsNoTracking()
            join profile in db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
            join contract in db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            where link.Id == linkId && contract.Id == contractId && profile.UserId == userId &&
                link.Role == RoomManagement.Domain.Enums.ContractTenantRole.ContractParty && link.LeaveDate == null
            select new { contract.RoomId, room.PropertyId, contract.Status };
        var found = await ownedLink.SingleOrDefaultAsync(ct) ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Properties.FromSqlInterpolated($"SELECT * FROM dbo.Properties WITH (UPDLOCK, HOLDLOCK) WHERE Id = {found.PropertyId}").AsNoTracking().SingleAsync(ct);
        await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {found.RoomId}").AsNoTracking().SingleAsync(ct);
        var locked = await ownedLink.SingleOrDefaultAsync(ct) ?? throw NotFound();
        if (locked.Status is not (ContractStatus.Draft or ContractStatus.Active)) throw NotFound();
        var current = await db.ContractRulesVersions.AsNoTracking().Where(x => x.PropertyId == locked.PropertyId && x.IsActive)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        if (current is null || current.Id != versionId)
            throw new BusinessException(BusinessErrorType.Conflict, "RULES_VERSION_CONFLICT", "Nội quy đã thay đổi hoặc chưa được thiết lập. Vui lòng tải lại.");
        var existing = await db.ContractRulesAcceptances.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RulesVersionId == versionId && x.ContractTenantId == linkId, ct);
        if (existing is not null)
        {
            if (existing.AcceptedByUserId != userId) throw NotFound();
            await tx.CommitAsync(ct); return existing;
        }
        var result = new ContractRulesAcceptance { Id = Guid.NewGuid(), RulesVersionId = versionId,
            ContractTenantId = linkId, AcceptedByUserId = userId, AcceptedAt = DateTime.UtcNow };
        db.ContractRulesAcceptances.Add(result); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return result;
    }
}
