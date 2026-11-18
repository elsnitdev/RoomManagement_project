using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Data;
using RoomManagement.Domain.Entities;

namespace RoomManagement.API.Infrastructure;

public sealed class NotificationEmailWorker(
    IServiceScopeFactory scopes, IHttpClientFactory clients, IConfiguration configuration,
    ILogger<NotificationEmailWorker> logger) : BackgroundService
{
    private static readonly string[] EmailTypes =
    [
        "InvoiceIssued", "PaymentConfirmed", "ContractOfferSent", "ContractActivated",
        "ContractCancelled", "ContractTerminated", "ContractMoveOut", "ContractSettled"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(configuration["SendGrid:ApiKey"]) &&
                    !string.IsNullOrWhiteSpace(configuration["SendGrid:FromEmail"]))
                    await ProcessBatch(stoppingToken);
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                // Service resolution or disposal can fail during shutdown without throwing
                // OperationCanceledException. Do not report it as a failed background service.
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification email batch failed");
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task ProcessBatch(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
        var now = DateTime.UtcNow;
        // SendGrid Mail Send has no request idempotency key. An interrupted send may
        // already have been accepted, so an expired claim needs manual reconciliation.
        await db.UserNotifications.Where(n => n.EmailStatus == "Processing" &&
                n.EmailNextAttemptAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailStatus, "Unknown")
                .SetProperty(n => n.EmailError, "InterruptedSend")
                .SetProperty(n => n.EmailNextAttemptAt, (DateTime?)null), ct);
        var ids = await db.UserNotifications.AsNoTracking()
            .Where(n => EmailTypes.Contains(n.Type) &&
                (n.EmailStatus == null || n.EmailStatus == "Pending") &&
                (n.EmailNextAttemptAt == null || n.EmailNextAttemptAt <= now))
            .OrderBy(n => n.CreatedAt).Select(n => n.Id).Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            var claimed = await db.UserNotifications.Where(n => n.Id == id &&
                    (n.EmailStatus == null || n.EmailStatus == "Pending") &&
                    (n.EmailNextAttemptAt == null || n.EmailNextAttemptAt <= now))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailStatus, "Processing")
                    .SetProperty(n => n.EmailAttempts, n => n.EmailAttempts + 1)
                    .SetProperty(n => n.EmailNextAttemptAt, DateTime.UtcNow.AddMinutes(5)), ct);
            if (claimed == 0) continue;
            var notification = await db.UserNotifications.AsNoTracking().SingleAsync(n => n.Id == id, ct);
            var recipient = await db.Users.AsNoTracking()
                .Where(u => u.Id == notification.UserId)
                .Select(u => new { u.Email, Name = u.FullName ?? u.UserName })
                .SingleOrDefaultAsync(ct);
            var email = notification.EmailAddress ?? recipient?.Email;
            if (string.IsNullOrWhiteSpace(email))
            {
                await db.UserNotifications.Where(n => n.Id == id).ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.EmailStatus, "MissingEmail"), ct);
                continue;
            }
            try
            {
                var context = await BuildEmailContextAsync(db, notification, recipient?.Name, configuration["App:FrontendBaseUrl"], ct);
                var htmlBody = NotificationEmailRenderer.BuildHtml(context);

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["SendGrid:ApiKey"]);
                request.Content = JsonContent.Create(new
                {
                    personalizations = new[] { new
                    {
                        to = new[] { new { email } },
                        custom_args = new { notification_id = id.ToString("N") }
                    } },
                    from = new
                    {
                        email = configuration["SendGrid:FromEmail"],
                        name = configuration["SendGrid:FromName"] ?? "Nhà Trọ Xanh"
                    },
                    subject = notification.Title,
                    content = new[]
                    {
                        new { type = "text/plain", value = notification.Message },
                        new { type = "text/html", value = htmlBody }
                    }
                });
                using var response = await clients.CreateClient("NotificationEmail").SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    await db.UserNotifications.Where(n => n.Id == id).ExecuteUpdateAsync(s => s
                        .SetProperty(n => n.EmailStatus, "Accepted")
                        .SetProperty(n => n.EmailAddress, email)
                        .SetProperty(n => n.EmailSentAt, DateTime.UtcNow)
                        .SetProperty(n => n.EmailNextAttemptAt, (DateTime?)null)
                        .SetProperty(n => n.EmailError, (string?)null), ct);
                }
                else
                {
                    var status = response.StatusCode == HttpStatusCode.TooManyRequests ? "Pending" :
                        (int)response.StatusCode >= 500 ? "Unknown" : "Failed";
                    await Fail(db, id, email, notification.EmailAttempts, status,
                        $"HTTP {(int)response.StatusCode}", ct);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                await Fail(db, id, email, notification.EmailAttempts, "Unknown", ex.GetType().Name, ct);
            }
        }
    }

    public static async Task<NotificationEmailContext> BuildEmailContextAsync(
        RoomManagementDbContext db,
        UserNotification notification,
        string? recipientName,
        string? frontendBaseUrl,
        CancellationToken ct)
    {
        InvoiceSummaryInfo? invoiceInfo = null;
        if (notification.InvoiceId is { } invoiceId)
        {
            invoiceInfo = await (from inv in db.Invoices.AsNoTracking()
                                 join bal in db.InvoiceBalances.AsNoTracking() on inv.Id equals bal.InvoiceId into balances
                                 from bal in balances.DefaultIfEmpty()
                                 where inv.Id == invoiceId
                                 select new InvoiceSummaryInfo(
                                     inv.InvoiceCode,
                                     inv.BillingMonth,
                                     inv.BillingYear,
                                     inv.TotalAmount,
                                     inv.DueDate,
                                     inv.RoomId,
                                     inv.ContractId,
                                     bal != null ? bal.RemainingAmount : (decimal?)null))
                .SingleOrDefaultAsync(ct);
        }

        var contractId = notification.ContractId ?? invoiceInfo?.ContractId;
        ContractSummaryInfo? contractInfo = null;
        if (contractId is { } cid)
        {
            contractInfo = await db.Contracts.AsNoTracking()
                .Where(c => c.Id == cid)
                .Select(c => new ContractSummaryInfo(
                    c.ContractCode,
                    c.OfferVersion,
                    c.StartDate,
                    c.EndDate,
                    c.ClosedOn,
                    c.HandoverOn,
                    c.MonthlyRent,
                    c.DepositAmount,
                    c.RoomId))
                .SingleOrDefaultAsync(ct);
        }

        var roomId = invoiceInfo?.RoomId ?? contractInfo?.RoomId;
        RoomSummaryInfo? roomInfo = null;
        if (roomId is { } rid)
        {
            roomInfo = await db.Rooms.AsNoTracking()
                .Where(r => r.Id == rid)
                .Select(r => new RoomSummaryInfo(r.RoomNumber, r.PropertyId))
                .SingleOrDefaultAsync(ct);
        }

        var propertyId = notification.PropertyId ?? roomInfo?.PropertyId;
        PropertySummaryInfo? propertyInfo = null;
        PropertyContactInfo? contactInfo = null;
        if (propertyId is { } pid)
        {
            propertyInfo = await db.Properties.AsNoTracking()
                .Where(p => p.Id == pid)
                .Select(p => new PropertySummaryInfo(p.Id, p.Name, p.Address, p.OwnerUserId))
                .SingleOrDefaultAsync(ct);

            if (propertyInfo is not null)
                contactInfo = await ResolvePropertyContactAsync(db, propertyInfo.Id, propertyInfo.OwnerUserId, ct);
        }

        return new NotificationEmailContext(
            notification.Type,
            notification.Title,
            notification.Message,
            string.IsNullOrWhiteSpace(recipientName) ? "Quý khách" : recipientName,
            roomInfo?.RoomNumber,
            propertyInfo,
            contactInfo,
            invoiceInfo,
            contractInfo,
            frontendBaseUrl);
    }

    public static async Task<PropertyContactInfo?> ResolvePropertyContactAsync(
        RoomManagementDbContext db,
        Guid propertyId,
        string ownerUserId,
        CancellationToken ct)
    {
        var manager = await (from assignment in db.PropertyManagerAssignments.AsNoTracking()
                             join user in db.Users.AsNoTracking() on assignment.ManagerUserId equals user.Id
                             where assignment.PropertyId == propertyId
                                   && assignment.RevokedAt == null
                                   && user.IsActive
                                   && user.DeletedAt == null
                                   && !string.IsNullOrWhiteSpace(user.Email)
                             orderby assignment.AssignedAt descending
                             select new PropertyContactInfo(
                                 "Quản lý khu trọ",
                                 string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName,
                                 user.Email!,
                                 user.PhoneNumber))
            .FirstOrDefaultAsync(ct);

        if (manager is not null)
            return manager;

        return await db.Users.AsNoTracking()
            .Where(u => u.Id == ownerUserId && u.IsActive && u.DeletedAt == null)
            .Select(u => new PropertyContactInfo(
                "Chủ trọ",
                string.IsNullOrWhiteSpace(u.FullName) ? u.UserName : u.FullName,
                u.Email ?? string.Empty,
                u.PhoneNumber))
            .SingleOrDefaultAsync(ct);
    }

    private static Task<int> Fail(RoomManagementDbContext db, Guid id, string email, int attempts,
        string status, string error, CancellationToken ct) => db.UserNotifications.Where(n => n.Id == id)
        .ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailStatus,
                status == "Pending" && attempts >= 5 ? "Failed" : status)
            .SetProperty(n => n.EmailAddress, email)
            .SetProperty(n => n.EmailError, error)
            .SetProperty(n => n.EmailNextAttemptAt,
                status == "Pending" && attempts < 5 ? DateTime.UtcNow.AddMinutes(Math.Pow(2, attempts)) : (DateTime?)null), ct);
}

