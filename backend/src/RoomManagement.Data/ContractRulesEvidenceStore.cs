using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class ContractRulesEvidenceStore(RoomManagementDbContext db) : IContractRulesEvidenceStore
{
    public Task<bool> TenantCanAccessAsync(Guid contractId, Guid linkId, string userId, CancellationToken ct) =>
        (from link in db.ContractTenants.AsNoTracking()
         join profile in db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
         where link.Id == linkId && link.ContractId == contractId && profile.UserId == userId
         select link.Id).AnyAsync(ct);

    public Task<bool> OwnerCanAccessAsync(Guid propertyId, Guid contractId, Guid linkId, string userId, CancellationToken ct) =>
        (from link in db.ContractTenants.AsNoTracking()
         join contract in db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
         join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
         join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
         where link.Id == linkId && contract.Id == contractId && property.Id == propertyId && property.OwnerUserId == userId
         select link.Id).AnyAsync(ct);

    public async Task<Guid?> CurrentAcceptanceAsync(Guid contractId, Guid linkId, string userId, CancellationToken ct)
    {
        var propertyId = await (from contract in db.Contracts.AsNoTracking()
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            where contract.Id == contractId select room.PropertyId).SingleAsync(ct);
        var versionId = await db.ContractRulesVersions.AsNoTracking().Where(x => x.PropertyId == propertyId && x.IsActive)
            .OrderByDescending(x => x.VersionNumber).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        return await (from acceptance in db.ContractRulesAcceptances.AsNoTracking()
            join link in db.ContractTenants.AsNoTracking() on acceptance.ContractTenantId equals link.Id
            join profile in db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
            join contract in db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
            where link.Id == linkId && link.ContractId == contractId && link.LeaveDate == null &&
                (contract.Status == ContractStatus.Draft || contract.Status == ContractStatus.Active) &&
                profile.UserId == userId && acceptance.AcceptedByUserId == userId && acceptance.RulesVersionId == versionId
            select (Guid?)acceptance.Id).SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<RulesEvidenceItem>> ListAsync(Guid contractId, Guid linkId, CancellationToken ct) =>
        await (from evidence in db.ContractRulesEvidence.AsNoTracking()
            join acceptance in db.ContractRulesAcceptances.AsNoTracking() on evidence.AcceptanceId equals acceptance.Id
            join version in db.ContractRulesVersions.AsNoTracking() on acceptance.RulesVersionId equals version.Id
            join link in db.ContractTenants.AsNoTracking() on acceptance.ContractTenantId equals link.Id
            join contract in db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            where link.ContractId == contractId && acceptance.ContractTenantId == linkId &&
                (version.ContractId == contractId || version.PropertyId == room.PropertyId)
            orderby evidence.CreatedAt, evidence.Id
            select new RulesEvidenceItem(evidence, version.VersionNumber, version.ContractId != null)).ToListAsync(ct);

    public async Task AddAsync(ContractRulesEvidence evidence, CancellationToken ct)
    {
        db.ContractRulesEvidence.Add(evidence);
        await db.SaveChangesAsync(ct);
    }
}
