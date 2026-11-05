namespace RoomManagement.Business.Interfaces;

public interface ILandlordStore
{
  /// <summary>Kiểm tra tài khoản có phải chủ trọ đang hoạt động hay không.</summary>
  Task<bool> IsActiveLandlordAsync(string userId, CancellationToken cancellationToken);
}
