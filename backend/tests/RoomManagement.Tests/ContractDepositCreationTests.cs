using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class ContractDepositCreationTests
{
    [SqlServerFact]
    public async Task Sending_offer_reports_overlapping_contract_before_occupied_room_status()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId) = await SeedAsync(database);
        var start = VietnamDate.Today;
        var end = new DateOnly(start.Year, start.Month,
            DateTime.DaysInMonth(start.Year, start.Month));
        Guid draftId;
        await using (var db = database.CreateContext())
        {
            draftId = (await Service(db, ownerId).CreateAsync(propertyId,
                Input(roomId, "CT-NEW-DRAFT") with { StartDate = start, EndDate = end },
                CancellationToken.None)).Contract.Id;
            db.Contracts.Add(new Contract { Id = Guid.NewGuid(), RoomId = roomId,
                ContractCode = "CT-EXISTING", StartDate = start,
                EndDate = end, MonthlyRent = 3_000_000,
                Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow });
            await db.Rooms.Where(r => r.Id == roomId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RoomStatus.Occupied));
            await db.SaveChangesAsync();
        }
        await using (var db = database.CreateContext())
        {
            var room = await new RoomStore(db).GetResultAsync(propertyId, roomId, ownerId,
                CancellationToken.None);
            Assert.Equal(RoomStatus.Occupied, room!.Status);
            var draft = await db.Contracts.SingleAsync(c => c.Id == draftId);
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                new ContractStore(db).SendOfferSafelyAsync(draft, ownerId, CancellationToken.None));
            Assert.Equal("CONTRACT_PERIOD_OVERLAP", error.Code);
            Assert.Equal("CT-EXISTING", error.ConflictingContract?.Code);
        }
    }

    [SqlServerFact]
    public async Task Creation_saves_members_atomically_even_when_tenant_has_another_active_contract()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId) = await SeedAsync(database);
        var otherRoomId = Guid.NewGuid();
        var existingContractId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        const string tenantUserId = "multi-contract-tenant";
        await using (var db = database.CreateContext())
        {
            db.Users.Add(new AppUser { Id = tenantUserId, UserName = tenantUserId,
                NormalizedUserName = tenantUserId.ToUpperInvariant(), PasswordHash = "test-hash",
                Role = UserRoles.Tenant, IsActive = true, FullName = "Người thuê",
                PhoneNumber = "0912345678", CreatedAt = DateTime.UtcNow });
            db.Rooms.Add(new Room { Id = otherRoomId, PropertyId = propertyId, RoomNumber = "B202",
                BasePrice = 3_000_000, MaxOccupants = 2, CreatedAt = DateTime.UtcNow });
            db.TenantProfiles.Add(new TenantProfile { Id = profileId, UserId = tenantUserId,
                FullName = "Người thuê", PhoneNumber = "0912345678",
                CreatedByUserId = tenantUserId, CreatedAt = DateTime.UtcNow });
            db.Contracts.Add(new Contract { Id = existingContractId, RoomId = otherRoomId,
                ContractCode = "CT-OTHER-ROOM", StartDate = new DateOnly(2027, 1, 1),
                EndDate = new DateOnly(2027, 1, 31), MonthlyRent = 3_000_000,
                Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow });
            db.ContractTenants.Add(new ContractTenant { Id = Guid.NewGuid(),
                ContractId = existingContractId, TenantId = profileId,
                JoinDate = new DateOnly(2027, 1, 1), IsContactPerson = true,
                CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var created = await Service(db, ownerId).CreateAsync(propertyId,
                Input(roomId, "CT-SECOND-ROOM") with
                {
                    Members = [new CreateContractMemberInput(tenantUserId, true)]
                }, CancellationToken.None);
            Assert.Single(await db.ContractTenants.Where(t => t.ContractId == created.Contract.Id).ToListAsync());
            var invalid = Input(roomId, "CT-NO-USER") with
            {
                Members = [new CreateContractMemberInput("missing-user", true)]
            };
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                Service(db, ownerId).CreateAsync(propertyId, invalid, CancellationToken.None));
            Assert.Equal("TENANT_NOT_FOUND", error.Code);
        }
        await using (var db = database.CreateContext())
        {
            Assert.False(await db.Contracts.AnyAsync(c => c.ContractCode == "CT-NO-USER"));
            Assert.Single(await db.Invoices.ToListAsync());
        }
    }

    [SqlServerFact]
    public async Task Draft_does_not_reserve_room_but_sent_offer_does()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId) = await SeedAsync(database);
        Guid firstId;
        await using (var db = database.CreateContext())
        {
            var service = Service(db, ownerId);
            firstId = (await service.CreateAsync(propertyId, Input(roomId, "CT-FIRST"), CancellationToken.None)).Contract.Id;
            await service.CreateAsync(propertyId, Input(roomId, "CT-SECOND"), CancellationToken.None);
        }
        await using (var db = database.CreateContext())
        {
            var candidate = new Contract { Id = Guid.NewGuid(), RoomId = roomId,
                StartDate = new DateOnly(2027, 1, 15), EndDate = new DateOnly(2027, 2, 28) };
            var store = new ContractStore(db);
            Assert.False(await store.HasOverlappingContractAsync(candidate, CancellationToken.None));
            await db.Contracts.Where(c => c.Id == firstId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ContractStatus.PendingTenantConfirmation));
            Assert.True(await store.HasOverlappingContractAsync(candidate, CancellationToken.None));
            candidate.StartDate = new DateOnly(2027, 2, 1);
            Assert.False(await store.HasOverlappingContractAsync(candidate, CancellationToken.None));
        }
    }

    [SqlServerFact]
    public async Task Inactive_drafts_cancel_after_five_days_except_paid_deposits()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId) = await SeedAsync(database);
        var now = DateTime.UtcNow;
        Guid unpaidId, paidId, recentlyUpdatedId;
        await using (var db = database.CreateContext())
        {
            var service = Service(db, ownerId);
            unpaidId = (await service.CreateAsync(propertyId, Input(roomId, "CT-OLD-UNPAID"), CancellationToken.None)).Contract.Id;
            paidId = (await service.CreateAsync(propertyId, Input(roomId, "CT-OLD-PAID") with
            {
                DepositPaymentStatus = InitialDepositPaymentStatus.Paid,
                DepositPaymentMethod = PaymentMethod.Cash,
                DepositPaidAt = new DateTimeOffset(now.AddDays(-6), TimeSpan.Zero)
            }, CancellationToken.None)).Contract.Id;
            recentlyUpdatedId = (await service.CreateAsync(propertyId, Input(roomId, "CT-RECENT"), CancellationToken.None)).Contract.Id;
        }
        await using (var db = database.CreateContext())
        {
            await db.Contracts.Where(c => c.Id == unpaidId || c.Id == paidId || c.Id == recentlyUpdatedId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, now.AddDays(-6)));
            await db.Contracts.Where(c => c.Id == recentlyUpdatedId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, now.AddDays(-1)));
            var result = await new ContractDraftExpiryService(db).CancelInactiveAsync(now, CancellationToken.None);
            Assert.Equal(1, result.Cancelled);
            Assert.Equal(1, result.NeedsReview);
            Assert.Equal(ContractStatus.Cancelled, (await db.Contracts.SingleAsync(c => c.Id == unpaidId)).Status);
            Assert.Equal(InvoiceStatus.Cancelled, (await db.Invoices.SingleAsync(i => i.ContractId == unpaidId)).Status);
            Assert.Equal(ContractStatus.Draft, (await db.Contracts.SingleAsync(c => c.Id == paidId)).Status);
            Assert.Equal(ContractStatus.Draft, (await db.Contracts.SingleAsync(c => c.Id == recentlyUpdatedId)).Status);
            Assert.Single(await db.UserNotifications.Where(n => n.ContractId == paidId &&
                n.Type == "ContractDraftNeedsReview").ToListAsync());
        }
    }

    [SqlServerFact]
    public async Task Creating_contract_creates_issued_deposit_and_optional_verified_payment()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId) = await SeedAsync(database);
        var paidAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        Guid unpaidId;
        Guid paidId;
        Guid zeroId;
        await using (var context = database.CreateContext())
        {
            var service = Service(context, ownerId);
            var unpaid = await service.CreateAsync(propertyId, Input(roomId, "CT-UNPAID"), CancellationToken.None);
            var paid = await service.CreateAsync(propertyId, Input(roomId, "CT-PAID") with
            {
                DepositPaymentStatus = InitialDepositPaymentStatus.Paid,
                DepositPaymentMethod = PaymentMethod.BankTransfer,
                DepositPaidAt = paidAt
            }, CancellationToken.None);
            var zero = await service.CreateAsync(propertyId, Input(roomId, "CT-ZERO") with
            {
                DepositAmount = 0
            }, CancellationToken.None);
            unpaidId = unpaid.Contract.Id;
            paidId = paid.Contract.Id;
            zeroId = zero.Contract.Id;
            Assert.NotNull(unpaid.DepositInvoiceId);
            Assert.NotNull(paid.DepositInvoiceId);
            Assert.Null(zero.DepositInvoiceId);
        }

        await using (var context = database.CreateContext())
        {
            var unpaid = await context.Invoices.SingleAsync(x => x.ContractId == unpaidId);
            var paid = await context.Invoices.SingleAsync(x => x.ContractId == paidId);
            Assert.Equal(InvoiceStatus.Issued, unpaid.Status);
            Assert.Equal(InvoiceType.Deposit, unpaid.InvoiceType);
            Assert.Equal(1_000_000m, unpaid.TotalAmount);
            Assert.Equal(unpaid.TotalAmount, (await context.InvoiceItems.SingleAsync(x => x.InvoiceId == unpaid.Id)).Amount);
            Assert.Equal(1_000_000m, (await context.InvoiceBalances.SingleAsync(x => x.InvoiceId == unpaid.Id)).RemainingAmount);
            Assert.Empty(await context.Payments.Where(x => x.InvoiceId == unpaid.Id).ToListAsync());

            var payment = await context.Payments.SingleAsync(x => x.InvoiceId == paid.Id);
            Assert.Equal(PaymentStatus.Success, payment.Status);
            Assert.Equal(PaymentMethod.BankTransfer, payment.PaymentMethod);
            Assert.Equal(1_000_000m, payment.VerifiedAmount);
            Assert.Equal(ownerId, payment.VerifiedByUserId);
            Assert.True(Math.Abs((payment.PaidAt!.Value - paidAt.UtcDateTime).TotalSeconds) < 1);
            Assert.Single(await context.PaymentReviewEvents.Where(x => x.PaymentId == payment.Id).ToListAsync());
            Assert.Equal(0m, (await context.InvoiceBalances.SingleAsync(x => x.InvoiceId == paid.Id)).RemainingAmount);
            Assert.Empty(await context.Invoices.Where(x => x.ContractId == zeroId).ToListAsync());
        }
    }

    [SqlServerFact]
    public async Task Invalid_paid_choice_and_duplicate_code_leave_no_extra_financial_rows()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId) = await SeedAsync(database);
        await using (var context = database.CreateContext())
        {
            var service = Service(context, ownerId);
            var invalid = Input(roomId, "CT-INVALID") with
            {
                DepositPaymentStatus = InitialDepositPaymentStatus.Paid
            };
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                service.CreateAsync(propertyId, invalid, CancellationToken.None));
            Assert.Equal("DEPOSIT_PAYMENT_INVALID", error.Code);
            await service.CreateAsync(propertyId, Input(roomId, "CT-UNIQUE"), CancellationToken.None);
        }
        await using (var context = database.CreateContext())
        {
            var service = Service(context, ownerId);
            await Assert.ThrowsAsync<BusinessException>(() =>
                service.CreateAsync(propertyId, Input(roomId, "CT-UNIQUE") with
                {
                    DepositPaymentStatus = InitialDepositPaymentStatus.Paid,
                    DepositPaymentMethod = PaymentMethod.Cash,
                    DepositPaidAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                }, CancellationToken.None));
        }
        await using (var context = database.CreateContext())
        {
            Assert.Single(await context.Contracts.ToListAsync());
            Assert.Single(await context.Invoices.ToListAsync());
            Assert.Single(await context.InvoiceItems.ToListAsync());
            Assert.Empty(await context.Payments.ToListAsync());
        }
    }

    private static ContractService Service(RoomManagementDbContext context, string ownerId) =>
        new(new CurrentLandlord(ownerId), new LandlordStore(context), new ContractStore(context));

    private static CreateContractInput Input(Guid roomId, string code) =>
        new(roomId, code, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31),
            3_000_000m, 1_000_000m, 5, null);

    private static async Task<(string OwnerId, Guid PropertyId, Guid RoomId)> SeedAsync(SqlServerDatabase database)
    {
        const string ownerId = "deposit-creation-owner";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        await using var context = database.CreateContext();
        context.Users.Add(new AppUser
        {
            Id = ownerId, UserName = ownerId, NormalizedUserName = ownerId.ToUpperInvariant(),
            PasswordHash = "test-hash", Role = UserRoles.Landlord,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        context.Properties.Add(new Property
        {
            Id = propertyId, OwnerUserId = ownerId, Name = "Khu kiểm thử cọc",
            Address = "Địa chỉ thử", IsActive = true, CreatedAt = DateTime.UtcNow
        });
        context.Rooms.Add(new Room
        {
            Id = roomId, PropertyId = propertyId, RoomNumber = "A101",
            BasePrice = 3_000_000m, MaxOccupants = 2, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return (ownerId, propertyId, roomId);
    }

    private sealed record CurrentLandlord(string UserId) : ICurrentLandlord
    {
        public string GetUserId() => UserId;
    }
}
