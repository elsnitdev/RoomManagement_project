using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Data;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/notifications")]
public sealed class MyNotificationsController : ControllerBase
{
    private readonly RoomManagementDbContext _db;

    public MyNotificationsController(RoomManagementDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        var validPage = page > 0 ? page : 1;
        var validPageSize = pageSize is > 0 and <= 100 ? pageSize : 20;

        var query = _db.UserNotifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.ContractId,
                n.PropertyId,
                n.ActorUserId,
                n.IsRead,
                n.ReadAt,
                n.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new PageResponse<object>(
            items.Cast<object>().ToList(),
            new PageMeta(validPage, validPageSize, total, (int)Math.Ceiling((double)total / validPageSize))));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        var now = DateTime.UtcNow;
        var updated = await _db.UserNotifications
            .Where(n => n.Id == id && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), ct);

        if (updated == 0)
        {
            var exists = await _db.UserNotifications.AsNoTracking().AnyAsync(n => n.Id == id && n.UserId == userId, ct);
            if (!exists)
                return NotFound();
        }

        return Ok(new ApiResponse<object>(new { id, isRead = true }));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        var now = DateTime.UtcNow;
        await _db.UserNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), ct);

        return Ok(new ApiResponse<object>(new { readAll = true }));
    }
}
