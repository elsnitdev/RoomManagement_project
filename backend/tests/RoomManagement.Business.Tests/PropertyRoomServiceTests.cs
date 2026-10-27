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
        public Task<(IReadOnlyList<RoomResult> Items, long TotalItems)> ListAsync(Guid propertyId, string ownerId, string? search, RoomStatus? status, int skip, int take, CancellationToken ct) =>
            Task.FromResult(((IReadOnlyList<RoomResult>)Array.Empty<RoomResult>(), 0L));
        public Task<bool> NumberExistsAsync(Guid propertyId, string number, Guid? exceptRoomId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> HasActiveContractAsync(Guid roomId, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> GetDeleteBlockerAsync(Guid roomId, CancellationToken ct) => Task.FromResult(Blocker);
        public Task SaveAsync(CancellationToken ct) { Saved = true; return Task.CompletedTask; }
        public Task DeleteAsync(Room room, CancellationToken ct) { Deleted = true; return Task.CompletedTask; }
    }
}
