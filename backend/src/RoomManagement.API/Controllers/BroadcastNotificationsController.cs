using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize]
public sealed class BroadcastNotificationsController(RoomManagementDbContext db) : ControllerBase
{
    public sealed record BroadcastInput(string Title, string Message, Guid IdempotencyKey);

    [HttpPost("api/admin/notifications/broadcast")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> AdminBroadcast([FromBody] BroadcastInput input, CancellationToken ct)
    {
        if (!Valid(input)) return BadRequest("Tiêu đề, nội dung hoặc khóa gửi không hợp lệ.");
        var actor = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(actor)) return Forbid();
        var key = $"admin:{input.IdempotencyKey:N}";
        if (await db.UserNotifications.AnyAsync(n => n.EventKey == key, ct))
            return Ok(new ApiResponse<object>(new { duplicate = true }));
        var ids = await db.Users.AsNoTracking().Where(u => u.IsActive && u.DeletedAt == null)
            .Select(u => u.Id).ToListAsync(ct);
        var now = DateTime.UtcNow;
        db.UserNotifications.AddRange(ids.Select(id => NewNotification(id, actor, "SystemBroadcast", input, key, now)));
        await db.SaveChangesAsync(ct);
        return Ok(new ApiResponse<object>(new { recipients = ids.Count }));
    }

    [HttpPost("api/properties/{propertyId:guid}/manager-announcements")]
    [Authorize(Roles = UserRoles.Manager)]
    public async Task<IActionResult> ManagerBroadcast(Guid propertyId, [FromBody] BroadcastInput input, CancellationToken ct)
    {
        if (!Valid(input)) return BadRequest("Tiêu đề, nội dung hoặc khóa gửi không hợp lệ.");
        var actor = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(actor)) return Forbid();
        var allowed = await db.PropertyManagerAssignments.AsNoTracking()
            .AnyAsync(a => a.PropertyId == propertyId && a.ManagerUserId == actor && a.RevokedAt == null, ct);
        if (!allowed) return Forbid();
        var key = $"manager:{propertyId:N}:{input.IdempotencyKey:N}";
        if (await db.UserNotifications.AnyAsync(n => n.EventKey == key, ct))
            return Ok(new ApiResponse<object>(new { duplicate = true }));
        var ids = await (from link in db.ContractTenants.AsNoTracking()
                         join profile in db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
                         join contract in db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
                         join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                         join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
                         where room.PropertyId == propertyId && contract.Status == ContractStatus.Active &&
                               link.LeaveDate == null && user.IsActive && user.DeletedAt == null
                         select user.Id).Distinct().ToListAsync(ct);
        var now = DateTime.UtcNow;
        db.UserNotifications.AddRange(ids.Select(id =>
        {
            var notification = NewNotification(id, actor, "ManagerAnnouncement", input, key, now);
            notification.PropertyId = propertyId;
            return notification;
        }));
        await db.SaveChangesAsync(ct);
        return Ok(new ApiResponse<object>(new { recipients = ids.Count }));
    }

    private static bool Valid(BroadcastInput input) => input.IdempotencyKey != Guid.Empty &&
        !string.IsNullOrWhiteSpace(input.Title) && input.Title.Trim().Length <= 250 &&
        !string.IsNullOrWhiteSpace(input.Message) && input.Message.Trim().Length <= 4000;

    private static UserNotification NewNotification(string id, string actor, string type,
        BroadcastInput input, string key, DateTime now) => new()
    {
        Id = Guid.NewGuid(), UserId = id, ActorUserId = actor, Type = type,
        Title = input.Title.Trim(), Message = input.Message.Trim(), EventKey = key,
        CreatedAt = now
    };
}
