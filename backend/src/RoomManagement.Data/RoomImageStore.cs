using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data;

public sealed class RoomImageStore(RoomManagementDbContext db) : IRoomImageStore
{
    public Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct) =>
        (from room in db.Rooms join property in db.Properties on room.PropertyId equals property.Id
         where room.Id == roomId && room.PropertyId == propertyId && property.OwnerUserId == ownerId
         select room.Id).AnyAsync(ct);

    public async Task<IReadOnlyList<RoomImage>> ListAsync(Guid roomId, CancellationToken ct) =>
        await db.RoomImages.AsNoTracking().Where(x => x.RoomId == roomId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);

    public Task<RoomImage?> GetAsync(Guid roomId, Guid imageId, CancellationToken ct) =>
        db.RoomImages.AsNoTracking().SingleOrDefaultAsync(x => x.RoomId == roomId && x.Id == imageId, ct);

    public async Task AddAsync(RoomImage image, CancellationToken ct)
    { db.RoomImages.Add(image); await db.SaveChangesAsync(ct); }

    public async Task DeleteAndQueueCleanupAsync(RoomImage image, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.RoomImages.Remove(image);
        db.BlobCleanupJobs.Add(new BlobCleanupJob { Id = Guid.NewGuid(), BlobName = image.BlobName,
            CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task SetOrderAsync(Guid roomId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var images = await db.RoomImages.Where(x => x.RoomId == roomId).ToListAsync(ct);
        for (var i = 0; i < ids.Count; i++) images.Single(x => x.Id == ids[i]).SortOrder = i;
        await db.SaveChangesAsync(ct);
    }
}
