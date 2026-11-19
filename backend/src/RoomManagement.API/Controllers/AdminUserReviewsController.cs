using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Validators;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin/user-reviews")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminUserReviewsController(RoomManagementDbContext db) : ControllerBase
{
    private IQueryable<AppUser> Eligible => db.Users.Where(u => u.DeletedAt == null &&
        (u.Role == UserRoles.Landlord || u.Role == UserRoles.Tenant));

    [HttpGet]
    public async Task<IActionResult> List(string kind = "approval", string? status = null,
        string? keyword = null, int page = 1, CancellationToken cancellationToken = default)
    {
        if (kind is not ("approval" or "identity")) return BadRequest();
        bool approval = kind == "approval";
        var query = Eligible.AsNoTracking();
        if (approval) query = query.Where(u => u.Role == UserRoles.Landlord);
        if (!string.IsNullOrWhiteSpace(status))
            query = approval ? query.Where(u => u.ApprovalStatus == status)
                : query.Where(u => u.IdentityVerificationStatus == status);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(u => u.UserName.Contains(term) || u.FullName!.Contains(term) ||
                u.Email!.Contains(term) || u.PhoneNumber!.Contains(term) || u.CitizenId == term);
        }

        const int size = 20;
        page = Math.Clamp(page, 1, int.MaxValue / size);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(u => new { u.Id, u.UserName, u.FullName, u.Role, u.CreatedAt,
                u.ApprovalStatus, u.IdentityVerificationStatus, u.IsActive }).ToListAsync(cancellationToken);
        return Ok(new { data = rows, meta = new PageMeta(page, size, total, (total + size - 1) / size) });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var counts = await Eligible.Where(u => u.Role == UserRoles.Landlord)
            .GroupBy(u => u.ApprovalStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var byStatus = counts.ToDictionary(x => x.Status, x => x.Count);
        return Ok(new { data = new
        {
            pending = byStatus.GetValueOrDefault(ApprovalStatuses.Pending),
            approved = byStatus.GetValueOrDefault(ApprovalStatuses.Approved),
            rejected = byStatus.GetValueOrDefault(ApprovalStatuses.Rejected)
        } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        var user = await Eligible.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new { u.Id, u.UserName, u.FullName, u.Role, u.Email, u.PhoneNumber, u.CitizenId,
                u.DateOfBirth, u.Address, u.ApprovalStatus, u.IdentityVerificationStatus, u.IsActive,
                u.ReviewedAt, u.IdentityVerifiedAt, u.RejectionReason, u.ReviewVersion })
            .SingleOrDefaultAsync(ct);
        return user is null ? NotFound() : Ok(new { data = user });
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> History(string id, int page = 1, CancellationToken ct = default)
    {
        if (!await Eligible.AnyAsync(u => u.Id == id, ct)) return NotFound();
        const int size = 20;
        page = Math.Clamp(page, 1, int.MaxValue / size);
        var query = db.Set<UserReviewHistory>().AsNoTracking().Where(x => x.TargetUserId == id);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Action, x.FromStatus, x.ToStatus, x.Reason, x.VerificationMethod, x.CreatedAt,
                Actor = db.Users.Where(u => u.Id == x.ActorAdminUserId).Select(u => u.UserName).FirstOrDefault(),
                x.ActorAdminUserId }).ToListAsync(ct);
        return Ok(new { data = rows, meta = new PageMeta(page, size, total, (total + size - 1) / size) });
    }

    public sealed record Decision(string Action, byte[]? ExpectedVersion, string? Reason, string? VerificationMethod);

    [HttpPost("{id}/decision")]
    public async Task<IActionResult> Decide(string id, Decision request, CancellationToken ct)
    {
        var actor = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized();
        var user = await Eligible.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        if (request.ExpectedVersion is not { Length: 8 })
            return Problem(statusCode: 400, detail: "Thiếu phiên bản hồ sơ hợp lệ.");
        if (!user.ReviewVersion.SequenceEqual(request.ExpectedVersion)) return ReviewConflict();

        var reason = request.Reason?.Trim();
        var method = request.VerificationMethod?.Trim();
        if (reason?.Length > 500 || method?.Length > 100)
            return Problem(statusCode: 400, detail: "Lý do tối đa 500 ký tự, phương thức tối đa 100 ký tự.");
        if (request.Action is "reject" or "rejectVerification" or "revoke" && string.IsNullOrWhiteSpace(reason))
            return Problem(statusCode: 400, detail: "Vui lòng nhập lý do.");

        var now = DateTime.UtcNow;
        bool approval = request.Action is "approve" or "reject";
        var from = approval ? user.ApprovalStatus : user.IdentityVerificationStatus;
        string to;
        if (approval)
        {
            if (user.Role != UserRoles.Landlord) return NotFound();
            if (from != ApprovalStatuses.Pending) return ReviewConflict();
            bool approved = request.Action == "approve";
            to = approved ? ApprovalStatuses.Approved : ApprovalStatuses.Rejected;
            user.ApprovalStatus = to;
            user.IsActive = approved;
            user.ReviewedAt = now;
            user.RejectionReason = approved ? null : reason;
        }
        else
        {
            to = (request.Action, from) switch
            {
                ("start", IdentityVerificationStatuses.Unverified) => IdentityVerificationStatuses.Pending,
                ("verify", IdentityVerificationStatuses.Pending) => IdentityVerificationStatuses.Verified,
                ("rejectVerification", IdentityVerificationStatuses.Pending) => IdentityVerificationStatuses.Unverified,
                ("revoke", IdentityVerificationStatuses.Verified) => IdentityVerificationStatuses.Unverified,
                _ => ""
            };
            if (to.Length == 0) return ReviewConflict();
            if (to == IdentityVerificationStatuses.Verified)
            {
                if (string.IsNullOrWhiteSpace(user.FullName) || string.IsNullOrWhiteSpace(user.PhoneNumber) ||
                    string.IsNullOrWhiteSpace(user.CitizenId) || user.DateOfBirth is null ||
                    string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(reason))
                    return Problem(statusCode: 400, detail: "Cần đủ thông tin định danh, phương thức và ghi chú đối chiếu.");
                try { UserProfileRules.EnsureValidFormat(user); }
                catch (AppValidationException ex) { return Problem(statusCode: 400, detail: ex.Message); }
            }
            user.IdentityVerificationStatus = to;
            user.IdentityVerifiedAt = to == IdentityVerificationStatuses.Verified ? now : null;
        }

        db.Add(new UserReviewHistory
        {
            TargetUserId = id, ActorAdminUserId = actor, Action = request.Action,
            FromStatus = from, ToStatus = to, Reason = reason, VerificationMethod = method, CreatedAt = now
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ReviewConflict(); }

        return Ok(new { data = new { status = to } });
    }

    private ObjectResult ReviewConflict() =>
        Problem(statusCode: 409, detail: "Hồ sơ đã thay đổi. Vui lòng kiểm tra lại trước khi xử lý.");
}
