using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Errors;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class BillingInvoiceStoreTests
{
    [SqlServerFact]
    public async Task Concurrent_contract_activation_allows_only_one_overlapping_room_booking()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, _, roomId, firstId) = await SeedAsync(database);
        var secondId = Guid.NewGuid();
        await using (var seed = database.CreateContext())
        {
            var first = await seed.Contracts.SingleAsync(x => x.Id == firstId);
            first.Status = ContractStatus.Draft;
            seed.Contracts.Add(new Contract { Id = secondId, ContractCode = $"CT-{secondId:N}",
                RoomId = roomId, StartDate = first.StartDate, MonthlyRent = first.MonthlyRent,
                DepositAmount = 0, Status = ContractStatus.Draft, CreatedAt = DateTime.UtcNow });
            for (var index = 0; index < 2; index++)
            {
                var profileId = Guid.NewGuid();
                seed.TenantProfiles.Add(new TenantProfile { Id = profileId, CreatedByUserId = ownerId,
                    FullName = $"Người thuê {index}", PhoneNumber = $"090000003{index}", CreatedAt = DateTime.UtcNow });
                seed.ContractTenants.Add(new ContractTenant { Id = Guid.NewGuid(),
                    ContractId = index == 0 ? firstId : secondId, TenantId = profileId,
                    JoinDate = first.StartDate, CreatedAt = DateTime.UtcNow });
            }
            await seed.SaveChangesAsync();
        }
        async Task<Exception?> Activate(Guid id)
        {
            await using var context = database.CreateContext();
            var contract = await context.Contracts.SingleAsync(x => x.Id == id);
            try { await new ContractStore(context).ActivateSafelyAsync(contract, CancellationToken.None); return null; }
            catch (Exception error) { return error; }
        }
        var outcomes = await Task.WhenAll(Activate(firstId), Activate(secondId));
        Assert.Single(outcomes, x => x is null);
        Assert.Single(outcomes, x => x is BusinessException { Type: RoomManagement.Business.Errors.BusinessErrorType.Conflict });
    }

    [SqlServerFact]
    public async Task Contract_account_visibility_room_occupants_and_expiry_debt_are_preserved()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, contractId) = await SeedAsync(database);
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var tenantId = "contract-test-tenant";
        await using (var context = database.CreateContext())
        {
            context.Users.Add(new AppUser { Id = tenantId, UserName = tenantId,
                NormalizedUserName = tenantId.ToUpperInvariant(), Email = "tenant-contract@example.test",
                NormalizedEmail = "TENANT-CONTRACT@EXAMPLE.TEST", FullName = "Người thuê thử",
                PhoneNumber = "0900000019", PasswordHash = "test-hash", Role = UserRoles.Tenant,
                IsActive = true, CreatedAt = DateTime.UtcNow });
            foreach (var index in new[] { 2, 3 })
                context.Users.Add(new AppUser { Id = $"contract-test-tenant-{index}",
                    UserName = $"contract-test-tenant-{index}",
                    NormalizedUserName = $"CONTRACT-TEST-TENANT-{index}",
                    Email = $"tenant{index}@example.test", NormalizedEmail = $"TENANT{index}@EXAMPLE.TEST",
                    FullName = $"Người thuê {index}", PhoneNumber = $"090000002{index}",
                    PasswordHash = "test-hash", Role = UserRoles.Tenant,
                    IsActive = true, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            var store = new ContractStore(context);
            var contract = await context.Contracts.SingleAsync(x => x.Id == contractId);
            await store.AddUserAsync(contract, "tenant-contract@example.test", today, CancellationToken.None);
            await store.AddUserAsync(contract, "tenant2@example.test", today, CancellationToken.None);
            var capacityError = await Assert.ThrowsAsync<BusinessException>(() =>
                store.AddUserAsync(contract, "tenant3@example.test", today, CancellationToken.None));
            Assert.Equal("TENANT_OCCUPANCY_CONFLICT", capacityError.Code);
            Assert.NotNull(await store.GetDetailMineAsync(contractId, tenantId, CancellationToken.None));
            Assert.Null(await store.GetDetailMineAsync(contractId, "other-tenant", CancellationToken.None));
            Assert.Null(await store.GetDetailOwnedAsync(propertyId, contractId, "other-owner", CancellationToken.None));
            var room = await new RoomStore(context).GetResultAsync(propertyId, roomId, ownerId, CancellationToken.None);
            Assert.Equal(RoomStatus.Occupied, room!.Status);
            Assert.Equal(2, room.Occupants!.Count);
            context.Invoices.Add(new Invoice { Id = Guid.NewGuid(), ContractId = contractId,
                RoomId = roomId, InvoiceCode = $"INV-{Guid.NewGuid():N}", InvoiceType = InvoiceType.Monthly,
                BillingYear = (short)today.Year, BillingMonth = (byte)today.Month,
                IssueDate = today, DueDate = today.AddDays(5), TotalAmount = 100000,
                Status = InvoiceStatus.Issued, CreatedAt = DateTime.UtcNow });
            contract.EndDate = today.AddDays(-1);
            await context.SaveChangesAsync();
        }
        await using (var context = database.CreateContext())
        {
            var store = new ContractStore(context);
            await store.ExpireDueAsync(today, CancellationToken.None);
            var detail = await store.GetDetailOwnedAsync(propertyId, contractId, ownerId, CancellationToken.None);
            Assert.Equal(ContractStatus.Expired, detail!.Contract.Status);
            Assert.Equal(100000, detail.OutstandingAmount);
            Assert.False(detail.HasSettlement);
            var room = await new RoomStore(context).GetResultAsync(propertyId, roomId, ownerId, CancellationToken.None);
            Assert.Equal(RoomStatus.Available, room!.Status);
            Assert.Empty(room.Occupants!);
        }
    }

    [SqlServerFact]
    public async Task Tenant_profile_scope_and_billing_month_access_are_enforced()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, contractId) = await SeedAsync(database);
        var otherOwnerId = "invoice-other-owner";
        var formerTenantId = "invoice-former-tenant";
        var newTenantId = "invoice-new-tenant";
        var formerProfileId = Guid.NewGuid();
        var newProfileId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using (var context = database.CreateContext())
        {
            foreach (var (id, role) in new[] {
                (otherOwnerId, UserRoles.Landlord),
                (formerTenantId, UserRoles.Tenant),
                (newTenantId, UserRoles.Tenant) })
                context.Users.Add(new AppUser {
                    Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(),
                    PasswordHash = "test-hash", Role = role, IsActive = true, CreatedAt = DateTime.UtcNow
                });
            context.TenantProfiles.AddRange(
                new TenantProfile { Id = formerProfileId, UserId = formerTenantId,
                    CreatedByUserId = ownerId, FullName = "Người thuê cũ",
                    PhoneNumber = "0900000011", CreatedAt = DateTime.UtcNow },
                new TenantProfile { Id = newProfileId, UserId = newTenantId,
                    CreatedByUserId = otherOwnerId, FullName = "Người thuê mới",
                    PhoneNumber = "0900000012", CreatedAt = DateTime.UtcNow });
            context.ContractTenants.AddRange(
                new ContractTenant { Id = Guid.NewGuid(), ContractId = contractId,
                    TenantId = formerProfileId, JoinDate = new DateOnly(2026, 1, 1),
                    LeaveDate = new DateOnly(2026, 11, 30), CreatedAt = DateTime.UtcNow },
                new ContractTenant { Id = Guid.NewGuid(), ContractId = contractId,
                    TenantId = newProfileId, JoinDate = new DateOnly(2026, 12, 1),
                    CreatedAt = DateTime.UtcNow });
            context.Invoices.Add(new Invoice {
                Id = invoiceId, ContractId = contractId, RoomId = roomId,
                InvoiceCode = $"INV-{invoiceId:N}", InvoiceType = InvoiceType.Monthly,
                BillingYear = 2026, BillingMonth = 11,
                IssueDate = new DateOnly(2026, 12, 1), DueDate = new DateOnly(2026, 12, 5),
                TotalAmount = 1_000_000, Status = InvoiceStatus.Issued, CreatedAt = DateTime.UtcNow
            });
            context.InvoiceItems.Add(new InvoiceItem {
                Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = InvoiceItemType.Rent,
                Description = "Tiền phòng", Quantity = 1, UnitPrice = 1_000_000,
                Amount = 1_000_000, CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
        await using (var context = database.CreateContext())
        {
            var contracts = new ContractStore(context);
            Assert.True(await contracts.TenantProfileOwnedAsync(formerProfileId, ownerId, CancellationToken.None));
            Assert.False(await contracts.TenantProfileOwnedAsync(newProfileId, ownerId, CancellationToken.None));
            var formerDetail = await contracts.GetDetailMineAsync(contractId, formerTenantId, CancellationToken.None);
            Assert.Single(formerDetail!.Tenants);
            Assert.Equal(formerTenantId, formerDetail.Tenants[0].UserId);
            var ownerDetail = await contracts.GetDetailOwnedAsync(propertyId, contractId, ownerId, CancellationToken.None);
            Assert.Equal(2, ownerDetail!.Tenants.Count);
            var invoices = new InvoiceStore(context);
            Assert.NotNull(await invoices.GetTenantDetailAsync(invoiceId, formerTenantId, CancellationToken.None));
            Assert.Null(await invoices.GetTenantDetailAsync(invoiceId, newTenantId, CancellationToken.None));
        }
    }

    [SqlServerFact]
    public async Task Closed_contract_cannot_issue_existing_draft_invoice()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, _, roomId, contractId) = await SeedAsync(database);
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var invoiceId = Guid.NewGuid();
        await using (var context = database.CreateContext())
        {
            await new InvoiceStore(context).CreateDraftAsync(new Invoice {
                Id = invoiceId, ContractId = contractId, RoomId = roomId,
                InvoiceCode = $"INV-{invoiceId:N}", InvoiceType = InvoiceType.Monthly,
                BillingYear = (short)today.Year, BillingMonth = (byte)today.Month,
                IssueDate = today, DueDate = today.AddDays(5), TotalAmount = 100,
                Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow
            }, [new InvoiceItem {
                Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = InvoiceItemType.Rent,
                Description = "Tiền phòng", Quantity = 1, UnitPrice = 100,
                Amount = 100, CreatedAt = DateTime.UtcNow
            }], ownerId, CancellationToken.None);
            var contract = await context.Contracts.SingleAsync(x => x.Id == contractId);
            await new ContractStore(context).CloseSafelyAsync(contract, ContractStatus.Active,
                ContractStatus.Terminated, today, "Kết thúc", CancellationToken.None);
        }
        await using (var context = database.CreateContext())
        {
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                new InvoiceStore(context).IssueAsync(invoiceId, ownerId, CancellationToken.None));
            Assert.Equal("CONTRACT_CLOSED", error.Code);
        }
    }

    [SqlServerFact]
    public async Task Past_end_contract_cannot_activate_and_stale_cancel_cannot_overwrite_activation()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, _, _, contractId) = await SeedAsync(database);
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        await using (var context = database.CreateContext())
        {
            var contract = await context.Contracts.SingleAsync(x => x.Id == contractId);
            contract.Status = ContractStatus.Draft;
            contract.EndDate = today.AddDays(-1);
            var tenantId = Guid.NewGuid();
            context.TenantProfiles.Add(new TenantProfile { Id = tenantId, CreatedByUserId = ownerId,
                FullName = "Người thuê", PhoneNumber = "0900000088", CreatedAt = DateTime.UtcNow });
            context.ContractTenants.Add(new ContractTenant { Id = Guid.NewGuid(), ContractId = contractId,
                TenantId = tenantId, JoinDate = contract.StartDate, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                new ContractStore(context).ActivateSafelyAsync(contract, CancellationToken.None));
            Assert.Equal("CONTRACT_ALREADY_EXPIRED", error.Code);
            contract.EndDate = today.AddDays(1);
            await context.SaveChangesAsync();
        }
        await using var staleContext = database.CreateContext();
        var staleContract = await staleContext.Contracts.SingleAsync(x => x.Id == contractId);
        await using (var activeContext = database.CreateContext())
        {
            var activeContract = await activeContext.Contracts.SingleAsync(x => x.Id == contractId);
            await new ContractStore(activeContext).ActivateSafelyAsync(activeContract, CancellationToken.None);
        }
        var conflict = await Assert.ThrowsAsync<BusinessException>(() =>
            new ContractStore(staleContext).CloseSafelyAsync(staleContract, ContractStatus.Draft,
                ContractStatus.Cancelled, today, null, CancellationToken.None));
        Assert.Equal("CONTRACT_ALREADY_CHANGED", conflict.Code);
    }

    [SqlServerFact]
    public async Task Invoice_draft_issue_and_owner_scope_use_sql_balance_view()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, contractId) = await SeedAsync(database);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var invoiceId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = invoiceId, ContractId = contractId, RoomId = roomId,
            InvoiceCode = $"INV-{invoiceId:N}", InvoiceType = InvoiceType.Monthly,
            BillingYear = (short)today.Year, BillingMonth = (byte)today.Month,
            IssueDate = today, DueDate = today.AddDays(5),
            TotalAmount = 3_000_000, Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow
        };
        var item = new InvoiceItem
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = InvoiceItemType.Rent,
            Description = "Tiền phòng", Quantity = 1, UnitPrice = 3_000_000,
            Amount = 3_000_000, CreatedAt = DateTime.UtcNow
        };

        await using (var context = database.CreateContext())
        {
            var store = new InvoiceStore(context);
            await store.CreateDraftAsync(invoice, [item], ownerId, CancellationToken.None);
            var draft = await store.GetOwnedDetailAsync(invoiceId, ownerId, CancellationToken.None);
            Assert.NotNull(draft);
            Assert.Equal(0, draft.Balance.EffectiveAmount);
            Assert.Null(await store.GetOwnedDetailAsync(invoiceId, "another-owner", CancellationToken.None));
        }

        await using (var context = database.CreateContext())
        {
            var store = new InvoiceStore(context);
            await store.IssueAsync(invoiceId, ownerId, CancellationToken.None);
            var issued = await store.GetOwnedDetailAsync(invoiceId, ownerId, CancellationToken.None);
            Assert.NotNull(issued);
            Assert.Equal(3_000_000, issued.Balance.EffectiveAmount);
            Assert.Equal(3_000_000, issued.Balance.RemainingAmount);
            Assert.Single(issued.Items);
            var duplicate = new Invoice
            {
                Id = Guid.NewGuid(), ContractId = contractId, RoomId = roomId,
                InvoiceCode = $"INV-{Guid.NewGuid():N}", InvoiceType = InvoiceType.Monthly,
                BillingYear = invoice.BillingYear, BillingMonth = invoice.BillingMonth,
                IssueDate = today, DueDate = today.AddDays(5),
                TotalAmount = 3_000_000, Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow
            };
            var duplicateItem = new InvoiceItem
            {
                Id = Guid.NewGuid(), InvoiceId = duplicate.Id, ItemType = InvoiceItemType.Rent,
                Description = "Tiền phòng", Quantity = 1, UnitPrice = 3_000_000,
                Amount = 3_000_000, CreatedAt = DateTime.UtcNow
            };
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                store.CreateDraftAsync(duplicate, [duplicateItem], ownerId, CancellationToken.None));
            Assert.Equal("INVOICE_ALREADY_EXISTS", error.Code);
        }
    }

    [SqlServerFact]
    public async Task Rate_overlap_and_meter_gap_are_rejected()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (_, propertyId, roomId, _) = await SeedAsync(database);
        await using var context = database.CreateContext();
        var store = new BillingInputStore(context);
        var first = new UtilityRate
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, UtilityType = UtilityType.Electricity,
            UnitPrice = 3_500, Unit = "kWh", EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = new DateOnly(2026, 12, 31), CreatedAt = DateTime.UtcNow
        };
        await store.AddRateAsync(first, CancellationToken.None);
        var overlap = new UtilityRate
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, UtilityType = UtilityType.Electricity,
            UnitPrice = 4_000, Unit = "kWh", EffectiveFrom = new DateOnly(2026, 6, 1),
            EffectiveTo = new DateOnly(2027, 5, 31), CreatedAt = DateTime.UtcNow
        };
        var rateError = await Assert.ThrowsAsync<BusinessException>(() =>
            store.AddRateAsync(overlap, CancellationToken.None));
        Assert.Equal("UTILITY_RATE_OVERLAP", rateError.Code);

        await store.AddReadingAsync(new MeterReading
        {
            Id = Guid.NewGuid(), RoomId = roomId, MeterType = UtilityType.Electricity,
            Year = 2026, Month = 1, PreviousValue = 100, CurrentValue = 120,
            RecordedAt = DateTime.UtcNow
        }, CancellationToken.None);
        var gapError = await Assert.ThrowsAsync<BusinessException>(() =>
            store.AddReadingAsync(new MeterReading
            {
                Id = Guid.NewGuid(), RoomId = roomId, MeterType = UtilityType.Electricity,
                Year = 2026, Month = 3, PreviousValue = 120, CurrentValue = 130,
                RecordedAt = DateTime.UtcNow
            }, CancellationToken.None));
        Assert.Equal("METER_READING_GAP", gapError.Code);
    }

    private static async Task<(string OwnerId, Guid PropertyId, Guid RoomId, Guid ContractId)> SeedAsync(SqlServerDatabase database)
    {
        const string ownerId = "invoice-test-owner";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        await using var context = database.CreateContext();
        context.Users.Add(new AppUser
        {
            Id = ownerId, UserName = ownerId, NormalizedUserName = ownerId.ToUpperInvariant(),
            PasswordHash = "test-hash", Role = UserRoles.Landlord,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        context.Properties.Add(new Property
        {
            Id = propertyId, OwnerUserId = ownerId, Name = "Khu thử hóa đơn",
            Address = "Địa chỉ thử", IsActive = true, CreatedAt = DateTime.UtcNow
        });
        context.Rooms.Add(new Room
        {
            Id = roomId, PropertyId = propertyId, RoomNumber = "A101",
            BasePrice = 3_000_000, MaxOccupants = 2, CreatedAt = DateTime.UtcNow
        });
        context.Contracts.Add(new Contract
        {
            Id = contractId, ContractCode = $"CT-{contractId:N}", RoomId = roomId,
            StartDate = new DateOnly(2026, 1, 1), MonthlyRent = 3_000_000,
            DepositAmount = 2_000_000, PaymentDueDay = 5,
            Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return (ownerId, propertyId, roomId, contractId);
    }
}
