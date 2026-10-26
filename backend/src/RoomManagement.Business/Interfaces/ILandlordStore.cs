namespace RoomManagement.Business.Interfaces;

public interface ILandlordStore
{
  Task<bool> IsActiveLandlordAsync(string userId, CancellationToken cancellationToken);
}