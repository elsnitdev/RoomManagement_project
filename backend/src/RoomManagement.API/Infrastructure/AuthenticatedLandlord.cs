using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Infrastructure;

public sealed class AuthenticatedLandlord(IHttpContextAccessor httpContextAccessor) : ICurrentLandlord
{
    public string GetUserId() =>
        httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Không tìm thấy ID chủ trọ trong token.");
}
