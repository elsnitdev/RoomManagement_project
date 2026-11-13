using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Errors;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class ContractTenancyModeTests
{
    [SqlServerFact]
    public async Task Family_contract_requires_only_signer_acceptance_and_preserves_occupant_approval()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var db = database.CreateContext();
        const string owner = "family-mode-landlord";
        const string signerUser = "family-mode-signer";
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var signerId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var otherAdultId = Guid.NewGuid();
        var rulesId = Guid.NewGuid();
        var contract = new Contract
        {
            Id = Guid.NewGuid(), RoomId = roomId, ContractCode = $"FAM-{Guid.NewGuid():N}",
            StartDate = today, EndDate = monthEnd, MonthlyRent = 2_000_000,
            TenancyMode = ContractTenancyMode.PrimaryTenantWithFamily,
            Status = ContractStatus.Draft, CreatedAt = DateTime.UtcNow
        };
        db.Users.AddRange(
            new AppUser { Id = owner, UserName = owner, NormalizedUserName = owner.ToUpperInvariant(),
                PasswordHash = "test-hash", Role = UserRoles.Landlord, IsActive = true, CreatedAt = DateTime.UtcNow },
            new AppUser { Id = signerUser, UserName = signerUser, NormalizedUserName = signerUser.ToUpperInvariant(),
                PasswordHash = "test-hash", Role = UserRoles.Tenant, IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Properties.Add(new Property { Id = propertyId, OwnerUserId = owner,
            Name = "Khu gia đình", Address = "Địa chỉ", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId, RoomNumber = "F1",
            MaxOccupants = 3, BasePrice = 2_000_000, Status = RoomStatus.Available,
            CreatedAt = DateTime.UtcNow });
        db.Contracts.Add(contract);
        db.TenantProfiles.AddRange(
            new TenantProfile { Id = signerId, CreatedByUserId = owner, UserId = signerUser,
                FullName = "Người đứng tên", PhoneNumber = "0901234567", CreatedAt = DateTime.UtcNow },
            new TenantProfile { Id = childId, CreatedByUserId = owner,
                FullName = "Con nhỏ", DateOfBirth = today.AddYears(-5), CreatedAt = DateTime.UtcNow },
            new TenantProfile { Id = otherAdultId, CreatedByUserId = owner,
                FullName = "Người lớn khác", CreatedAt = DateTime.UtcNow });
        db.ContractRulesVersions.Add(new ContractRulesVersion { Id = rulesId, PropertyId = propertyId,
            IsActive = true, VersionNumber = 1, Content = "Nội quy", CreatedByUserId = owner,
            CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var store = new ContractStore(db);
        var signer = await store.AddProfileAsync(contract, signerId, owner, today, true,
            ContractTenantRole.ContractParty, default);
        var child = await store.AddProfileAsync(contract, childId, owner, today, false,
            ContractTenantRole.AuthorizedOccupant, default);
        Assert.False(child.OnlineRulesRequired);
        Assert.Equal(owner, child.ResidenceApprovedByUserId);
        Assert.NotNull(child.ResidenceApprovedAt);

        var secondSignerError = await Assert.ThrowsAsync<BusinessException>(() =>
            store.AddProfileAsync(contract, otherAdultId, owner, today, false,
                ContractTenantRole.ContractParty, default));
        Assert.Equal("CONTRACT_TENANT_ROLE_INVALID", secondSignerError.Code);

        var beforeAcceptance = await Assert.ThrowsAsync<BusinessException>(() =>
            store.ActivateSafelyAsync(contract, default));
        Assert.Equal("COMMITMENT_REQUIRED", beforeAcceptance.Code);
        db.ContractRulesAcceptances.Add(new ContractRulesAcceptance { Id = Guid.NewGuid(),
            RulesVersionId = rulesId, ContractTenantId = signer.Id,
            AcceptedByUserId = signerUser, AcceptedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await store.ActivateSafelyAsync(contract, default);
        var detail = await store.GetDetailOwnedAsync(propertyId, contract.Id, owner, default);
        var childInfo = Assert.Single(detail!.Tenants, t => t.TenantId == childId);
        Assert.Equal(ContractTenantRole.AuthorizedOccupant, childInfo.Role);
        Assert.False(childInfo.HasAcceptedCurrentRules);
        Assert.False(childInfo.HasConfirmedCommitment);
        Assert.Equal(owner, childInfo.ResidenceApprovedByUserId);
        Assert.Equal(1, await db.ContractRulesAcceptances.CountAsync());
        Assert.Equal(2, (await new RoomStore(db).GetResultAsync(propertyId, roomId, owner, default))!.Occupants!.Count);
    }

    [SqlServerFact]
    public async Task Co_tenancy_rejects_authorized_occupants()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var db = database.CreateContext();
        const string owner = "co-mode-landlord";
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = owner, UserName = owner, NormalizedUserName = owner.ToUpperInvariant(),
            PasswordHash = "test-hash", Role = UserRoles.Landlord, IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Properties.Add(new Property { Id = propertyId, OwnerUserId = owner,
            Name = "Khu đồng thuê", Address = "Địa chỉ", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId, RoomNumber = "C1",
            MaxOccupants = 2, BasePrice = 1_000_000, Status = RoomStatus.Available,
            CreatedAt = DateTime.UtcNow });
        var contract = new Contract { Id = Guid.NewGuid(), RoomId = roomId,
            ContractCode = $"CO-{Guid.NewGuid():N}", StartDate = today,
            MonthlyRent = 1_000_000, TenancyMode = ContractTenancyMode.CoTenancy,
            Status = ContractStatus.Draft, CreatedAt = DateTime.UtcNow };
        db.Contracts.Add(contract);
        db.TenantProfiles.Add(new TenantProfile { Id = profileId, CreatedByUserId = owner,
            FullName = "Người cùng ở", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new ContractStore(db).AddProfileAsync(contract, profileId, owner, today, false,
                ContractTenantRole.AuthorizedOccupant, default));
        Assert.Equal("CONTRACT_TENANT_ROLE_INVALID", error.Code);
        Assert.Empty(await db.ContractTenants.ToListAsync());
    }
}
