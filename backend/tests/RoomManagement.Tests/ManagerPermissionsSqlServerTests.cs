using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.DTOs.Managers;
using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Data;
using RoomManagement.Data.Stores;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class ManagerPermissionsSqlServerTests
{
    private sealed record ActorContext(string UserId) : ICurrentLandlord
    {
        public string GetUserId() => UserId;
    }

    [SqlServerFact]
    public async Task Migration_backfills_OwnerLandlordUserId_from_creator_and_contract()
    {
        await using var database = await SqlServerDatabase.CreateAsync(migrate: false);
        await using var db = database.CreateContext();
        await db.GetService<IMigrator>().MigrateAsync("20261113160100_AddSettlementOneOffIdentity");

        var landlordId = "landlord-backfill";
        var propId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var profileByCreatorId = Guid.NewGuid();
        var profileByContractId = Guid.NewGuid();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.Users (Id, UserName, NormalizedUserName, PasswordHash, Role)
            VALUES ({landlordId}, N'landlord_bf', N'LANDLORD_BF', N'hash', N'Landlord')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.Properties (Id, OwnerUserId, Name, Address)
            VALUES ({propId}, {landlordId}, N'Khu A', N'Addr A')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.Rooms (Id, PropertyId, RoomNumber, BasePrice, MaxOccupants)
            VALUES ({roomId}, {propId}, N'A101', 2000000, 2)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.TenantProfiles (Id, CreatedByUserId, FullName)
            VALUES ({profileByCreatorId}, {landlordId}, N'Nguyễn Văn A')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.TenantProfiles (Id, CreatedByUserId, FullName)
            VALUES ({profileByContractId}, NULL, N'Trần Thị B')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.Contracts (Id, ContractCode, RoomId, StartDate, MonthlyRent, DepositAmount, PaymentDueDay, Status)
            VALUES ({contractId}, N'HD-BF-01', {roomId}, '2026-01-01', 2000000, 2000000, 5, N'Active')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.ContractTenants (Id, ContractId, TenantId, JoinDate, IsContactPerson)
            VALUES ({Guid.NewGuid()}, {contractId}, {profileByContractId}, '2026-01-01', 1)
            """);

        await db.GetService<IMigrator>().MigrateAsync();
        db.ChangeTracker.Clear();

        var p1 = await db.TenantProfiles.SingleAsync(x => x.Id == profileByCreatorId);
        var p2 = await db.TenantProfiles.SingleAsync(x => x.Id == profileByContractId);
        Assert.Equal(landlordId, p1.OwnerLandlordUserId);
        Assert.Equal(landlordId, p2.OwnerLandlordUserId);
    }

    [SqlServerFact]
    public async Task Manager_permission_matrix_scoping_revocation_lock_and_soft_delete()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var db = database.CreateContext();

        var hasher = new PasswordHasher<AppUser>();
        var landlord = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "landlord1",
            NormalizedUserName = "LANDLORD1",
            Role = UserRoles.Landlord,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved,
            CreatedAt = DateTime.UtcNow
        };
        landlord.PasswordHash = hasher.HashPassword(landlord, "Pass123!");
        var otherLandlord = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "landlord2",
            NormalizedUserName = "LANDLORD2",
            Role = UserRoles.Landlord,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved,
            CreatedAt = DateTime.UtcNow
        };
        otherLandlord.PasswordHash = hasher.HashPassword(otherLandlord, "Pass123!");
        db.Users.AddRange(landlord, otherLandlord);
        await db.SaveChangesAsync();

        var landlordStore = new LandlordStore(db);
        var propertyStore = new PropertyStore(db);
        var managerStore = new ManagerStore(db);
        var roomStore = new RoomStore(db);
        var contractStore = new ContractStore(db);
        var invoiceStore = new InvoiceStore(db);
        var billingStore = new BillingInputStore(db);
        var paymentStore = new PaymentStore(db);
        var tenantProfileStore = new TenantProfileStore(db);

        // Landlord creates Property A, Property B, and Property C
        var landlordPropertySvc = new PropertyService(new ActorContext(landlord.Id), landlordStore, propertyStore);
        var propA = await landlordPropertySvc.CreateAsync(new CreatePropertyInput("Khu A", "Addr A", null), default);
        var propB = await landlordPropertySvc.CreateAsync(new CreatePropertyInput("Khu B", "Addr B", null), default);
        var propC = await landlordPropertySvc.CreateAsync(new CreatePropertyInput("Khu C", "Addr C", null), default);

        // Other landlord creates Property X
        var otherPropSvc = new PropertyService(new ActorContext(otherLandlord.Id), landlordStore, propertyStore);
        var propX = await otherPropSvc.CreateAsync(new CreatePropertyInput("Khu X", "Addr X", null), default);

        // Landlord creates rooms in A and B
        var landlordRoomSvc = new RoomManagement.Business.Services.RoomService(new ActorContext(landlord.Id), landlordStore, propertyStore, roomStore);
        var roomA1 = (await landlordRoomSvc.CreateAsync(propA.Id, new CreateRoomInput("A101", null, 1, 20m, 2_000_000m, 2, null), default)).Room;
        var roomB1 = (await landlordRoomSvc.CreateAsync(propB.Id, new CreateRoomInput("B101", null, 1, 20m, 2_000_000m, 2, null), default)).Room;

        // Landlord creates Manager assigned to Property A and Property C (NOT B or X)
        var landlordManagerSvc = new ManagerService(new ActorContext(landlord.Id), landlordStore, managerStore, hasher);
        await Assert.ThrowsAsync<BusinessException>(() => landlordManagerSvc.CreateAsync(
            new CreateManagerInput("mgr_bad", "Temp123!", null, "Mgr", null, [propX.Id]), default));

        var managerDto = await landlordManagerSvc.CreateAsync(
            new CreateManagerInput("mgr1", "Temp123!", null, "Manager One", "0901234567", [propA.Id, propC.Id]), default);
        Assert.True(managerDto.MustChangePassword);
        Assert.Equal(2, managerDto.AssignedProperties.Count);

        // Manager cannot use business services while MustChangePassword == true
        var mgrPropSvc = new PropertyService(new ActorContext(managerDto.Id), landlordStore, propertyStore);
        await Assert.ThrowsAsync<BusinessException>(() => mgrPropSvc.ListAsync(new PropertyFilter(1, 20, null, null), default));

        // Manager changes temporary password
        var authSvc = new AuthenticationService(new UserStore(db), hasher);
        var changed = await authSvc.ChangePasswordAsync(managerDto.Id, "Temp123!", "NewPass123!", "NewPass123!", default);
        Assert.False(changed.MustChangePassword);

        // Manager lists properties -> sees ONLY A and C (TotalItems == 2, B is hidden)
        var mgrProps = await mgrPropSvc.ListAsync(new PropertyFilter(1, 20, null, null), default);
        Assert.Equal(2, mgrProps.TotalItems);
        Assert.DoesNotContain(mgrProps.Items, p => p.Id == propB.Id);

        // Manager cannot access Property B or Room B1 directly
        await Assert.ThrowsAsync<BusinessException>(() => mgrPropSvc.GetAsync(propB.Id, default));
        var mgrRoomSvc = new RoomManagement.Business.Services.RoomService(new ActorContext(managerDto.Id), landlordStore, propertyStore, roomStore);
        await Assert.ThrowsAsync<BusinessException>(() => mgrRoomSvc.GetAsync(propB.Id, roomB1.Id, default));

        // Manager cannot create/delete Property or update bank account or manage Managers
        await Assert.ThrowsAsync<BusinessException>(() => mgrPropSvc.CreateAsync(new CreatePropertyInput("Hack", "Hack", null), default));
        await Assert.ThrowsAsync<BusinessException>(() => mgrPropSvc.DeleteAsync(propA.Id, default));
        await Assert.ThrowsAsync<BusinessException>(() => mgrPropSvc.UpdatePaymentAccountAsync(propA.Id, "VCB", "123", "A", Convert.ToBase64String(propA.RowVersion), default));
        var mgrManagerSvc = new ManagerService(new ActorContext(managerDto.Id), landlordStore, managerStore, hasher);
        await Assert.ThrowsAsync<BusinessException>(() => mgrManagerSvc.ListAsync(new ManagerFilter(1, 20, null, null), default));

        // Manager creates TenantProfile (OwnerLandlordUserId == landlord.Id, CreatedByUserId == manager.Id)
        var mgrTenantSvc = new TenantProfileService(tenantProfileStore, NullLogger<TenantProfileService>.Instance, landlordStore);
        var tenantProfile = await mgrTenantSvc.CreateProfileAsync(managerDto.Id, new CreateTenantProfileDto
        {
            FullName = "Nguyễn Văn Thuê",
            PhoneNumber = "0987654321"
        }, default);
        Assert.Equal(landlord.Id, tenantProfile.OwnerLandlordUserId);
        Assert.Equal(managerDto.Id, tenantProfile.CreatedByUserId);

        // Landlord sees the TenantProfile created by Manager
        var landlordTenantSvc = new TenantProfileService(tenantProfileStore, NullLogger<TenantProfileService>.Instance, landlordStore);
        var fetchedByLandlord = await landlordTenantSvc.GetProfileByIdAsync(landlord.Id, tenantProfile.Id, default);
        Assert.Equal(tenantProfile.Id, fetchedByLandlord.Id);

        // Manager creates Contract in Property A and cannot access Contract in Property B
        var mgrContractSvc = new ContractService(new ActorContext(managerDto.Id), landlordStore, contractStore);
        var landlordContractSvc = new ContractService(new ActorContext(landlord.Id), landlordStore, contractStore);

        var contractInB = await landlordContractSvc.CreateAsync(propB.Id, new CreateContractInput(
            roomB1.Id, "HD-B-01", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            2_000_000m, 2_000_000m, 5, null, InitialDepositPaymentStatus.Unpaid, null, null, ContractTenancyMode.PrimaryTenantWithFamily), default);
        await Assert.ThrowsAsync<BusinessException>(() => mgrContractSvc.GetDetailAsync(propB.Id, contractInB.Contract.Id, default));

        var contractInA = await mgrContractSvc.CreateAsync(propA.Id, new CreateContractInput(
            roomA1.Id, "HD-A-01", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            2_000_000m, 2_000_000m, 5, null, InitialDepositPaymentStatus.Paid, PaymentMethod.Cash, null, ContractTenancyMode.PrimaryTenantWithFamily), default);
        Assert.NotNull(contractInA.DepositInvoiceId);

        // Verify payment created by Manager has ActorUserId as SubmittedByUserId/VerifiedByUserId
        var depPayments = await paymentStore.ListOwnedAsync(contractInA.DepositInvoiceId!.Value, landlord.Id, default);
        Assert.Single(depPayments);
        Assert.Equal(managerDto.Id, depPayments[0].Payment.SubmittedByUserId);
        Assert.Equal(managerDto.Id, depPayments[0].Payment.VerifiedByUserId);

        // Manager records overpayment & overpayment refund on Property A invoice
        var mgrPaymentSvc = new LandlordPaymentService(new ActorContext(managerDto.Id), landlordStore, paymentStore);
        var mgrInvoiceSvc = new InvoiceService(new ActorContext(managerDto.Id), landlordStore, invoiceStore, billingStore, new MonthlyInvoiceCalculator());
        var landlordInvoiceSvc = new InvoiceService(new ActorContext(landlord.Id), landlordStore, invoiceStore, billingStore, new MonthlyInvoiceCalculator());

        // Overpay deposit invoice by 500k using RecordReceived? Deposit is already paid, so create a one-off invoice in A and overpay via direct payment + refund
        var oneOffA = await mgrInvoiceSvc.CreateOneOffAsync(propA.Id, new OneOffInvoiceInput(
            contractInA.Contract.Id, InvoiceType.Other, new DateOnly(2026, 2, 10), null,
            [new EditableInvoiceItem(null, InvoiceItemType.Other, "Phí phát sinh", 1, 100_000m)]), default);
        await mgrInvoiceSvc.IssueAsync(oneOffA.Id, default);
        await mgrPaymentSvc.RecordReceivedAsync(oneOffA.Id, new RecordReceivedPaymentInput(
            150_000m, PaymentMethod.Cash, null, null, null, Guid.NewGuid()), default);
        // Wait: RecordReceived checks remaining <= 0, and 150k > 100k creates 50k credit!
        var refund = await mgrPaymentSvc.RecordOverpaymentRefundAsync(oneOffA.Id, 50_000m, "REF-1", Guid.NewGuid(), default);
        Assert.Equal(managerDto.Id, refund.CreatedByUserId);

        // Manager cannot access Invoice in Property B by ID
        await Assert.ThrowsAsync<BusinessException>(() => mgrInvoiceSvc.GetDetailAsync(contractInB.DepositInvoiceId!.Value, default));

        // Immediate revocation of Property A: next request to Property A fails, Property C still works!
        await landlordManagerSvc.RevokePropertyAsync(managerDto.Id, propA.Id, default);
        await Assert.ThrowsAsync<BusinessException>(() => mgrPropSvc.GetAsync(propA.Id, default));
        var propCResult = await mgrPropSvc.GetAsync(propC.Id, default);
        Assert.Equal(propC.Id, propCResult.Id);

        // Immediate lock: next request to Property C fails
        await landlordManagerSvc.SetLockStateAsync(managerDto.Id, isActive: false, default);
        await Assert.ThrowsAsync<BusinessException>(() => mgrPropSvc.GetAsync(propC.Id, default));

        // Unlock works again
        await landlordManagerSvc.SetLockStateAsync(managerDto.Id, isActive: true, default);
        Assert.Equal(propC.Id, (await mgrPropSvc.GetAsync(propC.Id, default)).Id);

        // Delete Manager with referenced records -> soft-deletes, hides from list, preserves business history!
        await landlordManagerSvc.DeleteAsync(managerDto.Id, default);
        var listAfterDelete = await landlordManagerSvc.ListAsync(new ManagerFilter(1, 20, null, null), default);
        Assert.Equal(0, listAfterDelete.TotalItems);
        var deletedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == managerDto.Id);
        Assert.False(deletedUser.IsActive);
        Assert.NotNull(deletedUser.DeletedAt);
        Assert.True(await db.Refunds.AnyAsync(r => r.Id == refund.Id));
        Assert.True(await db.TenantProfiles.AnyAsync(t => t.Id == tenantProfile.Id));
    }
}