public sealed record InvoiceSummaryInfo(
    string InvoiceCode,
    byte? BillingMonth,
    short? BillingYear,
    decimal TotalAmount,
    DateOnly DueDate,
    Guid RoomId,
    Guid ContractId,
    decimal? RemainingAmount);

public sealed record ContractSummaryInfo(
    string ContractCode,
    int OfferVersion,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? ClosedOn,
    DateOnly? HandoverOn,
    decimal MonthlyRent,
    decimal DepositAmount,
    Guid RoomId);

public sealed record PropertySummaryInfo(
    Guid Id,
    string Name,
    string Address,
    string OwnerUserId);

public sealed record RoomSummaryInfo(
    string RoomNumber,
    Guid PropertyId);

public sealed record PropertyContactInfo(
    string RoleLabel,
    string Name,
    string Email,
    string? PhoneNumber);

public sealed record NotificationEmailContext(
    string Type,
    string Title,
    string Message,
    string RecipientName,
    string? RoomNumber,
    PropertySummaryInfo? Property,
    PropertyContactInfo? Contact,
    InvoiceSummaryInfo? Invoice,
    ContractSummaryInfo? Contract,
    string? FrontendBaseUrl);

public static class NotificationEmailRenderer
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    private sealed record SummaryRow(string Label, string Value, string? Tone = null);

    public static string BuildHtml(NotificationEmailContext ctx)
    {
        var (badgeText, introText, ctaLabel, ctaHash) = GetTypeMetadata(ctx.Type);
        var ctaUrl = BuildCtaUrl(ctx.FrontendBaseUrl, ctaHash);
        var rows = BuildSummaryRows(ctx);

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(ctx.Property?.Name))
            subtitleParts.Add(ctx.Property.Name);
        if (!string.IsNullOrWhiteSpace(ctx.RoomNumber))
            subtitleParts.Add($"Phòng {ctx.RoomNumber}");
        var subtitle = subtitleParts.Count > 0 ? string.Join(" · ", subtitleParts) : "Hệ thống quản lý phòng trọ";

        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>
            """);
        sb.Append(Enc(ctx.Title));
        sb.Append("""
              </title>
            </head>
            <body style="margin:0;padding:0;background-color:#f5f8fb;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Arial,sans-serif;color:#243b53;">
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background-color:#f5f8fb;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="width:100%;max-width:600px;background-color:#ffffff;border:1px solid #dae5ef;border-radius:10px;overflow:hidden;">
            """);

        // 1. Header
        sb.Append($"""
                      <tr>
                        <td style="border-top:3px solid #2167aa;background-color:#eff6fd;border-bottom:1px solid #d5e3f1;padding:16px 24px;">
                          <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%">
                            <tr>
                              <td valign="middle" style="text-align:left;">
                                <div style="font-size:15px;font-weight:700;color:#243b53;line-height:1.3;">Nhà Trọ Xanh</div>
                                <div style="font-size:12px;color:#6c7f91;line-height:1.4;margin-top:2px;">{Enc(subtitle)}</div>
                              </td>
                              <td valign="middle" style="text-align:right;">
                                <span style="display:inline-block;padding:4px 10px;border-radius:6px;background-color:#ffffff;border:1px solid #b9d6ef;color:#1b609f;font-size:12px;font-weight:600;white-space:nowrap;">{Enc(badgeText)}</span>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
            """);

        // 2. Greeting & Intro
        sb.Append($"""
                      <tr>
                        <td style="padding:24px 24px 18px 24px;">
                          <p style="margin:0 0 10px 0;font-size:14px;line-height:1.55;color:#243b53;">
                            Xin chào <strong>{Enc(ctx.RecipientName)}</strong>,
                          </p>
                          <h2 style="margin:0 0 8px 0;font-size:16.5px;line-height:1.35;font-weight:700;color:#243b53;">
                            {Enc(ctx.Title)}
                          </h2>
                          <p style="margin:0 0 10px 0;font-size:13.5px;line-height:1.55;color:#243b53;">
                            {Enc(introText)}
                          </p>
                          <p style="margin:0;font-size:13.5px;line-height:1.55;color:#6c7f91;">
                            {Enc(ctx.Message)}
                          </p>
                        </td>
                      </tr>
            """);

        // 3. Summary Table
        if (rows.Count > 0)
        {
            sb.Append("""
                      <tr>
                        <td style="padding:0 24px 20px 24px;">
                          <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background-color:#f8fafc;border:1px solid #dae5ef;border-radius:8px;border-collapse:separate;">
            """);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var borderBottom = i < rows.Count - 1 ? "border-bottom:1px solid #e6edf4;" : string.Empty;
                var valueHtml = RenderRowValue(row);
                sb.Append($"""
                            <tr>
                              <td style="padding:10px 14px;{borderBottom}font-size:13px;color:#6c7f91;width:40%;vertical-align:top;">{Enc(row.Label)}</td>
                              <td style="padding:10px 14px;{borderBottom}font-size:13.5px;color:#243b53;text-align:right;vertical-align:top;">{valueHtml}</td>
                            </tr>
                """);
            }
            sb.Append("""
                          </table>
                        </td>
                      </tr>
            """);
        }

        // 4. CTA Button
        sb.Append($"""
                      <tr>
                        <td style="padding:0 24px 24px 24px;" align="left">
                          <a href="{Enc(ctaUrl)}" style="display:inline-block;background-color:#2167aa;color:#ffffff;font-size:13.5px;font-weight:600;line-height:1.2;text-decoration:none;padding:11px 20px;border-radius:8px;">
                            {Enc(ctaLabel)}
                          </a>
                        </td>
                      </tr>
            """);

        // 5. Contact Block & Footer
        if (ctx.Property is not null || ctx.Contact is not null)
        {
            sb.Append("""
                      <tr>
                        <td style="padding:0 24px 20px 24px;">
                          <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background-color:#eff6fd;border:1px solid #d5e3f1;border-radius:8px;">
                            <tr>
                              <td style="padding:14px 16px;font-size:13px;line-height:1.55;color:#243b53;">
                                <div style="font-weight:700;color:#1b609f;margin-bottom:4px;">Thông tin liên hệ khu trọ</div>
            """);

            if (ctx.Property is not null)
            {
                sb.Append($"<div><strong>{Enc(ctx.Property.Name)}</strong></div>");
                if (!string.IsNullOrWhiteSpace(ctx.Property.Address))
                    sb.Append($"<div style=\"color:#6c7f91;\">Địa chỉ: {Enc(ctx.Property.Address)}</div>");
            }

            if (ctx.Contact is not null)
            {
                sb.Append($"<div style=\"margin-top:6px;\">{Enc(ctx.Contact.RoleLabel)}: <strong>{Enc(ctx.Contact.Name)}</strong></div>");
                if (!string.IsNullOrWhiteSpace(ctx.Contact.Email))
                    sb.Append($"<div>Email: <a href=\"mailto:{Enc(ctx.Contact.Email)}\" style=\"color:#2167aa;text-decoration:none;\">{Enc(ctx.Contact.Email)}</a></div>");
                if (!string.IsNullOrWhiteSpace(ctx.Contact.PhoneNumber))
                    sb.Append($"<div>SĐT: <a href=\"tel:{Enc(ctx.Contact.PhoneNumber)}\" style=\"color:#2167aa;text-decoration:none;\">{Enc(ctx.Contact.PhoneNumber)}</a></div>");
            }

            sb.Append("""
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
            """);
        }

        sb.Append("""
                      <tr>
                        <td style="padding:14px 24px;border-top:1px solid #e6edf4;background-color:#fafcfe;font-size:12px;line-height:1.5;color:#6c7f91;">
                          Đây là email thông báo tự động từ hệ thống Nhà Trọ Xanh. Vui lòng không trả lời trực tiếp email này; nếu cần hỗ trợ, hãy liên hệ người phụ trách khu trọ ở trên.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static string RenderRowValue(SummaryRow row) => row.Tone switch
    {
        "amount" => $"<span style=\"font-size:15.5px;font-weight:700;color:#2167aa;\">{Enc(row.Value)}</span>",
        "success" => $"<span style=\"display:inline-block;padding:2px 9px;border-radius:999px;background-color:#eaf8f1;color:#176a49;font-size:12.5px;font-weight:600;\">{Enc(row.Value)}</span>",
        "due" => $"<span style=\"display:inline-block;padding:2px 9px;border-radius:999px;background-color:#fff8eb;color:#855012;font-size:12.5px;font-weight:600;\">{Enc(row.Value)}</span>",
        _ => $"<strong>{Enc(row.Value)}</strong>"
    };

    private static List<SummaryRow> BuildSummaryRows(NotificationEmailContext ctx)
    {
        var rows = new List<SummaryRow>();

        switch (ctx.Type)
        {
            case "InvoiceIssued":
                if (ctx.Invoice is { } inv)
                {
                    rows.Add(new SummaryRow("Mã hóa đơn", inv.InvoiceCode));
                    if (!string.IsNullOrWhiteSpace(ctx.RoomNumber))
                        rows.Add(new SummaryRow("Phòng", ctx.RoomNumber));
                    if (inv.BillingMonth is { } m && inv.BillingYear is { } y)
                        rows.Add(new SummaryRow("Kỳ hóa đơn", $"Tháng {m}/{y}"));
                    rows.Add(new SummaryRow("Số tiền", FormatMoney(inv.TotalAmount), "amount"));
                    rows.Add(new SummaryRow("Hạn thanh toán", inv.DueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), "due"));
                }
                break;

            case "PaymentConfirmed":
                if (ctx.Invoice is { } paidInv)
                {
                    rows.Add(new SummaryRow("Mã hóa đơn", paidInv.InvoiceCode));
                    if (!string.IsNullOrWhiteSpace(ctx.RoomNumber))
                        rows.Add(new SummaryRow("Phòng", ctx.RoomNumber));
                    rows.Add(new SummaryRow("Tổng hóa đơn", FormatMoney(paidInv.TotalAmount), "amount"));
                    if (paidInv.RemainingAmount is { } remaining)
                    {
                        if (remaining <= 0)
                            rows.Add(new SummaryRow("Trạng thái", "Đã thanh toán đủ", "success"));
                        else
                            rows.Add(new SummaryRow("Số dư còn lại", FormatMoney(remaining), "due"));
                    }
                }
                break;

            case "ContractOfferSent":
            case "ContractActivated":
            case "ContractCancelled":
            case "ContractTerminated":
            case "ContractMoveOut":
            case "ContractSettled":
                if (ctx.Contract is { } c)
                {
                    var codeValue = ctx.Type == "ContractOfferSent"
                        ? $"{c.ContractCode} (phiên bản {c.OfferVersion})"
                        : c.ContractCode;
                    rows.Add(new SummaryRow("Mã hợp đồng", codeValue));
                    if (!string.IsNullOrWhiteSpace(ctx.RoomNumber))
                        rows.Add(new SummaryRow("Phòng", ctx.RoomNumber));

                    var term = c.EndDate is { } endDate
                        ? $"{c.StartDate:dd/MM/yyyy} – {endDate:dd/MM/yyyy}"
                        : $"Từ {c.StartDate:dd/MM/yyyy}";
                    rows.Add(new SummaryRow("Thời hạn thuê", term));

                    if (ctx.Type is "ContractOfferSent" or "ContractActivated")
                    {
                        rows.Add(new SummaryRow("Giá thuê / tháng", FormatMoney(c.MonthlyRent), "amount"));
                        rows.Add(new SummaryRow("Tiền đặt cọc", FormatMoney(c.DepositAmount)));
                    }

                    if (ctx.Type == "ContractTerminated" && (c.ClosedOn ?? c.EndDate) is { } termDate)
                        rows.Add(new SummaryRow("Ngày kết thúc", termDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));

                    if (ctx.Type == "ContractMoveOut" && (c.HandoverOn ?? c.ClosedOn) is { } moveDate)
                        rows.Add(new SummaryRow("Ngày rời phòng", moveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));

                    var statusRow = ctx.Type switch
                    {
                        "ContractOfferSent" => new SummaryRow("Trạng thái", "Chờ bạn xác nhận", "due"),
                        "ContractActivated" => new SummaryRow("Trạng thái", "Đang hiệu lực", "success"),
                        "ContractCancelled" => new SummaryRow("Trạng thái", "Đã hủy"),
                        "ContractTerminated" => new SummaryRow("Trạng thái", "Đã chấm dứt"),
                        "ContractMoveOut" => new SummaryRow("Trạng thái", "Đã ghi nhận rời phòng"),
                        "ContractSettled" => new SummaryRow("Trạng thái", "Đã quyết toán hoàn tất", "success"),
                        _ => null
                    };
                    if (statusRow is not null)
                        rows.Add(statusRow);
                }
                break;
        }

        return rows;
    }

    private static (string Badge, string Intro, string CtaLabel, string CtaHash) GetTypeMetadata(string type) => type switch
    {
        "InvoiceIssued" => (
            "Hóa đơn mới",
            "Hóa đơn tiền phòng kỳ mới của bạn đã được phát hành. Vui lòng kiểm tra thông tin bên dưới và thanh toán đúng hạn.",
            "Xem & thanh toán hóa đơn",
            "#/tenant/billing"),
        "PaymentConfirmed" => (
            "Xác nhận thanh toán",
            "Khoản thanh toán của bạn đã được ghi nhận trên hệ thống. Bạn có thể xem lại lịch sử giao dịch trong mục Hóa đơn.",
            "Xem chi tiết hóa đơn",
            "#/tenant/billing"),
        "ContractOfferSent" => (
            "Đề nghị hợp đồng",
            "Bạn vừa nhận được đề nghị hợp đồng thuê phòng mới. Vui lòng xem kỹ các điều khoản tóm tắt dưới đây và xác nhận trên hệ thống.",
            "Xem & xác nhận hợp đồng",
            "#/tenant/contracts"),
        "ContractActivated" => (
            "Hợp đồng hiệu lực",
            "Hợp đồng thuê phòng của bạn đã chính thức được kích hoạt. Chúc bạn có thời gian lưu trú thoải mái.",
            "Xem hợp đồng của tôi",
            "#/tenant/contracts"),
        "ContractCancelled" => (
            "Hợp đồng đã hủy",
            "Đề nghị hoặc hợp đồng thuê phòng dưới đây đã được hủy trên hệ thống.",
            "Xem thông tin hợp đồng",
            "#/tenant/contracts"),
        "ContractTerminated" => (
            "Chấm dứt hợp đồng",
            "Hợp đồng thuê phòng của bạn đã được ghi nhận chấm dứt. Vui lòng kiểm tra trạng thái bàn giao và quyết toán.",
            "Xem chi tiết hợp đồng",
            "#/tenant/contracts"),
        "ContractMoveOut" => (
            "Ghi nhận rời phòng",
            "Thông tin bàn giao và rời phòng của bạn đã được cập nhật trên hệ thống.",
            "Theo dõi quyết toán",
            "#/tenant/contracts"),
        "ContractSettled" => (
            "Hoàn tất quyết toán",
            "Hợp đồng thuê phòng của bạn đã hoàn tất quyết toán. Cảm ơn bạn đã đồng hành cùng Nhà Trọ Xanh.",
            "Xem hồ sơ hợp đồng",
            "#/tenant/contracts"),
        _ => (
            "Thông báo",
            "Bạn có một thông báo mới từ hệ thống Nhà Trọ Xanh.",
            "Mở ứng dụng",
            "#/tenant/notifications")
    };

    private static string BuildCtaUrl(string? baseUrl, string hashPath)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return hashPath;
        return $"{baseUrl.TrimEnd('/')}/{hashPath}";
    }

    private static string FormatMoney(decimal amount) => $"{amount.ToString("N0", ViCulture)} đ";

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
