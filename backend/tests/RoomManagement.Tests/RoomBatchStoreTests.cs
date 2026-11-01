using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

public sealed class RoomBatchStoreTests
{
    [SqlServerFact]
    public async Task Concurrent_batches_never_share_room_numbers_or_partially_save()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var propertyId = Guid.NewGuid();
        const string ownerId = "concurrent-batch-owner";
        await using (var seed = database.CreateContext())
        {
            seed.Users.Add(new AppUser
            {
                Id = ownerId, UserName = ownerId, NormalizedUserName = ownerId.ToUpperInvariant(),
                PasswordHash = "test-hash", Role = UserRoles.Landlord,
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            seed.Properties.Add(new Property
            {
                Id = propertyId, OwnerUserId = ownerId, Name = "Khu test", Address = "Địa chỉ test",
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }
        var plan = new RoomBatchPlan(
            [new RoomBatchGroup("floor-1", null, 1, 2, new RoomValues(null, 25, 2_500_000, 2, null))], []);
        string fingerprint;
        await using (var context = database.CreateContext())
            fingerprint = (await new RoomBatchStore(context).PreviewAsync(propertyId, plan, CancellationToken.None)).PreviewFingerprint;

        async Task<Exception?> SendAsync()
        {
            await using var context = database.CreateContext();
            return await Record.ExceptionAsync(() => new RoomBatchStore(context).CreateAsync(
                propertyId, ownerId, plan, fingerprint, Guid.NewGuid(), CancellationToken.None));
        }
        var results = await Task.WhenAll(SendAsync(), SendAsync());
        Assert.Single(results, error => error is null);
        Assert.Single(results, error => error is BusinessException { Code: "ROOM_PREVIEW_CHANGED" });
        await using var verify = database.CreateContext();
        Assert.Equal(new[] { "101", "102" }, await verify.Rooms.OrderBy(r => r.RoomNumber).Select(r => r.RoomNumber).ToArrayAsync());
        Assert.Single(await verify.RoomBatchSubmissions.ToListAsync());
    }

    [SqlServerFact]
    public async Task Batch_creates_special_room_atomically_and_retry_keeps_ids()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var propertyId = Guid.NewGuid();
        const string ownerId = "batch-test-owner";
        await using (var seed = database.CreateContext())
        {
            seed.Users.Add(new AppUser
            {
                Id = ownerId, UserName = ownerId, NormalizedUserName = ownerId.ToUpperInvariant(),
                PasswordHash = "test-hash", Role = UserRoles.Landlord,
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            seed.Properties.Add(new Property
            {
                Id = propertyId, OwnerUserId = ownerId, Name = "Khu test", Address = "Địa chỉ test",
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var common = new RoomValues(null, 25, 2_500_000, 2, null);
        var special = new RoomValues("Phòng góc", 30, 3_000_000, 2, "Có ban công");
        var plan = new RoomBatchPlan(
            [new RoomBatchGroup("floor-1", null, 1, 4, common)],
            [new RoomBatchOverride("floor-1", 3, special)]);
        var idempotencyKey = Guid.NewGuid();
        string originalFingerprint;
        await using (var context = database.CreateContext())
        {
            var store = new RoomBatchStore(context);
            var preview = await store.PreviewAsync(propertyId, plan, CancellationToken.None);
            originalFingerprint = preview.PreviewFingerprint;
            Assert.Equal(new[] { "101", "102", "103", "104" }, preview.Rooms.Select(x => x.RoomNumber).ToArray());
            var result = await store.CreateAsync(propertyId, ownerId, plan, preview.PreviewFingerprint, idempotencyKey, CancellationToken.None);
            Assert.Equal(4, result.Rooms.Count);
            Assert.Equal("Phòng góc", result.Rooms.Single(x => x.Room.RoomNumber == "103").Room.RoomName);
            Assert.Equal(3_000_000m, result.Rooms.Single(x => x.Room.RoomNumber == "103").Room.BasePrice);
            var repeated = await store.CreateAsync(propertyId, ownerId, plan, preview.PreviewFingerprint, idempotencyKey, CancellationToken.None);
            Assert.Equal(result.Rooms.Select(x => x.Room.Id), repeated.Rooms.Select(x => x.Room.Id));
        }
        await using (var verify = database.CreateContext())
        {
            Assert.Equal(4, await verify.Rooms.CountAsync());
            Assert.Equal(4, (await verify.RoomNumberCounters.SingleAsync()).LastSequence);
            Assert.Single(await verify.RoomBatchSubmissions.ToListAsync());
        }

        await using (var changed = database.CreateContext())
        {
            changed.Rooms.Remove(await changed.Rooms.SingleAsync(r => r.RoomNumber == "101"));
            await changed.SaveChangesAsync();
            var repeated = await new RoomBatchStore(changed).CreateAsync(
                propertyId, ownerId, plan, originalFingerprint, idempotencyKey, CancellationToken.None);
            Assert.Equal(4, repeated.Rooms.Count);
            Assert.Equal("101", repeated.Rooms.First().Room.RoomNumber);
            Assert.Equal(3, await changed.Rooms.CountAsync());
        }

        await using (var context = database.CreateContext())
        {
            var store = new RoomBatchStore(context);
            var fresh = await store.PreviewAsync(propertyId, plan, CancellationToken.None);
            Assert.Equal("105", fresh.Rooms.First().RoomNumber);
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                store.CreateAsync(propertyId, ownerId, plan, "stale-fingerprint", Guid.NewGuid(), CancellationToken.None));
            Assert.Equal("ROOM_PREVIEW_CHANGED", error.Code);
        }
    }
}
