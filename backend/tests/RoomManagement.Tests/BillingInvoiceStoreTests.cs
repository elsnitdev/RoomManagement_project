using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using Xunit.Abstractions;

namespace RoomManagement.Tests;

public sealed class BillingInvoiceStoreTests(ITestOutputHelper output)
{
    private sealed record BillingActor(string UserId) : ICurrentLandlord
    {
        public string GetUserId() => UserId;
    }

    [SqlServerFact]
    public async Task Concurrent_close_creates_one_draft_and_batch_issue_requires_the_reviewed_amount()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, contractId) = await SeedAsync(database);
        var today = VietnamDate.Today;
        var period = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var year = (short)period.Year;
        var month = (byte)period.Month;
        var missingContractId = Guid.NewGuid();
        const string tenantUserId = "monthly-batch-test-tenant";
        await using (var seed = database.CreateContext())
        {
            var contract = await seed.Contracts.SingleAsync(x => x.Id == contractId);
            contract.StartDate = period;
            contract.PaymentDueDay = 31;
            var tenantProfileId = Guid.NewGuid();
            seed.Users.Add(new AppUser { Id = tenantUserId, UserName = tenantUserId,
                NormalizedUserName = tenantUserId.ToUpperInvariant(), FullName = "Người thuê thử",
                PasswordHash = "test-hash", Role = UserRoles.Tenant, IsActive = true, CreatedAt = DateTime.UtcNow });
            seed.TenantProfiles.Add(new TenantProfile { Id = tenantProfileId, UserId = tenantUserId,
                CreatedByUserId = ownerId, FullName = "Người thuê thử", PhoneNumber = "0900000101",
                DateOfBirth = new DateOnly(1990, 1, 1), CreatedAt = DateTime.UtcNow });
            seed.ContractTenants.Add(new ContractTenant { Id = Guid.NewGuid(), ContractId = contractId,
                TenantId = tenantProfileId, JoinDate = period, IsContactPerson = true, CreatedAt = DateTime.UtcNow });
            var missingRoomId = Guid.NewGuid();
            seed.Rooms.Add(new Room { Id = missingRoomId, PropertyId = propertyId, RoomNumber = "A102",
                BasePrice = 3_000_000, MaxOccupants = 2, CreatedAt = DateTime.UtcNow });
            seed.Contracts.Add(new Contract { Id = missingContractId, RoomId = missingRoomId,
                ContractCode = $"CT-{missingContractId:N}", StartDate = period, PaymentDueDay = 31,
                MonthlyRent = 3_000_000, Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow });
            foreach (var type in new[] { UtilityType.Electricity, UtilityType.Water })
            {
                var electric = type == UtilityType.Electricity;
                seed.UtilityRates.Add(new UtilityRate { Id = Guid.NewGuid(), PropertyId = propertyId, UtilityType = type,
                    Unit = electric ? "kWh" : "m³", UnitPrice = electric ? 3_500m : 15_000m,
                    EffectiveFrom = period, CreatedAt = DateTime.UtcNow });
                seed.MeterReadings.Add(new MeterReading { Id = Guid.NewGuid(), ContractId = contractId,
                    RoomId = roomId, MeterType = type, Year = year, Month = month,
                    PreviousValue = electric ? 100 : 30, CurrentValue = electric ? 119 : 33, RecordedAt = DateTime.UtcNow });
            }
            await seed.SaveChangesAsync();
        }

        InvoiceService Service(RoomManagementDbContext db) => new(new BillingActor(ownerId), new LandlordStore(db),
            new InvoiceStore(db), new BillingInputStore(db), new MonthlyInvoiceCalculator());
        var actor = new OperatorContext(ownerId, ownerId, UserRoles.Landlord, false);
        var input = new MonthlyInvoiceInput(contractId, year, month);
        await using (var previewContext = database.CreateContext())
        {
            var service = Service(previewContext);
            var preview = await service.PreviewMonthlyAsync(propertyId, input, default);
            Assert.Equal(3_111_500m, preview.TotalAmount);
            await using var import = database.CreateContext();
            var updated = await ImportStore(import).SaveAsync(propertyId, period, actor,
                new MeterImportRowInput(roomId, 120m, null, 100m, ExpectedElectricityCurrent: 119m), default);
            Assert.Equal("Saved", updated.Electricity.Status);
            var conflict = await Assert.ThrowsAsync<BusinessException>(() =>
                service.CreateMonthlyAsync(propertyId, input with { PreviewVersion = preview.Version }, default));
            Assert.Equal("INVOICE_PREVIEW_CHANGED", conflict.Code);
        }

        async Task<IReadOnlyList<BillingBatchResult>> CloseAsync()
        {
            await using var db = database.CreateContext();
            return await Service(db).CloseMonthAsync(propertyId, year, month, default);
        }
        async Task UpdateElectricityAsync(decimal expected, decimal value)
        {
            await using var db = database.CreateContext();
            var result = await ImportStore(db).SaveAsync(propertyId, period, actor,
                new MeterImportRowInput(roomId, value, null, 100m, ExpectedElectricityCurrent: expected), default);
            Assert.Equal("Saved", result.Electricity.Status);
        }

        var closeRequests = Task.WhenAll(CloseAsync(), CloseAsync());
        await Task.WhenAll(closeRequests, UpdateElectricityAsync(120m, 121m));
        var requests = await closeRequests;
        var rows = requests.SelectMany(x => x).ToList();
        var created = Assert.Single(rows, x => x.ContractId == contractId && x.Status == "DraftCreated");
        Assert.Single(rows, x => x.ContractId == contractId && x.Status == "AlreadyExists");
        Assert.Equal(2, rows.Count(x => x.ContractId == missingContractId && x.Code == "INVOICE_INPUT_INCOMPLETE"));
        var invoiceId = created.InvoiceId!.Value;
        string oldVersion;
        await using (var review = database.CreateContext())
        {
            var detail = await Service(review).GetDetailAsync(invoiceId, default);
            Assert.Equal(3_118_500m, detail.Invoice.TotalAmount);
            Assert.Equal(InvoiceStatus.Draft, detail.Invoice.Status);
            oldVersion = InvoiceReviewVersion.ForDraft(detail.Invoice, detail.Items);
        }
        await UpdateElectricityAsync(121m, 122m);
        async Task<BillingBatchResult> IssueAsync(string version)
        {
            await using var db = database.CreateContext();
            return Assert.Single(await Service(db).IssueMonthAsync(propertyId, year, month,
                [new(invoiceId, version)], default));
        }

        var stale = await IssueAsync(oldVersion);
        Assert.Equal("INVOICE_REVIEW_CHANGED", stale.Code);
        string newVersion;
        await using (var review = database.CreateContext())
        {
            var detail = await Service(review).GetDetailAsync(invoiceId, default);
            Assert.Equal(3_122_000m, detail.Invoice.TotalAmount);
            Assert.Equal(InvoiceStatus.Draft, detail.Invoice.Status);
            newVersion = InvoiceReviewVersion.ForDraft(detail.Invoice, detail.Items);
        }
        var issues = await Task.WhenAll(IssueAsync(newVersion), IssueAsync(newVersion));
        Assert.All(issues, result => Assert.Equal("Issued", result.Status));
        await using var verify = database.CreateContext();
        Assert.Equal(InvoiceStatus.Issued, (await verify.Invoices.SingleAsync()).Status);
        Assert.Single(await verify.UserNotifications.Where(x => x.InvoiceId == invoiceId &&
            x.UserId == tenantUserId && x.Type == "InvoiceIssued").ToListAsync());
        output.WriteLine($"Kỳ {month:D2}/{year} | A101: thuê 3000000, điện 100→121 × 3500, nước 30→33 × 15000");
        output.WriteLine("Xem trước 3111500 với số điện 119; đổi thành 120 → INVOICE_PREVIEW_CHANGED");
        output.WriteLine("Hai yêu cầu chốt: DraftCreated + AlreadyExists; A102: INVOICE_INPUT_INCOMPLETE");
        output.WriteLine("Cập nhật điện thành 121 đồng thời với chốt → nháp 3118500");
        output.WriteLine("Sửa số điện thành 122 → nháp 3122000; duyệt cũ → INVOICE_REVIEW_CHANGED");
        output.WriteLine("Hai yêu cầu phát hành phiên bản mới: Issued + Issued; đúng một hóa đơn và một thông báo");
    }

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
                    FullName = $"Người thuê {index}", PhoneNumber = $"090000003{index}",
                    DateOfBirth = new DateOnly(1990, 1, 1), CreatedAt = DateTime.UtcNow });
                var linkId = Guid.NewGuid();
                seed.ContractTenants.Add(new ContractTenant { Id = linkId,
                    ContractId = index == 0 ? firstId : secondId, TenantId = profileId,
                    JoinDate = first.StartDate, IsContactPerson = true, CreatedAt = DateTime.UtcNow });
                seed.MemberCommitments.Add(new MemberCommitment { Id = Guid.NewGuid(),
                    ContractTenantId = linkId, CommitmentType = CommitmentType.RoomRulesAgreement,
                    SignedOn = first.StartDate, SignerName = $"Người thuê {index}", TermsSnapshot = "Nội quy.",
                    ConfirmedAt = DateTime.UtcNow, ConfirmedByUserId = ownerId,
                    EvidenceStatus = CommitmentEvidenceStatus.PendingUpload, CreatedAt = DateTime.UtcNow });
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
            foreach (var (id, email, name) in new[] {
                (tenantId, "tenant-contract@example.test", "Người thuê thử"),
                ("contract-test-tenant-2", "tenant2@example.test", "Người thuê 2"),
                ("contract-test-tenant-3", "tenant3@example.test", "Người thuê 3") })
                context.TenantProfiles.Add(new TenantProfile { Id = Guid.NewGuid(), UserId = id,
                    CreatedByUserId = ownerId, FullName = name, Email = email,
                    PhoneNumber = "0900000019", DateOfBirth = new DateOnly(1990, 1, 1),
                    CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            var store = new ContractStore(context);
            var contract = await context.Contracts.SingleAsync(x => x.Id == contractId);
            var firstLink = await store.AddUserAsync(contract, "tenant-contract@example.test", today, CancellationToken.None);
            var secondLink = await store.AddUserAsync(contract, "tenant2@example.test", today, CancellationToken.None);
            foreach (var link in new[] { firstLink, secondLink })
                await store.ConfirmCommitmentAsync(contract, link.Id,
                    new(CommitmentType.RoomRulesAgreement, today, "Người thuê", null, null, "Nội quy."),
                    ownerId, CancellationToken.None);
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
                FullName = "Người thuê", PhoneNumber = "0900000088",
                DateOfBirth = new DateOnly(1990, 1, 1), CreatedAt = DateTime.UtcNow });
            var linkId = Guid.NewGuid();
            context.ContractTenants.Add(new ContractTenant { Id = linkId, ContractId = contractId,
                TenantId = tenantId, JoinDate = contract.StartDate,
                IsContactPerson = true, CreatedAt = DateTime.UtcNow });
            context.MemberCommitments.Add(new MemberCommitment { Id = Guid.NewGuid(),
                ContractTenantId = linkId, CommitmentType = CommitmentType.RoomRulesAgreement,
                SignedOn = contract.StartDate, SignerName = "Người thuê", TermsSnapshot = "Nội quy.",
                ConfirmedAt = DateTime.UtcNow, ConfirmedByUserId = ownerId,
                EvidenceStatus = CommitmentEvidenceStatus.PendingUpload, CreatedAt = DateTime.UtcNow });
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

    [SqlServerFact]
    public async Task Draft_edits_and_issued_adjustments_update_the_invoice_balance()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, contractId) = await SeedAsync(database);
        var today = RoomManagement.Business.Services.VietnamDate.Today;
        var invoiceId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await using var context = database.CreateContext();
        var store = new InvoiceStore(context);
        var invoice = new Invoice
        {
            Id = invoiceId, ContractId = contractId, RoomId = roomId,
            InvoiceCode = $"INV-{invoiceId:N}", InvoiceType = InvoiceType.Monthly,
            BillingYear = (short)today.Year, BillingMonth = (byte)today.Month,
            IssueDate = today, DueDate = today.AddDays(5), TotalAmount = 3_100_000,
            Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow
        };
        await store.CreateDraftAsync(invoice, [
            new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId,
                ItemType = InvoiceItemType.Rent, Description = "Tiền phòng",
                Quantity = 1, UnitPrice = 3_000_000, Amount = 3_000_000,
                CreatedAt = DateTime.UtcNow },
            new InvoiceItem { Id = serviceId, InvoiceId = invoiceId,
                ItemType = InvoiceItemType.Service, Description = "Dịch vụ",
                Quantity = 1, UnitPrice = 100_000, Amount = 100_000,
                CreatedAt = DateTime.UtcNow }
        ], ownerId, CancellationToken.None);

        await store.ReplaceDraftItemsAsync(invoiceId, ownerId, [
            new EditableInvoiceItem(serviceId, InvoiceItemType.Service, "Dịch vụ kỳ này", 1, 150_000),
            new EditableInvoiceItem(null, InvoiceItemType.Discount, "Giảm phí", 1, 50_000)
        ], CancellationToken.None);
        var draft = await store.GetOwnedDetailAsync(invoiceId, ownerId, CancellationToken.None);
        Assert.Equal(3_100_000, draft!.Invoice.TotalAmount);
        Assert.Contains(draft.Items, item => item.ItemType == InvoiceItemType.Rent && item.Amount == 3_000_000);
        Assert.Contains(draft.Items, item => item.ItemType == InvoiceItemType.Discount && item.Amount == -50_000);

        await store.IssueAsync(invoiceId, ownerId, CancellationToken.None);
        await store.AddAdjustmentAsync(invoiceId, ownerId,
            new InvoiceAdjustmentInput(InvoiceAdjustmentType.AdditionalCharge, 200_000,
                "Phí sửa chữa", Guid.NewGuid().ToString("N")), CancellationToken.None);
        await store.AddAdjustmentAsync(invoiceId, ownerId,
            new InvoiceAdjustmentInput(InvoiceAdjustmentType.Discount, 100_000,
                "Giảm tiền điện", Guid.NewGuid().ToString("N")), CancellationToken.None);

        var issued = await store.GetOwnedDetailAsync(invoiceId, ownerId, CancellationToken.None);
        Assert.Equal(3_200_000, issued!.Balance.EffectiveAmount);
        Assert.Equal(3_200_000, issued.Balance.RemainingAmount);
        Assert.Equal(2, issued.Adjustments.Count);
        var attention = await store.AttentionAsync(propertyId, ownerId, CancellationToken.None);
        Assert.Contains(attention.Unpaid, row => row.Invoice.Id == invoiceId);
        Assert.Null(await store.GetOwnedDetailAsync(invoiceId, "another-owner", CancellationToken.None));

        await new PaymentStore(context).RecordReceivedAsync(invoiceId, ownerId,
            new RecordReceivedPaymentInput(1_000_000, PaymentMethod.Cash, null, null, null, Guid.NewGuid()),
            CancellationToken.None);
        var tooLarge = await Assert.ThrowsAsync<BusinessException>(() => store.AddAdjustmentAsync(invoiceId, ownerId,
            new InvoiceAdjustmentInput(InvoiceAdjustmentType.Discount, 2_200_001,
                "Miễn tiền còn lại", Guid.NewGuid().ToString("N"), true), CancellationToken.None));
        Assert.Equal("ADJUSTMENT_EXCEEDS_AMOUNT_DUE", tooLarge.Code);
        var input = new InvoiceAdjustmentInput(InvoiceAdjustmentType.Discount, 2_200_000,
            "Miễn tiền còn lại", Guid.NewGuid().ToString("N"), true);
        var reduction = await store.AddAdjustmentAsync(invoiceId, ownerId, input, CancellationToken.None);
        var retry = await store.AddAdjustmentAsync(invoiceId, ownerId, input, CancellationToken.None);
        Assert.Equal(reduction.Id, retry.Id);
        var settled = await store.GetOwnedDetailAsync(invoiceId, ownerId, CancellationToken.None);
        Assert.Equal(0, settled!.Balance.RemainingAmount);
        Assert.Equal(1_000_000, settled.Balance.TotalPaid);
    }

    [SqlServerFact]
    public async Task Full_discount_can_reduce_a_draft_to_zero_and_issue_it()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, _, roomId, contractId) = await SeedAsync(database);
        var today = VietnamDate.Today;
        var invoiceId = Guid.NewGuid();
        await using var context = database.CreateContext();
        var store = new InvoiceStore(context);
        var invoice = new Invoice { Id = invoiceId, ContractId = contractId, RoomId = roomId,
            InvoiceCode = $"INV-{invoiceId:N}", InvoiceType = InvoiceType.Monthly,
            BillingYear = (short)today.Year, BillingMonth = (byte)today.Month,
            IssueDate = today, DueDate = today, TotalAmount = 3_000_000,
            Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow };
        await store.CreateDraftAsync(invoice, [new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId,
            ItemType = InvoiceItemType.Rent, Description = "Tiền phòng", Quantity = 1,
            UnitPrice = 3_000_000, Amount = 3_000_000, CreatedAt = DateTime.UtcNow }], ownerId, CancellationToken.None);
        await store.ReplaceDraftItemsAsync(invoiceId, ownerId,
            [new EditableInvoiceItem(null, InvoiceItemType.Discount, "Miễn tiền phòng", 1, 3_000_000)], CancellationToken.None);
        await store.IssueAsync(invoiceId, ownerId, CancellationToken.None);
        var detail = await store.GetOwnedDetailAsync(invoiceId, ownerId, CancellationToken.None);
        Assert.Equal(InvoiceStatus.Issued, detail!.Invoice.Status);
        Assert.Equal(0, detail.Invoice.TotalAmount);
        Assert.Equal(0, detail.Balance.RemainingAmount);
    }

    [SqlServerFact]
    public async Task Meter_import_separates_same_month_contracts_and_uses_the_handover()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, previousId) = await SeedAsync(database);
        var nextId = Guid.NewGuid();
        var period = new DateOnly(2026, 6, 1);
        await using (var seed = database.CreateContext())
        {
            var previous = await seed.Contracts.SingleAsync(x => x.Id == previousId);
            previous.Status = ContractStatus.Terminated;
            previous.HandoverOn = period.AddDays(14);
            previous.ClosedOn = previous.HandoverOn;
            previous.EndDate = previous.HandoverOn;
            seed.Contracts.Add(new Contract { Id = nextId, ContractCode = $"CT-{nextId:N}", RoomId = roomId,
                StartDate = period.AddDays(15), MonthlyRent = previous.MonthlyRent,
                Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow });
            seed.MeterReadings.Add(new MeterReading { Id = Guid.NewGuid(), RoomId = roomId, ContractId = previousId,
                MeterType = UtilityType.Electricity, Year = 2026, Month = 6, PreviousValue = 95m,
                CurrentValue = 150m, RecordedAt = DateTime.UtcNow });
            seed.MeterReadings.Add(new MeterReading { Id = Guid.NewGuid(), RoomId = roomId, ContractId = previousId,
                MeterType = UtilityType.Water, Year = 2026, Month = 6, PreviousValue = 25m,
                CurrentValue = 35m, RecordedAt = DateTime.UtcNow });
            seed.Invoices.Add(new Invoice { Id = Guid.NewGuid(), ContractId = previousId, RoomId = roomId,
                InvoiceCode = $"INV-{Guid.NewGuid():N}", InvoiceType = InvoiceType.Monthly, BillingYear = 2026,
                BillingMonth = 6, IssueDate = period, DueDate = period, Status = InvoiceStatus.Issued,
                TotalAmount = 100m, CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }
        var actor = new OperatorContext(ownerId, ownerId, UserRoles.Landlord, false);
        await using (var context = database.CreateContext())
        {
            var store = ImportStore(context);
            var list = await store.InspectAsync(propertyId, period, null, CancellationToken.None);
            Assert.Single(list.Rows);
            Assert.Contains(list.Rows, x => x.ContractId == nextId && x.Electricity.PreviousValue == 150m);
            Assert.Equal(35m, Assert.Single(list.Rows, x => x.ContractId == nextId).Water.PreviousValue);
            var ambiguous = await store.InspectAsync(propertyId, period, [new(roomId, 200m, null)], CancellationToken.None);
            Assert.Equal("METER_IMPORT_MULTIPLE_CONTRACTS", Assert.Single(ambiguous.Rows).Electricity.Code);
            var input = new MeterImportRowInput(roomId, 200m, null, 150m, ContractId: nextId);
            var saved = await store.SaveAsync(propertyId, period, actor, input, CancellationToken.None);
            Assert.Equal("Saved", saved.Electricity.Status);
            Assert.Equal(50m, saved.Electricity.Consumption);
        }
        await using var verify = database.CreateContext();
        var readings = await verify.MeterReadings.Where(x => x.RoomId == roomId && x.Year == 2026 && x.Month == 6).ToListAsync();
        Assert.Equal(3, readings.Count);
        Assert.Equal(150m, Assert.Single(readings, x => x.ContractId == nextId).PreviousValue);
    }

    [SqlServerFact]
    public async Task Meter_import_hides_handed_over_invoiced_tenancy_but_preserves_history()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (_, propertyId, roomId, contractId) = await SeedAsync(database);
        var period = new DateOnly(2026, 6, 1);
        await using (var seed = database.CreateContext())
        {
            var contract = await seed.Contracts.SingleAsync(x => x.Id == contractId);
            contract.Status = ContractStatus.Terminated;
            contract.HandoverOn = period.AddDays(14);
            contract.ClosedOn = contract.HandoverOn;
            contract.EndDate = contract.HandoverOn;
            seed.MeterReadings.Add(new MeterReading { Id = Guid.NewGuid(), RoomId = roomId, ContractId = contractId,
                MeterType = UtilityType.Water, Year = 2026, Month = 6, PreviousValue = 20, CurrentValue = 35,
                RecordedAt = DateTime.UtcNow });
            seed.Invoices.Add(new Invoice { Id = Guid.NewGuid(), ContractId = contractId, RoomId = roomId,
                InvoiceCode = $"INV-{Guid.NewGuid():N}", InvoiceType = InvoiceType.Monthly, BillingYear = 2026,
                BillingMonth = 6, IssueDate = period, DueDate = period, Status = InvoiceStatus.Issued,
                TotalAmount = 100, CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }
        await using var context = database.CreateContext();
        Assert.Empty((await ImportStore(context).InspectAsync(propertyId, period, null, CancellationToken.None)).Rows);
        Assert.Single(await context.MeterReadings.Where(x => x.ContractId == contractId).ToListAsync());
        var historical = await ImportStore(context).InspectAsync(propertyId, period,
            [new MeterImportRowInput(roomId, null, null, ContractId: contractId)], CancellationToken.None);
        Assert.Equal(35m, Assert.Single(historical.Rows).Water.CurrentValue);
    }

    [SqlServerFact]
    public async Task Meter_import_uses_last_handover_after_vacancy_and_can_save_only_electricity()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, previousContractId) = await SeedAsync(database);
        var newContractId = Guid.NewGuid();
        var handover = new DateOnly(2026, 3, 15);
        var period = new DateOnly(2026, 6, 1);
        await using (var seed = database.CreateContext())
        {
            var previousContract = await seed.Contracts.SingleAsync(x => x.Id == previousContractId);
            previousContract.Status = ContractStatus.Terminated;
            previousContract.EndDate = handover;
            previousContract.ClosedOn = handover;
            previousContract.HandoverOn = handover;
            seed.Contracts.Add(new Contract
            {
                Id = newContractId, ContractCode = $"CT-{newContractId:N}", RoomId = roomId,
                StartDate = period.AddDays(11), MonthlyRent = previousContract.MonthlyRent,
                Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow
            });
            seed.MeterReadings.Add(new MeterReading
            {
                Id = Guid.NewGuid(), RoomId = roomId, ContractId = previousContractId,
                MeterType = UtilityType.Electricity, Year = 2026, Month = 3, PreviousValue = 80m,
                CurrentValue = 100m, RecordedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var actor = new OperatorContext(ownerId, ownerId, UserRoles.Landlord, false);
        var input = new MeterImportRowInput(roomId, 139.25m, null, 100m);
        await using (var storeContext = database.CreateContext())
        {
            var store = ImportStore(storeContext);
            var emptyMonth = await store.InspectAsync(propertyId, period.AddMonths(-1), null, CancellationToken.None);
            Assert.Empty(emptyMonth.Rows);
            var saved = await store.SaveAsync(propertyId, period, actor, input, CancellationToken.None);
            Assert.Equal("Saved", saved.Electricity.Status);
            Assert.Equal(100m, saved.Electricity.PreviousValue);
            Assert.Equal(39.25m, saved.Electricity.Consumption);
            Assert.NotEqual("Saved", saved.Water.Status);
        }

        await using var verify = database.CreateContext();
        var retry = await ImportStore(verify).SaveAsync(propertyId, period, actor, input, CancellationToken.None);
        Assert.Equal("AlreadyRecorded", retry.Electricity.Status);
        var reading = await verify.MeterReadings.SingleAsync(x => x.RoomId == roomId && x.Year == 2026 && x.Month == 6);
        Assert.Equal(newContractId, reading.ContractId);
        Assert.Equal(139.25m, reading.CurrentValue);
        Assert.False(await verify.Invoices.AnyAsync());
    }

    [SqlServerFact]
    public async Task Meter_import_syncs_draft_totals_but_rejects_readings_for_an_issued_invoice()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, contractId) = await SeedAsync(database);
        var invoiceId = Guid.NewGuid();
        var period = new DateOnly(2026, 2, 1);
        await using (var seed = database.CreateContext())
        {
            foreach (var type in new[] { UtilityType.Electricity, UtilityType.Water })
            {
                seed.MeterReadings.Add(new MeterReading
                {
                    Id = Guid.NewGuid(), RoomId = roomId, MeterType = type, Year = 2026, Month = 1,
                    PreviousValue = 90m, CurrentValue = 100m, RecordedAt = DateTime.UtcNow
                });
                seed.UtilityRates.Add(new UtilityRate
                {
                    Id = Guid.NewGuid(), PropertyId = propertyId, UtilityType = type,
                    Unit = type == UtilityType.Electricity ? "kWh" : "m³", UnitPrice = 3_000m,
                    EffectiveFrom = period.AddMonths(-1), CreatedAt = DateTime.UtcNow
                });
            }
            seed.Invoices.Add(new Invoice
            {
                Id = invoiceId, ContractId = contractId, RoomId = roomId,
                InvoiceCode = $"INV-{invoiceId:N}", InvoiceType = InvoiceType.Monthly,
                BillingYear = 2026, BillingMonth = 2, IssueDate = period, DueDate = period.AddDays(4),
                Status = InvoiceStatus.Draft, TotalAmount = 3_020_000m, CreatedAt = DateTime.UtcNow
            });
            seed.InvoiceItems.AddRange(
                new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = InvoiceItemType.Rent,
                    Description = "Tiền phòng", Quantity = 1, UnitPrice = 3_000_000m, Amount = 3_000_000m, CreatedAt = DateTime.UtcNow },
                new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = InvoiceItemType.Other,
                    Description = "Khoản bổ sung", Quantity = 1, UnitPrice = 20_000m, Amount = 20_000m, CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        var actor = new OperatorContext(ownerId, ownerId, UserRoles.Landlord, false);
        await using (var draftContext = database.CreateContext())
        {
            var store = ImportStore(draftContext);
            var result = await store.SaveAsync(propertyId, period, actor,
                new MeterImportRowInput(roomId, 110m, null, 100m), CancellationToken.None);
            Assert.Equal("Saved", result.Electricity.Status);
            var revision = new MeterImportRowInput(roomId, 112m, null, 100m, ExpectedElectricityCurrent: 110m);
            var preview = await store.InspectAsync(propertyId, period, [revision], CancellationToken.None);
            var replacement = Assert.Single(preview.Rows).Electricity;
            Assert.Equal("Ready", replacement.Status);
            Assert.Equal(110m, replacement.RecordedValue);
            var updated = await store.SaveAsync(propertyId, period, actor, revision, CancellationToken.None);
            Assert.Equal("Saved", updated.Electricity.Status);
            Assert.Equal(result.Electricity.ReadingId, updated.Electricity.ReadingId);
            var stale = await store.SaveAsync(propertyId, period, actor, revision with { Electricity = 115m }, CancellationToken.None);
            Assert.Equal("METER_IMPORT_BASELINE_CHANGED", stale.Electricity.Code);
        }
        await using (var verifyDraft = database.CreateContext())
        {
            var invoice = await verifyDraft.Invoices.SingleAsync();
            Assert.Equal(InvoiceStatus.Draft, invoice.Status);
            Assert.Equal(3_056_000m, invoice.TotalAmount);
            var items = await verifyDraft.InvoiceItems.ToListAsync();
            Assert.Equal(20_000m, Assert.Single(items, x => x.ItemType == InvoiceItemType.Other).Amount);
            var electricity = Assert.Single(items, x => x.ItemType == InvoiceItemType.Electricity);
            Assert.Equal(12m, electricity.Quantity);
            Assert.Equal(36_000m, electricity.Amount);
            Assert.NotNull(electricity.ReferenceId);
            invoice.Status = InvoiceStatus.Issued;
            await verifyDraft.SaveChangesAsync();
        }
        await using var verifyIssued = database.CreateContext();
        var blocked = await ImportStore(verifyIssued).SaveAsync(propertyId, period, actor,
            new MeterImportRowInput(roomId, 116m, 112m, 100m, 100m, ExpectedElectricityCurrent: 112m), CancellationToken.None);
        Assert.Equal("METER_IMPORT_INVOICE_LOCKED", blocked.Electricity.Code);
        Assert.NotEqual("Saved", blocked.Water.Status);
        Assert.False(await verifyIssued.MeterReadings.AnyAsync(x => x.Year == 2026 && x.Month == 2 && x.MeterType == UtilityType.Water));
        Assert.Equal(112m, await verifyIssued.MeterReadings.Where(x => x.Year == 2026 && x.Month == 2 && x.MeterType == UtilityType.Electricity)
            .Select(x => x.CurrentValue).SingleAsync());
        Assert.Equal(3_056_000m, await verifyIssued.Invoices.Select(x => x.TotalAmount).SingleAsync());
    }

    [SqlServerFact]
    public async Task Meter_import_accepts_manual_opening_once_and_does_not_allow_bypassing_a_later_gap()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var (ownerId, propertyId, roomId, _) = await SeedAsync(database);
        await using var context = database.CreateContext();
        var store = ImportStore(context);
        var actor = new OperatorContext(ownerId, ownerId, UserRoles.Landlord, false);
        var period = new DateOnly(2026, 2, 1);
        var input = new MeterImportRowInput(roomId, 10m, null, 0m,
            ElectricityManual: true, InitialElectricityPrevious: 0m);
        var initial = await store.InspectAsync(propertyId, period, null, CancellationToken.None);
        Assert.True(Assert.Single(initial.Rows).Electricity.CanEnterOpening);
        var saved = await store.SaveAsync(propertyId, period, actor, input, CancellationToken.None);
        Assert.Equal("Saved", saved.Electricity.Status);
        Assert.Equal(0m, saved.Electricity.PreviousValue);
        Assert.False(saved.Electricity.CanEnterOpening);
        var gap = await store.SaveAsync(propertyId, period.AddMonths(2), actor, input, CancellationToken.None);
        Assert.Equal("METER_IMPORT_INITIAL_NOT_ALLOWED", gap.Electricity.Code);
        Assert.True(gap.Water.CanEnterOpening);
        Assert.Single(await context.MeterReadings.ToListAsync());
    }

    private static MeterImportStore ImportStore(RoomManagementDbContext context) =>
        new(context, new LandlordStore(context), NullLogger<MeterImportStore>.Instance);

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
