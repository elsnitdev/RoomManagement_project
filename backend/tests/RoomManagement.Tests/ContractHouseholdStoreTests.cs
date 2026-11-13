using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Errors;
using RoomManagement.Business.DTOs;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class ContractHouseholdStoreTests
{
    [SqlServerFact]
    public async Task Legacy_member_remains_visible_without_being_reported_as_signed()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        const string owner = "legacy-owner";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        await using var context = database.CreateContext();
        context.Users.Add(new AppUser { Id = owner, UserName = owner,
            NormalizedUserName = owner.ToUpperInvariant(), PasswordHash = "test-hash",
            Role = UserRoles.Landlord, IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Properties.Add(new Property { Id = propertyId, OwnerUserId = owner,
            Name = "Khu cũ", Address = "Địa chỉ", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId,
            RoomNumber = "L1", MaxOccupants = 1, BasePrice = 1_000_000,
            Status = RoomStatus.Available, CreatedAt = DateTime.UtcNow });
        context.Contracts.Add(new Contract { Id = contractId, RoomId = roomId,
            ContractCode = $"LEGACY-{Guid.NewGuid():N}", StartDate = today.AddDays(-1),
            MonthlyRent = 1_000_000, Status = ContractStatus.Active,
            CreatedAt = DateTime.UtcNow });
        context.TenantProfiles.Add(new TenantProfile { Id = tenantId, CreatedByUserId = owner,
            FullName = "Người thuê cũ", CreatedAt = DateTime.UtcNow });
        context.ContractTenants.Add(new ContractTenant { Id = linkId, ContractId = contractId,
            TenantId = tenantId, JoinDate = today.AddDays(-1), IsContactPerson = true,
            CreatedAt = DateTime.UtcNow });
        context.MemberCommitments.Add(new MemberCommitment { Id = Guid.NewGuid(),
            ContractTenantId = linkId, CommitmentType = CommitmentType.RoomRulesAgreement,
            EvidenceStatus = CommitmentEvidenceStatus.LegacyPendingEvidence,
            CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var room = await new RoomStore(context).GetResultAsync(propertyId, roomId, owner, default);
        Assert.Equal(RoomOccupancyState.Occupied, room!.OccupancyState);
        var detail = await new ContractStore(context).GetDetailOwnedAsync(propertyId, contractId, owner, default);
        var tenant = Assert.Single(detail!.Tenants);
        Assert.False(tenant.HasConfirmedCommitment);
        Assert.Equal(CommitmentEvidenceStatus.LegacyPendingEvidence, tenant.EvidenceStatus);

        var contract = await context.Contracts.SingleAsync(x => x.Id == contractId);
        var monthEnd = new DateOnly(today.Year, today.Month,
            DateTime.DaysInMonth(today.Year, today.Month));
        var nextMonth = monthEnd.AddDays(1);
        var link = await context.ContractTenants.SingleAsync(x => x.Id == linkId);
        link.LeaveDate = nextMonth;
        await context.SaveChangesAsync();
        var store = new ContractStore(context);
        var memberError = await Assert.ThrowsAsync<BusinessException>(() =>
            store.SetLegacyEndDateAsync(contract, monthEnd, today, default));
        Assert.Equal("CONTRACT_MEMBER_AFTER_END_DATE", memberError.Code);
        link.LeaveDate = null;
        await context.SaveChangesAsync();

        var invoice = new Invoice { Id = Guid.NewGuid(), ContractId = contractId,
            RoomId = roomId, InvoiceCode = $"INV-{Guid.NewGuid():N}",
            InvoiceType = InvoiceType.Monthly, BillingYear = (short)nextMonth.Year,
            BillingMonth = (byte)nextMonth.Month, IssueDate = today,
            DueDate = nextMonth, TotalAmount = 100_000,
            Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        var invoiceError = await Assert.ThrowsAsync<BusinessException>(() =>
            store.SetLegacyEndDateAsync(contract, monthEnd, today, default));
        Assert.Equal("CONTRACT_INVOICE_AFTER_END_DATE", invoiceError.Code);
        context.Invoices.Remove(invoice);
        await context.SaveChangesAsync();
        await store.SetLegacyEndDateAsync(contract, monthEnd, today, default);
        Assert.Equal(monthEnd, contract.EndDate);
    }

    [SqlServerFact]
    public async Task New_member_of_active_contract_is_not_an_occupant_until_commitment_is_confirmed()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        const string owner = "commitment-owner";
        const string tenantUser = "commitment-tenant-user";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var monthEnd = new DateOnly(today.Year, today.Month,
            DateTime.DaysInMonth(today.Year, today.Month));
        var contract = new Contract { Id = Guid.NewGuid(), RoomId = roomId,
            ContractCode = $"COM-{Guid.NewGuid():N}", StartDate = today, EndDate = monthEnd,
            MonthlyRent = 2_000_000, Status = ContractStatus.Draft, CreatedAt = DateTime.UtcNow };
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await using var context = database.CreateContext();
        context.Users.Add(new AppUser { Id = owner, UserName = owner,
            NormalizedUserName = owner.ToUpperInvariant(), PasswordHash = "test-hash",
            Role = UserRoles.Landlord, IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Users.Add(new AppUser { Id = tenantUser, UserName = tenantUser,
            NormalizedUserName = tenantUser.ToUpperInvariant(), PasswordHash = "test-hash",
            Role = UserRoles.Tenant, IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Properties.Add(new Property { Id = propertyId, OwnerUserId = owner,
            Name = "Khu cam kết", Address = "Địa chỉ", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId, RoomNumber = "K1",
            MaxOccupants = 2, BasePrice = 2_000_000, Status = RoomStatus.Available,
            CreatedAt = DateTime.UtcNow });
        context.Contracts.Add(contract);
        foreach (var (id, name) in new[] { (first, "Đại diện"), (second, "Thành viên") })
            context.TenantProfiles.Add(new TenantProfile { Id = id, CreatedByUserId = owner,
                UserId = id == second ? tenantUser : null,
                FullName = name, DateOfBirth = new DateOnly(1990, 1, 1),
                PhoneNumber = "0901234567", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var store = new ContractStore(context);
        var firstLink = await store.AddProfileAsync(contract, first, owner, today, true, default);
        await store.ConfirmCommitmentAsync(contract, firstLink.Id,
            new(CommitmentType.RoomRulesAgreement, today, "Đại diện", null, null, "Nội quy."), owner, default);
        await store.ActivateSafelyAsync(contract, default);
        var secondLink = await store.AddProfileAsync(contract, second, owner, today, false, default);
        var rooms = new RoomStore(context);
        Assert.Single((await rooms.GetResultAsync(propertyId, roomId, owner, default))!.Occupants!);

        await store.ConfirmCommitmentAsync(contract, secondLink.Id,
            new(CommitmentType.RoomRulesAgreement, today, "Thành viên", null, null, "Nội quy."), owner, default);
        Assert.Equal(2, (await rooms.GetResultAsync(propertyId, roomId, owner, default))!.Occupants!.Count);

        var sentAfter = DateTime.UtcNow.AddSeconds(-1);
        var notice = await store.CreateMoveOutNoticeAsync(contract,
            new(second, "Thông báo rời", "Tôi dự định rời phòng."), owner, default);
        Assert.Equal(TenantRequestCategory.MoveOutNotice, notice.Category);
        Assert.True(notice.RequestedAt >= sentAfter);
        Assert.Null((await store.GetTenantAsync(contract.Id, secondLink.Id, default))!.LeaveDate);
        await store.ResolveMoveOutNoticeAsync(contract, notice.Id, "Chủ trọ sẽ xem xét hoàn phí.", owner, default);
        Assert.Null((await store.GetTenantAsync(contract.Id, secondLink.Id, default))!.LeaveDate);
        await store.CreateMoveOutNoticeAsync(contract,
            new(first, "Thông báo khác", "Tôi cũng cần báo rời."), owner, default);
        var mine = await store.GetDetailMineAsync(contract.Id, tenantUser, default);
        Assert.Single(mine!.Tenants);
        Assert.Equal(second, mine.Tenants[0].TenantId);
        Assert.Equal(2, mine.Members.Count);
        Assert.Contains(mine.Members, member => member.Id == firstLink.Id && member.FullName == "Đại diện");
        Assert.Contains(mine.Members, member => member.Id == secondLink.Id && member.FullName == "Thành viên");
        var response = RoomManagement.API.Contracts.Contracts.ContractDetailResponse.From(mine);
        Assert.Equal(2, response.Members.Count);
        Assert.Equal(2, response.MemberCount);
        Assert.Single(response.Tenants);
        Assert.NotNull(mine.Tenants[0].CurrentCommitmentId);
        Assert.Single(mine.Tenants[0].TenureSpans);
        Assert.Single(mine.MoveOutNotices!);
        Assert.Equal(second, mine.MoveOutNotices![0].RequestedByTenantId);
    }

    [SqlServerFact]
    public async Task Three_profiles_without_accounts_can_share_one_contract_but_fourth_is_rejected()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        const string owner = "household-owner";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var memberIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthEnd = new DateOnly(today.Year, today.Month,
            DateTime.DaysInMonth(today.Year, today.Month));
        await using (var seed = database.CreateContext())
        {
            seed.Users.Add(new AppUser { Id = owner, UserName = owner,
                NormalizedUserName = owner.ToUpperInvariant(), PasswordHash = "test-hash",
                Role = UserRoles.Landlord, IsActive = true, CreatedAt = DateTime.UtcNow });
            seed.Properties.Add(new Property { Id = propertyId, OwnerUserId = owner,
                Name = "Khu gia đình", Address = "Địa chỉ test", IsActive = true,
                CreatedAt = DateTime.UtcNow });
            seed.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId, RoomNumber = "F1",
                MaxOccupants = 3, BasePrice = 3_000_000, Status = RoomStatus.Available,
                CreatedAt = DateTime.UtcNow });
            seed.Contracts.Add(new Contract { Id = contractId, RoomId = roomId,
                ContractCode = "FAMILY-001", StartDate = today, EndDate = monthEnd,
                Status = ContractStatus.Draft,
                MonthlyRent = 3_000_000, CreatedAt = DateTime.UtcNow });
            for (var i = 0; i < memberIds.Length; i++)
                seed.TenantProfiles.Add(new TenantProfile { Id = memberIds[i],
                    CreatedByUserId = owner, FullName = $"Thành viên {i + 1}",
                    PhoneNumber = i == 0 ? "0901234567" : null,
                    DateOfBirth = new DateOnly(1990, 1, 1),
                    CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var store = new ContractStore(context);
        var contract = await context.Contracts.SingleAsync(c => c.Id == contractId);
        var before = await new RoomStore(context).GetResultAsync(propertyId, roomId, owner, default);
        Assert.Equal(RoomStatus.Available, before!.Status);

        var links = new List<ContractTenant>();
        for (var i = 0; i < 3; i++)
            links.Add(await store.AddProfileAsync(contract, memberIds[i], owner, today, i == 0, default));
        var commitmentError = await Assert.ThrowsAsync<BusinessException>(() =>
            store.ActivateSafelyAsync(contract, default));
        Assert.Equal("COMMITMENT_REQUIRED", commitmentError.Code);
        for (var i = 0; i < links.Count; i++)
        {
            var link = links[i];
            await store.ConfirmCommitmentAsync(contract, link.Id,
                new(RoomManagement.Domain.Enums.CommitmentType.RoomRulesAgreement,
                    today, $"Thành viên {i + 1}", null, null, "Nội quy phòng trọ đã được đồng ý."),
                owner, default);
        }
        await store.ActivateSafelyAsync(contract, default);

        var detail = await store.GetDetailOwnedAsync(propertyId, contractId, owner, default);
        Assert.Equal(3, detail!.Tenants.Count);
        Assert.Single(detail.Tenants, t => t.IsContactPerson);
        Assert.All(detail.Tenants, t => Assert.Null(t.UserId));
        var room = await new RoomStore(context).GetResultAsync(propertyId, roomId, owner, default);
        Assert.Equal(RoomStatus.Occupied, room!.Status);
        Assert.Equal(3, room.Occupants!.Count);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            store.AddProfileAsync(contract, memberIds[3], owner, today, false, default));
        Assert.Equal("TENANT_OCCUPANCY_CONFLICT", error.Code);
    }

    [SqlServerFact]
    public async Task Contact_must_have_phone_or_email_before_assignment()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        const string owner = "contact-owner";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        await using var context = database.CreateContext();
        context.Users.Add(new AppUser { Id = owner, UserName = owner,
            NormalizedUserName = owner.ToUpperInvariant(), PasswordHash = "test-hash",
            Role = UserRoles.Landlord, IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Properties.Add(new Property { Id = propertyId, OwnerUserId = owner,
            Name = "Khu test", Address = "Địa chỉ", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId, RoomNumber = "C1",
            MaxOccupants = 2, BasePrice = 1_000_000, Status = RoomStatus.Available,
            CreatedAt = DateTime.UtcNow });
        var contract = new Contract { Id = contractId, RoomId = roomId, ContractCode = "CONTACT-001",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = ContractStatus.Draft,
            MonthlyRent = 1_000_000, CreatedAt = DateTime.UtcNow };
        context.Contracts.Add(contract);
        context.TenantProfiles.Add(new TenantProfile { Id = profileId, CreatedByUserId = owner,
            FullName = "Thành viên", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var store = new ContractStore(context);
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            store.AddProfileAsync(contract, profileId, owner, contract.StartDate, true, default));
        Assert.Equal("CONTACT_DETAILS_REQUIRED", error.Code);
        Assert.Empty(await context.ContractTenants.ToListAsync());
        await store.AddProfileAsync(contract, profileId, owner, contract.StartDate, false, default);
        var activationError = await Assert.ThrowsAsync<BusinessException>(() =>
            store.ActivateSafelyAsync(contract, default));
        Assert.Equal("CONTRACT_TENANT_CONFLICT", activationError.Code);
        Assert.Equal(RoomStatus.Available,
            (await new RoomStore(context).GetResultAsync(propertyId, roomId, owner, default))!.Status);
    }
}
