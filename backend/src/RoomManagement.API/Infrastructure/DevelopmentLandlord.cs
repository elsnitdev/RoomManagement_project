using RoomManagement.Business.Interfaces;

namespace RoomManagement.API.Infrastructure;

public sealed class DevelopmentLandlord : ICurrentLandlord
{
  private readonly string _userId;
  public DevelopmentLandlord(IConfiguration configuration)
  {
    _userId = configuration["DevelopmentLandlord:UserId"]
               ?? throw new InvalidOperationException(
                   "Thiếu cấu hình DevelopmentLandlord:UserId.");

    if (string.IsNullOrWhiteSpace(_userId))
    {
      throw new InvalidOperationException(
          "DevelopmentLandlord:UserId không được rỗng.");
    }
  }
  public string GetUserId() => _userId;
}