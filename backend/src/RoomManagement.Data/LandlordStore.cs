using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;

namespace RoomManagement.Data;

public sealed class LandlordStore : ILandlordStore
{
    private readonly RoomManagementDbContext _db;

    public LandlordStore(RoomManagementDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsActiveLandlordAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return _db.Users.AnyAsync(
            user => user.Id == userId
                && user.Role == UserRoles.Landlord
                && user.IsActive,
            cancellationToken);
    }
}
