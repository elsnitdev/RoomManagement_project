using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Infrastructure;

public sealed class AuthenticatedLandlord : ICurrentLandlord
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticatedLandlord(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Không tìm thấy ID chủ trọ trong token.");
}
