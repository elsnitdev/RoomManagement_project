using System.Net;
using RoomManagement.API.Infrastructure;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

public sealed class NotificationEmailTests
{
    [SqlServerFact]
    public async Task ResolvePropertyContactAsync_prefers_active_manager_with_email_over_owner()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var propertyId = Guid.NewGuid();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var managerId = $"manager-{Guid.NewGuid():N}";

        await using (var seed = database.CreateContext())
        {
            seed.Users.AddRange(
                new AppUser
                {
                    Id = ownerId,
                    UserName = ownerId,
                    NormalizedUserName = ownerId.ToUpperInvariant(),
                    FullName = "Chủ Trọ A",
                    Email = "owner@example.com",
                    PhoneNumber = "0901000111",
                    PasswordHash = "hash",
                    Role = UserRoles.Landlord,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new AppUser
                {
                    Id = managerId,
                    UserName = managerId,
                    NormalizedUserName = managerId.ToUpperInvariant(),
                    FullName = "Quản Lý B",
                    Email = "manager@example.com",
                    PhoneNumber = "0902000222",
                    PasswordHash = "hash",
                    Role = UserRoles.Manager,
                    LandlordUserId = ownerId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            seed.Properties.Add(new Property
            {
                Id = propertyId,
                OwnerUserId = ownerId,
                Name = "Nhà Trọ Xanh Q7",
                Address = "123 Nguyễn Thị Thập, Q7",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            seed.PropertyManagerAssignments.Add(new PropertyManagerAssignment
            {
                Id = Guid.NewGuid(),
                PropertyId = propertyId,
                ManagerUserId = managerId,
                AssignedByUserId = ownerId,
                AssignedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateContext();
        var contact = await NotificationEmailWorker.ResolvePropertyContactAsync(db, propertyId, ownerId, CancellationToken.None);

        Assert.NotNull(contact);
        Assert.Equal("Quản lý khu trọ", contact.RoleLabel);
        Assert.Equal("Quản Lý B", contact.Name);
        Assert.Equal("manager@example.com", contact.Email);
        Assert.Equal("0902000222", contact.PhoneNumber);
    }

    [SqlServerFact]
    public async Task ResolvePropertyContactAsync_falls_back_to_owner_when_no_active_manager_with_email()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var propertyId = Guid.NewGuid();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var revokedManagerId = $"manager-revoked-{Guid.NewGuid():N}";

        await using (var seed = database.CreateContext())
        {
            seed.Users.AddRange(
                new AppUser
                {
                    Id = ownerId,
                    UserName = ownerId,
                    NormalizedUserName = ownerId.ToUpperInvariant(),
                    FullName = "Chủ Trọ Chính",
                    Email = "owner-main@example.com",
                    PhoneNumber = "0909888777",
                    PasswordHash = "hash",
                    Role = UserRoles.Landlord,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new AppUser
                {
                    Id = revokedManagerId,
                    UserName = revokedManagerId,
                    NormalizedUserName = revokedManagerId.ToUpperInvariant(),
                    FullName = "Quản Lý Cũ",
                    Email = "old-manager@example.com",
                    PasswordHash = "hash",
                    Role = UserRoles.Manager,
                    LandlordUserId = ownerId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            seed.Properties.Add(new Property
            {
                Id = propertyId,
                OwnerUserId = ownerId,
                Name = "Nhà Trọ Xanh Bình Thạnh",
                Address = "456 Điện Biên Phủ, Bình Thạnh",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            seed.PropertyManagerAssignments.Add(new PropertyManagerAssignment
            {
                Id = Guid.NewGuid(),
                PropertyId = propertyId,
                ManagerUserId = revokedManagerId,
                AssignedByUserId = ownerId,
                AssignedAt = DateTime.UtcNow.AddDays(-10),
                RevokedAt = DateTime.UtcNow.AddDays(-1),
                RevokedByUserId = ownerId
            });
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateContext();
        var contact = await NotificationEmailWorker.ResolvePropertyContactAsync(db, propertyId, ownerId, CancellationToken.None);

        Assert.NotNull(contact);
        Assert.Equal("Chủ trọ", contact.RoleLabel);
        Assert.Equal("Chủ Trọ Chính", contact.Name);
        Assert.Equal("owner-main@example.com", contact.Email);
        Assert.Equal("0909888777", contact.PhoneNumber);
    }

    [Fact]
    public void BuildHtml_renders_invoice_issued_with_contact_and_html_encoding()
    {
        var context = new NotificationEmailContext(
            Type: "InvoiceIssued",
            Title: "Hóa đơn INV-2026-001 - Phòng 101 đã phát hành",
            Message: "Hóa đơn tháng 11/2026 cho <script>alert(1)</script>.",
            RecipientName: "Nguyễn Văn <An>",
            RoomNumber: "101",
            Property: new PropertySummaryInfo(Guid.NewGuid(), "Khu Trọ Xanh & Sạch", "12 Nguyễn Huệ", "owner-1"),
            Contact: new PropertyContactInfo("Quản lý khu trọ", "Trần Bình", "ql@nhatroxanh.vn", "0901234567"),
            Invoice: new InvoiceSummaryInfo("INV-2026-001", 11, 2026, 3_500_000m, new DateOnly(2026, 11, 10), Guid.NewGuid(), Guid.NewGuid(), 3_500_000m),
            Contract: null,
            FrontendBaseUrl: "https://app.nhatroxanh.vn/");

        var html = NotificationEmailRenderer.BuildHtml(context);
        var decoded = WebUtility.HtmlDecode(html);

        Assert.Contains("Nhà Trọ Xanh", decoded);
        Assert.Contains("INV-2026-001", decoded);
        Assert.Contains("Tháng 11/2026", decoded);
        Assert.Contains("10/11/2026", decoded);
        Assert.Contains("https://app.nhatroxanh.vn/#/tenant/billing", html);
        Assert.Contains("Quản lý khu trọ", decoded);
        Assert.Contains("mailto:ql@nhatroxanh.vn", html);
        Assert.Contains("tel:0901234567", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("Nguyễn Văn &lt;An&gt;", html);
        Assert.Contains("Khu Trọ Xanh &amp; Sạch", html);
    }

    [Fact]
    public void BuildHtml_renders_payment_confirmed_paid_in_full_and_contract_offer()
    {
        var paymentCtx = new NotificationEmailContext(
            Type: "PaymentConfirmed",
            Title: "Đã xác nhận thanh toán INV-2026-002",
            Message: "Đã xác nhận thanh toán.",
            RecipientName: "Lê Thị Hoa",
            RoomNumber: "202",
            Property: new PropertySummaryInfo(Guid.NewGuid(), "Nhà Trọ Xanh", "10 Lê Lợi", "owner-1"),
            Contact: new PropertyContactInfo("Chủ trọ", "Phạm Minh", "chutro@nhatroxanh.vn", null),
            Invoice: new InvoiceSummaryInfo("INV-2026-002", 11, 2026, 2_800_000m, new DateOnly(2026, 11, 10), Guid.NewGuid(), Guid.NewGuid(), 0m),
            Contract: null,
            FrontendBaseUrl: null);

        var paymentHtml = NotificationEmailRenderer.BuildHtml(paymentCtx);
        var paymentDecoded = WebUtility.HtmlDecode(paymentHtml);
        Assert.Contains("Đã thanh toán đủ", paymentDecoded);
        Assert.Contains("Chủ trọ", paymentDecoded);
        Assert.Contains("href=\"#/tenant/billing\"", paymentHtml);

        var contractCtx = new NotificationEmailContext(
            Type: "ContractOfferSent",
            Title: "Đề nghị hợp đồng HD-009",
            Message: "Mời bạn xác nhận hợp đồng.",
            RecipientName: "Lê Thị Hoa",
            RoomNumber: "202",
            Property: null,
            Contact: null,
            Invoice: null,
            Contract: new ContractSummaryInfo("HD-009", 2, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), null, null, 3_200_000m, 3_200_000m, Guid.NewGuid()),
            FrontendBaseUrl: "https://app.nhatroxanh.vn");

        var contractHtml = NotificationEmailRenderer.BuildHtml(contractCtx);
        var contractDecoded = WebUtility.HtmlDecode(contractHtml);
        Assert.Contains("HD-009 (phiên bản 2)", contractDecoded);
        Assert.Contains("01/12/2026 – 30/11/2027", contractDecoded);
        Assert.Contains("Chờ bạn xác nhận", contractDecoded);
        Assert.Contains("https://app.nhatroxanh.vn/#/tenant/contracts", contractHtml);
    }
}
