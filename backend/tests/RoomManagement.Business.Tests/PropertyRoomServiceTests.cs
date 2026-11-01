using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using Xunit;
using RoomManagementRoomService = RoomManagement.Business.Services.RoomService;

namespace RoomManagement.Business.Tests;

public sealed class PropertyRoomServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string OwnerId = "landlord-1";

    [Fact]
    public async Task CreateProperty_AssignsOwnerFromProvider()
    {
        var properties = new FakeProperties();
        var service = new PropertyService(new Current(), new ActiveLandlord(), properties);
        var result = await service.CreateAsync(new CreatePropertyInput("  Khu A  ", "  Địa chỉ  ", null), Ct);
        Assert.Equal(OwnerId, result.OwnerUserId);
        Assert.Equal("Khu A", result.Name);
        Assert.Same(result, properties.Added);
    }

    [Fact]
    public async Task PropertyRead_UsesOwnerScope_AndHidesOtherOwner()
    {
        var properties = new FakeProperties { Property = new Property { Id = Guid.NewGuid(), OwnerUserId = "other-owner" } };
        var service = new PropertyService(new Current(), new ActiveLandlord(), properties);
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(properties.Property.Id, Ct));
        Assert.Equal("PROPERTY_NOT_FOUND", error.Code);
        Assert.Equal(OwnerId, properties.LastOwnerId);
    }

    [Fact]
    public async Task RoomUpdate_RejectsOccupiedEvenWhenRoomExists()
    {
        var propertyId = Guid.NewGuid();
        var room = new Room { Id = Guid.NewGuid(), PropertyId = propertyId, RoomNumber = "A1", MaxOccupants = 1, RowVersion = new byte[8] };
        var properties = new FakeProperties { Property = new Property { Id = propertyId, OwnerUserId = OwnerId, IsActive = true } };
        var rooms = new FakeRooms { Room = room };
        var service = new RoomManagementRoomService(new Current(), new ActiveLandlord(), properties, rooms);
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(propertyId, room.Id,
                new UpdateRoomInput("A1", null, null, null, 100, 1, null, RoomStatus.Occupied, Convert.ToBase64String(new byte[8])), Ct));
        Assert.Equal("ROOM_STATUS_INVALID", error.Code);
        Assert.False(rooms.Saved);
        Assert.Equal(OwnerId, rooms.LastOwnerId);
    }

    [Fact]
    public async Task RoomDelete_BlocksHistoricalContracts()
    {
        var propertyId = Guid.NewGuid();
        var room = new Room { Id = Guid.NewGuid(), PropertyId = propertyId, RoomNumber = "A1" };
        var properties = new FakeProperties { Property = new Property { Id = propertyId, OwnerUserId = OwnerId } };
        var rooms = new FakeRooms { Room = room, Blocker = "ROOM_HAS_CONTRACTS" };
        var service = new RoomManagementRoomService(new Current(), new ActiveLandlord(), properties, rooms);
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(propertyId, room.Id, Ct));
        Assert.Equal("ROOM_HAS_CONTRACTS", error.Code);
        Assert.False(rooms.Deleted);
    }

    [Fact]
    public async Task BatchPreview_RejectsDuplicateScopeBeforeStore()
    {
        var propertyId = Guid.NewGuid();
        var property = new FakeProperties { Property = new Property { Id = propertyId, OwnerUserId = OwnerId, IsActive = true } };
        var store = new FakeBatchStore();
        var service = new RoomBatchService(new Current(), new ActiveLandlord(), property, store);
        var values = new RoomValues(null, 25, 100, 2, null);
        var plan = new RoomBatchPlan(
            [new RoomBatchGroup("a", null, 1, 2, values), new RoomBatchGroup("b", null, 1, 3, values)], []);
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.PreviewAsync(propertyId, plan, Ct));
        Assert.Equal("ROOM_BATCH_GROUP_INVALID", error.Code);
        Assert.False(store.PreviewCalled);
    }

    [Fact]
    public async Task BatchPreview_RejectsOverrideOutsideGroup()
    {
        var propertyId = Guid.NewGuid();
        var property = new FakeProperties { Property = new Property { Id = propertyId, OwnerUserId = OwnerId, IsActive = true } };
        var store = new FakeBatchStore();
        var service = new RoomBatchService(new Current(), new ActiveLandlord(), property, store);
        var values = new RoomValues(null, 25, 100, 2, null);
        var plan = new RoomBatchPlan([new RoomBatchGroup("a", null, 1, 2, values)],
            [new RoomBatchOverride("a", 3, values)]);
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.PreviewAsync(propertyId, plan, Ct));
        Assert.Equal("ROOM_BATCH_OVERRIDE_INVALID", error.Code);
        Assert.False(store.PreviewCalled);
    }

    [Fact]
    public async Task GeneratedRoom_CannotChangeNumberOrFloorThroughNormalEdit()
    {
        var propertyId = Guid.NewGuid();
        var room = new Room { Id = Guid.NewGuid(), PropertyId = propertyId, RoomNumber = "101", Floor = 1,
            SequenceNumber = 1, MaxOccupants = 2, RowVersion = new byte[8] };
        var property = new FakeProperties { Property = new Property { Id = propertyId, OwnerUserId = OwnerId, IsActive = true } };
        var rooms = new FakeRooms { Room = room };
        var service = new RoomManagementRoomService(new Current(), new ActiveLandlord(), property, rooms);
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(propertyId, room.Id,
            new UpdateRoomInput("102", null, 1, 25, 100, 2, null, RoomStatus.Available, Convert.ToBase64String(new byte[8])), Ct));
        Assert.Equal("ROOM_NUMBER_MANAGED", error.Code);
        Assert.False(rooms.Saved);
    }

    private sealed class Current : ICurrentLandlord
    {
        public string GetUserId() => OwnerId;
    }
    private sealed class ActiveLandlord : ILandlordStore
    {
        public Task<bool> IsActiveLandlordAsync(string userId, CancellationToken ct) => Task.FromResult(userId == OwnerId);
    }
    private sealed class FakeProperties : IPropertyStore
    {
        public Property? Property { get; init; }
        public Property? Added { get; private set; }
        public string? LastOwnerId { get; private set; }
        public Task AddAsync(Property property, CancellationToken ct) { Added = property; return Task.CompletedTask; }
        public Task<Property?> GetAsync(Guid propertyId, string ownerId, CancellationToken ct)
        {
            LastOwnerId = ownerId;
            return Task.FromResult(Property?.Id == propertyId && Property.OwnerUserId == ownerId ? Property : null);
        }
        public Task<(IReadOnlyList<Property> Items, long TotalItems)> ListAsync(string ownerId, string? search, bool? active, int skip, int take, CancellationToken ct) =>
            Task.FromResult(((IReadOnlyList<Property>)Array.Empty<Property>(), 0L));
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetDeleteBlockerAsync(Guid propertyId, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task DeleteAsync(Property property, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakeRooms : IRoomStore
    {
        public Room? Room { get; init; }
        public string? Blocker { get; init; }
        public string? LastOwnerId { get; private set; }
        public bool Saved { get; private set; }
        public bool Deleted { get; private set; }
        public Task AddAsync(Room room, CancellationToken ct) => Task.CompletedTask;
        public Task<Room?> GetAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct)
        {
            LastOwnerId = ownerId;
            return Task.FromResult(ownerId == OwnerId && Room?.Id == roomId && Room.PropertyId == propertyId ? Room : null);
        }
        public Task<RoomResult?> GetResultAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct) =>
            Task.FromResult<RoomResult?>(null);
        public Task<(IReadOnlyList<RoomResult> Items, long TotalItems)> ListAsync(Guid propertyId, string ownerId, string? search, RoomStatus? status, Guid? rowId, int? floor, int skip, int take, CancellationToken ct) =>
            Task.FromResult(((IReadOnlyList<RoomResult>)Array.Empty<RoomResult>(), 0L));
        public Task<bool> NumberExistsAsync(Guid propertyId, string number, Guid? exceptRoomId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> HasActiveContractAsync(Guid roomId, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> GetDeleteBlockerAsync(Guid roomId, CancellationToken ct) => Task.FromResult(Blocker);
        public Task SaveAsync(CancellationToken ct) { Saved = true; return Task.CompletedTask; }
        public Task DeleteAsync(Room room, CancellationToken ct) { Deleted = true; return Task.CompletedTask; }
    }

    private sealed class FakeBatchStore : IRoomBatchStore
    {
        public bool PreviewCalled { get; private set; }
        public Task<IReadOnlyList<RoomRow>> ListRowsAsync(Guid propertyId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RoomRow>>([]);
        public Task<RoomRow> AddRowAsync(Guid propertyId, string code, string name, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<RoomRow> UpdateRowAsync(Guid propertyId, Guid rowId, string name, bool isActive, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<RoomBatchPreviewResult> PreviewAsync(Guid propertyId, RoomBatchPlan plan, CancellationToken ct)
        {
            PreviewCalled = true;
            return Task.FromResult(new RoomBatchPreviewResult([], "test"));
        }
        public Task<RoomBatchCreateResult> CreateAsync(Guid propertyId, string ownerId, RoomBatchPlan plan, string fingerprint, Guid idempotencyKey, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
