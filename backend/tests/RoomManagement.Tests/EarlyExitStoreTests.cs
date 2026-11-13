using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Services;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class EarlyExitStoreTests
{
    [SqlServerFact]
    public async Task Handover_releases_room_but_keeps_contract_pending_until_financials_close()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var today = VietnamDate.Today;
        var start = new DateOnly(today.Year, today.Month, 1);
        var ownerId = "early-exit-owner";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await using (var db = database.CreateContext())
        {
            db.Users.Add(new AppUser { Id = ownerId, UserName = ownerId,
                NormalizedUserName = ownerId.ToUpperInvariant(), PasswordHash = "test-hash",
                Role = UserRoles.Landlord, IsActive = true, CreatedAt = DateTime.UtcNow });
            db.Properties.Add(new Property { Id = propertyId, OwnerUserId = ownerId,
                Name = "Khu thử", Address = "Địa chỉ", IsActive = true, CreatedAt = DateTime.UtcNow });
            db.Rooms.Add(new Room { Id = roomId, PropertyId = propertyId,
                RoomNumber = "A101", BasePrice = 3_000_000, MaxOccupants = 2,
                CreatedAt = DateTime.UtcNow });
            db.TenantProfiles.Add(new TenantProfile { Id = tenantId, FullName = "Người thuê",
                CreatedByUserId = ownerId, CreatedAt = DateTime.UtcNow });
            db.Contracts.Add(new Contract { Id = contractId, RoomId = roomId,
                ContractCode = $"CT-{contractId:N}", StartDate = start,
                MonthlyRent = 3_000_000, DepositAmount = 0, PaymentDueDay = 5,
                Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow });
            db.ContractTenants.Add(new ContractTenant { Id = Guid.NewGuid(),
                ContractId = contractId, TenantId = tenantId, JoinDate = start,
                IsContactPerson = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var store = new ContractStore(db);
            var contract = await db.Contracts.SingleAsync(x => x.Id == contractId);
            await store.StartEarlyExitAsync(contract, "Rời trước hạn", CancellationToken.None);
            Assert.Equal(ContractStatus.PendingMoveOut, contract.Status);
            Assert.True(await new RoomStore(db).HasActiveContractAsync(roomId, CancellationToken.None));
            await store.RecordContractReadingAsync(contract, UtilityType.Electricity,
                (short)today.Year, (byte)today.Month, 100, 110, CancellationToken.None);
            await store.RecordContractReadingAsync(contract, UtilityType.Water,
                (short)today.Year, (byte)today.Month, 20, 22, CancellationToken.None);
            await store.HandoverAsync(contract, today, CancellationToken.None);
            Assert.Equal(ContractStatus.PendingSettlement, contract.Status);
            Assert.Equal(today, contract.HandoverOn);
            Assert.True(await new RoomStore(db).HasActiveContractAsync(roomId, CancellationToken.None));
            var tenant = await db.ContractTenants.SingleAsync(x => x.ContractId == contractId);
            Assert.Equal(today, tenant.LeaveDate);
            var nextContract = new Contract { Id = Guid.NewGuid(), RoomId = roomId,
                ContractCode = $"CT-{Guid.NewGuid():N}", StartDate = today,
                MonthlyRent = 3_000_000, DepositAmount = 0, Status = ContractStatus.Draft };
            Assert.True(await store.HasOverlappingContractAsync(nextContract, CancellationToken.None));
            nextContract.CreatedAt = DateTime.UtcNow;
            db.Contracts.Add(nextContract);
            await db.SaveChangesAsync();
            var overlap = await Assert.ThrowsAsync<BusinessException>(() =>
                store.ActivateSafelyAsync(nextContract, CancellationToken.None));
            Assert.Equal("CONTRACT_ROOM_CONFLICT", overlap.Code);
            nextContract.StartDate = today.AddDays(1);
            await db.SaveChangesAsync();
            Assert.False(await store.HasOverlappingContractAsync(nextContract, CancellationToken.None));
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                store.CompleteSettlementAsync(contract, "NoDeposit", 0, null, CancellationToken.None));
            Assert.Equal("CONTRACT_MONTHLY_INVOICE_MISSING", error.Code);

            nextContract.Status = ContractStatus.Active;
            await db.SaveChangesAsync();
            var invoices = new InvoiceStore(db);
            var finalInvoiceId = Guid.Empty;
            var nextInvoiceId = Guid.Empty;
            foreach (var currentContract in new[] { contract, nextContract })
            {
                var invoiceId = Guid.NewGuid();
                if (currentContract.Id == contractId) finalInvoiceId = invoiceId;
                else nextInvoiceId = invoiceId;
                var invoice = new Invoice { Id = invoiceId, ContractId = currentContract.Id,
                    RoomId = roomId, InvoiceCode = $"INV-{invoiceId:N}",
                    InvoiceType = InvoiceType.Monthly, BillingYear = (short)today.Year,
                    BillingMonth = (byte)today.Month, IssueDate = today,
                    DueDate = today.AddDays(5), TotalAmount = 100,
                    Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow };
                var item = new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId,
                    ItemType = InvoiceItemType.Rent, Description = "Tiền phòng", Quantity = 1,
                    UnitPrice = 100, Amount = 100, CreatedAt = DateTime.UtcNow };
                await invoices.CreateDraftAsync(invoice, [item], ownerId, CancellationToken.None);
            }
            Assert.Equal(2, await db.Invoices.CountAsync(x => x.RoomId == roomId &&
                x.InvoiceType == InvoiceType.Monthly && x.BillingYear == today.Year &&
                x.BillingMonth == today.Month));
            await invoices.IssueAsync(finalInvoiceId, ownerId, CancellationToken.None);
            await invoices.IssueAsync(nextInvoiceId, ownerId, CancellationToken.None);
            var unpaid = await Assert.ThrowsAsync<BusinessException>(() =>
                store.CompleteSettlementAsync(contract, "NoDeposit", 0, null, CancellationToken.None));
            Assert.Equal("CONTRACT_FINANCIALS_OPEN", unpaid.Code);
            db.Payments.Add(new Payment { Id = Guid.NewGuid(), InvoiceId = finalInvoiceId,
                ClaimedAmount = 100, VerifiedAmount = 100, PaymentMethod = PaymentMethod.Cash,
                Status = PaymentStatus.Success, PaidAt = DateTime.UtcNow,
                VerifiedAt = DateTime.UtcNow, VerifiedByUserId = ownerId,
                IdempotencyKey = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            await store.CompleteSettlementAsync(contract, "NoDeposit", 0, null, CancellationToken.None);
            Assert.Equal(ContractStatus.Terminated, contract.Status);
            Assert.Equal(today, contract.ClosedOn);
            Assert.True(await db.ContractSettlements.AnyAsync(x => x.ContractId == contractId &&
                x.DepositDisposition == "NoDeposit"));
        }
    }
}
