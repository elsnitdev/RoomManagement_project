using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Services;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class TenantPaymentTests
{
    [SqlServerFact]
    public async Task Future_members_can_view_deposit_and_only_see_their_own_pending_claims()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var invoiceId = await SeedAsync(database);
        await using var context = database.CreateContext();
        var invoices = new InvoiceStore(context);
        Assert.NotNull(await invoices.GetTenantDetailAsync(invoiceId, "deposit-tenant-a", CancellationToken.None));
        Assert.NotNull(await invoices.GetTenantDetailAsync(invoiceId, "deposit-tenant-b", CancellationToken.None));
        Assert.Null(await invoices.GetTenantDetailAsync(invoiceId, "deposit-stranger", CancellationToken.None));

        var payments = new PaymentService(new PaymentStore(context));
        var request = new DeclareTenantPaymentInput(500_000m, PaymentMethod.BankTransfer,
            DateTimeOffset.UtcNow.AddMinutes(-5), "TXN-TEST-001", "Đã chuyển khoản", Guid.NewGuid());
        var first = await payments.DeclareTenantAsync(invoiceId, "deposit-tenant-a", request, CancellationToken.None);
        var retry = await payments.DeclareTenantAsync(invoiceId, "deposit-tenant-a", request, CancellationToken.None);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(PaymentStatus.Pending, first.Status);
        Assert.Null(first.VerifiedAmount);
        Assert.Equal("deposit-tenant-a", first.SubmittedByUserId);
        Assert.Single(await payments.ListTenantAsync(invoiceId, "deposit-tenant-a", CancellationToken.None));
        Assert.Empty(await payments.ListTenantAsync(invoiceId, "deposit-tenant-b", CancellationToken.None));
        Assert.Equal(1_000_000m,
            (await context.InvoiceBalances.SingleAsync(x => x.InvoiceId == invoiceId)).RemainingAmount);

        var changed = await Assert.ThrowsAsync<BusinessException>(() =>
            payments.DeclareTenantAsync(invoiceId, "deposit-tenant-a", request with { Amount = 600_000m },
                CancellationToken.None));
        Assert.Equal("PAYMENT_IDEMPOTENCY_CONFLICT", changed.Code);
        var stranger = await Assert.ThrowsAsync<BusinessException>(() =>
            payments.DeclareTenantAsync(invoiceId, "deposit-stranger", request with
            {
                IdempotencyKey = Guid.NewGuid()
            }, CancellationToken.None));
        Assert.Equal("INVOICE_NOT_FOUND", stranger.Code);
    }

    [SqlServerFact]
    public async Task Cash_claim_is_pending_and_paid_invoice_rejects_new_claim()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var invoiceId = await SeedAsync(database);
        await using (var context = database.CreateContext())
        {
            var payments = new PaymentService(new PaymentStore(context));
            var cash = await payments.DeclareTenantAsync(invoiceId, "deposit-tenant-a",
                new DeclareTenantPaymentInput(1_000_000m, PaymentMethod.Cash,
                    DateTimeOffset.UtcNow.AddMinutes(-2), null, "Đã giao tiền mặt", Guid.NewGuid()),
                CancellationToken.None);
            Assert.Equal(PaymentStatus.Pending, cash.Status);
        }
        await using (var context = database.CreateContext())
        {
            context.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(), InvoiceId = invoiceId, ClaimedAmount = 1_000_000m,
                VerifiedAmount = 1_000_000m, PaymentMethod = PaymentMethod.Cash,
                Status = PaymentStatus.Success, PaidAt = DateTime.UtcNow.AddMinutes(-1),
                VerifiedAt = DateTime.UtcNow, VerifiedByUserId = "deposit-owner",
                SubmittedByUserId = "deposit-owner", IdempotencyKey = $"test-paid:{Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
        await using (var context = database.CreateContext())
        {
            var payments = new PaymentService(new PaymentStore(context));
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                payments.DeclareTenantAsync(invoiceId, "deposit-tenant-b",
                    new DeclareTenantPaymentInput(100_000m, PaymentMethod.Cash,
                        DateTimeOffset.UtcNow.AddMinutes(-1), null, null, Guid.NewGuid()),
                    CancellationToken.None));
            Assert.Equal("INVOICE_ALREADY_PAID", error.Code);
        }
    }

    [SqlServerFact]
    public async Task Former_member_keeps_history_but_cannot_declare_another_payment()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var invoiceId = await SeedAsync(database);
        var claim = new DeclareTenantPaymentInput(100_000m, PaymentMethod.Cash,
            DateTimeOffset.UtcNow.AddMinutes(-5), null, null, Guid.NewGuid());
        await using (var context = database.CreateContext())
        {
            var payments = new PaymentService(new PaymentStore(context));
            await payments.DeclareTenantAsync(invoiceId, "deposit-tenant-a", claim, CancellationToken.None);
            var link = await (from member in context.ContractTenants
                              join profile in context.TenantProfiles on member.TenantId equals profile.Id
                              where profile.UserId == "deposit-tenant-a"
                              select member).SingleAsync();
            var contract = await context.Contracts.SingleAsync(x => x.Id == link.ContractId);
            contract.StartDate = VietnamDate.Today.AddDays(-10);
            link.JoinDate = VietnamDate.Today.AddDays(-10);
            link.LeaveDate = VietnamDate.Today.AddDays(-1);
            await context.SaveChangesAsync();
        }
        await using (var context = database.CreateContext())
        {
            var invoices = new InvoiceStore(context);
            Assert.NotNull(await invoices.GetTenantDetailAsync(invoiceId, "deposit-tenant-a", CancellationToken.None));
            var payments = new PaymentService(new PaymentStore(context));
            Assert.Single(await payments.ListTenantAsync(invoiceId, "deposit-tenant-a", CancellationToken.None));
            var retry = await payments.DeclareTenantAsync(invoiceId, "deposit-tenant-a", claim, CancellationToken.None);
            Assert.Equal(PaymentStatus.Pending, retry.Status);
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                payments.DeclareTenantAsync(invoiceId, "deposit-tenant-a", claim with
                {
                    IdempotencyKey = Guid.NewGuid()
                }, CancellationToken.None));
            Assert.Equal("PAYMENT_MEMBERSHIP_ENDED", error.Code);
        }
    }

    private static async Task<Guid> SeedAsync(SqlServerDatabase database)
    {
        const string ownerId = "deposit-owner";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var today = VietnamDate.Today;
        var start = today.AddMonths(1);
        var end = new DateOnly(start.Year, start.Month,
            DateTime.DaysInMonth(start.Year, start.Month));
        await using var context = database.CreateContext();
        foreach (var (id, role) in new[]
        {
            (ownerId, UserRoles.Landlord),
            ("deposit-tenant-a", UserRoles.Tenant),
            ("deposit-tenant-b", UserRoles.Tenant),
            ("deposit-stranger", UserRoles.Tenant)
        })
            context.Users.Add(new AppUser
            {
                Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(),
                PasswordHash = "test-hash", Role = role, IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        context.Properties.Add(new Property
        {
            Id = propertyId, OwnerUserId = ownerId, Name = "Khu thử thanh toán",
            Address = "Địa chỉ thử", IsActive = true, CreatedAt = DateTime.UtcNow
        });
        context.Rooms.Add(new Room
        {
            Id = roomId, PropertyId = propertyId, RoomNumber = "A101",
            BasePrice = 3_000_000m, MaxOccupants = 2, CreatedAt = DateTime.UtcNow
        });
        context.Contracts.Add(new Contract
        {
            Id = contractId, ContractCode = $"CT-{contractId:N}", RoomId = roomId,
            StartDate = start, EndDate = end, MonthlyRent = 3_000_000m,
            DepositAmount = 1_000_000m, Status = ContractStatus.Draft,
            CreatedAt = DateTime.UtcNow
        });
        foreach (var id in new[] { "deposit-tenant-a", "deposit-tenant-b" })
        {
            var profileId = Guid.NewGuid();
            context.TenantProfiles.Add(new TenantProfile
            {
                Id = profileId, UserId = id, CreatedByUserId = ownerId,
                FullName = id, PhoneNumber = "0900000011", CreatedAt = DateTime.UtcNow
            });
            context.ContractTenants.Add(new ContractTenant
            {
                Id = Guid.NewGuid(), ContractId = contractId, TenantId = profileId,
                JoinDate = start, CreatedAt = DateTime.UtcNow
            });
        }
        context.Invoices.Add(new Invoice
        {
            Id = invoiceId, ContractId = contractId, RoomId = roomId,
            InvoiceCode = $"DEP-{invoiceId:N}", InvoiceType = InvoiceType.Deposit,
            IssueDate = today, DueDate = start, TotalAmount = 1_000_000m,
            Status = InvoiceStatus.Issued, CreatedAt = DateTime.UtcNow
        });
        context.InvoiceItems.Add(new InvoiceItem
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = InvoiceItemType.Deposit,
            Description = "Tiền đặt cọc", Quantity = 1, UnitPrice = 1_000_000m,
            Amount = 1_000_000m, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return invoiceId;
    }
}
