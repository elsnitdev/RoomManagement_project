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
