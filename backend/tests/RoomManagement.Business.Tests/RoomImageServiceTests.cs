using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using Xunit;

namespace RoomManagement.Business.Tests;

public sealed class RoomImageServiceTests
{
    private readonly FakeImages _images = new();
    private readonly FakeStorage _storage = new();
    private readonly RoomImageService _service;
    private readonly Guid _property = Guid.NewGuid();
    private readonly Guid _room = Guid.NewGuid();

    public RoomImageServiceTests() => _service = new RoomImageService(new Current(), new Landlord(), _images, _storage);

    [Fact]
    public async Task OtherLandlordCannotReadOrUpload()
    {
        _images.Owned = false;
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.ListAsync(_property, _room, default));
        Assert.Equal("ROOM_NOT_FOUND", ex.Code);
        await Assert.ThrowsAsync<BusinessException>(() => _service.UploadAsync(_property, _room, "a.png", 8,
            new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), default));
        Assert.False(_storage.Uploaded);
    }

    [Fact]
    public async Task InvalidImageNeverReachesBlob()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.UploadAsync(_property, _room, "fake.png", 8,
            new MemoryStream(new byte[8]), default));
        Assert.Equal("IMAGE_FORMAT_INVALID", ex.Code);
        Assert.False(_storage.Uploaded);
    }

    [Fact]
    public async Task JpegUploadPersistsMetadataAndOriginalStream()
    {
        var bytes = new byte[] { 255, 216, 255, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var image = await _service.UploadAsync(_property, _room, "a.jpg", bytes.Length, new MemoryStream(bytes), default);
        Assert.Equal("image/jpeg", image.ContentType);
        Assert.Equal(bytes, _storage.Bytes);
        Assert.Single(_images.Items);
    }

    private sealed class Current : ICurrentLandlord { public string GetUserId() => "owner"; }
    private sealed class Landlord : ILandlordStore { public Task<bool> IsActiveLandlordAsync(string userId, CancellationToken ct) => Task.FromResult(true); }
    private sealed class FakeStorage : IObjectStorage
    {
        public bool Uploaded { get; private set; }
        public byte[] Bytes { get; private set; } = [];
        public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken ct)
        { Uploaded = true; using var copy = new MemoryStream(); await content.CopyToAsync(copy, ct); Bytes = copy.ToArray(); }
        public Task<Stream> OpenReadAsync(string name, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string name, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakeImages : IRoomImageStore
    {
        public bool Owned { get; set; } = true;
        public List<RoomImage> Items { get; } = [];
        public Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct) => Task.FromResult(Owned);
        public Task<IReadOnlyList<RoomImage>> ListAsync(Guid roomId, CancellationToken ct) => Task.FromResult<IReadOnlyList<RoomImage>>(Items);
        public Task<RoomImage?> GetAsync(Guid roomId, Guid imageId, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.Id == imageId));
        public Task AddAsync(RoomImage image, CancellationToken ct) { Items.Add(image); return Task.CompletedTask; }
        public Task DeleteAndQueueCleanupAsync(RoomImage image, CancellationToken ct) { Items.Remove(image); return Task.CompletedTask; }
        public Task SetOrderAsync(Guid roomId, IReadOnlyList<Guid> ids, CancellationToken ct) => Task.CompletedTask;
    }
}
