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

public sealed class DirectReceiptGuardTests
{
    private const string OwnerId = "receipt-guard-owner";

    [SqlServerFact]
    public async Task Fully_paid_invoice_rejects_new_direct_receipt_after_review()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (invoiceId, paymentId) = await SeedAsync(database);
        await using var db = database.CreateContext();
        var service = Service(db);

        await service.ReviewAsync(paymentId,
            new ReviewPaymentInput(PaymentStatus.Success, 1_000_000m, "Đã nhận đủ"),
            CancellationToken.None);
        Assert.Equal(0m, await db.InvoiceBalances.Where(x => x.InvoiceId == invoiceId)
            .Select(x => x.RemainingAmount).SingleAsync());

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RecordReceivedAsync(invoiceId, Receipt(100_000m), CancellationToken.None));
        Assert.Equal("INVOICE_NO_AMOUNT_DUE", error.Code);
        Assert.Equal(BusinessErrorType.Conflict, error.Type);
        Assert.Single(await db.Payments.Where(x => x.InvoiceId == invoiceId).ToListAsync());
        Assert.Single(await db.PaymentReviewEvents.Where(x => x.PaymentId == paymentId).ToListAsync());
    }

    [SqlServerFact]
    public async Task Direct_receipt_allows_credit_once_then_rejects_new_key_and_accepts_retry()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (invoiceId, _) = await SeedAsync(database);
        await using var db = database.CreateContext();
        var service = Service(db);
        var receipt = Receipt(1_100_000m);

        var recorded = await service.RecordReceivedAsync(invoiceId, receipt, CancellationToken.None);
        Assert.Equal(-100_000m, await db.InvoiceBalances.Where(x => x.InvoiceId == invoiceId)
            .Select(x => x.RemainingAmount).SingleAsync());
        var retried = await service.RecordReceivedAsync(invoiceId, receipt, CancellationToken.None);
        Assert.Equal(recorded.Payment.Id, retried.Payment.Id);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RecordReceivedAsync(invoiceId, receipt with { IdempotencyKey = Guid.NewGuid() },
                CancellationToken.None));
        Assert.Equal("INVOICE_NO_AMOUNT_DUE", error.Code);
        Assert.Equal(2, await db.Payments.CountAsync(x => x.InvoiceId == invoiceId));
        Assert.Single(await db.PaymentReviewEvents.Where(x => x.PaymentId == recorded.Payment.Id).ToListAsync());
    }

    private static RecordReceivedPaymentInput Receipt(decimal amount) =>
        new(amount, PaymentMethod.Cash, DateTimeOffset.UtcNow.AddMinutes(-1),
            null, "Thu trực tiếp", Guid.NewGuid());

    private static LandlordPaymentService Service(RoomManagementDbContext db) =>
        new(new CurrentLandlord(), new LandlordStore(db), new PaymentStore(db));

    private static async Task<(Guid InvoiceId, Guid PaymentId)> SeedAsync(SqlServerDatabase database)
    {
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = VietnamDate.Today;
        await using var db = database.CreateContext();
        db.Users.AddRange(
            new AppUser { Id = OwnerId, UserName = OwnerId, NormalizedUserName = OwnerId.ToUpperInvariant(),
                PasswordHash = "test", Role = UserRoles.Landlord, IsActive = true, CreatedAt = now },
            new AppUser { Id = "receipt-guard-tenant", UserName = "receipt-guard-tenant",
                NormalizedUserName = "RECEIPT-GUARD-TENANT", PasswordHash = "test",
                Role = UserRoles.Tenant, IsActive = true, CreatedAt = now });
        db.Properties.Add(new Property { Id = propertyId, OwnerUserId = OwnerId,
            Name = "Khu thử ghi tiền", Address = "Địa chỉ thử", IsActive = true, CreatedAt = now });
        db.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId,
            RoomNumber = "A101", BasePrice = 1_000_000m, MaxOccupants = 2, CreatedAt = now });
        db.Contracts.Add(new Contract { Id = contractId, ContractCode = $"CT-{contractId:N}",
            RoomId = roomId, StartDate = today, EndDate = today.AddDays(5),
            MonthlyRent = 1_000_000m, DepositAmount = 1_000_000m,
            Status = ContractStatus.Draft, CreatedAt = now });
        db.Invoices.Add(new Invoice { Id = invoiceId, ContractId = contractId, RoomId = roomId,
            InvoiceCode = $"DEP-{invoiceId:N}", InvoiceType = InvoiceType.Deposit,
            IssueDate = today, DueDate = today.AddDays(5), TotalAmount = 1_000_000m,
            Status = InvoiceStatus.Issued, CreatedAt = now });
        db.Payments.Add(new Payment { Id = paymentId, InvoiceId = invoiceId,
            SubmittedByUserId = "receipt-guard-tenant", ClaimedAmount = 1_000_000m,
            PaymentMethod = PaymentMethod.BankTransfer, Status = PaymentStatus.Pending,
            PaidAt = now.AddMinutes(-5), TransactionCode = $"TXN-{paymentId:N}",
            IdempotencyKey = $"tenant:{Guid.NewGuid():N}", CreatedAt = now });
        await db.SaveChangesAsync();
        return (invoiceId, paymentId);
    }

    private sealed class CurrentLandlord : ICurrentLandlord
    {
        public string GetUserId() => OwnerId;
    }
}
