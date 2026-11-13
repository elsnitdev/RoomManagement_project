using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class RoomImageService
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    private readonly ICurrentLandlord _current;
    private readonly ILandlordStore _landlords;
    private readonly IRoomImageStore _images;
    private readonly IObjectStorage _storage;

    public RoomImageService(ICurrentLandlord current, ILandlordStore landlords, IRoomImageStore images, IObjectStorage storage)
    { _current = current; _landlords = landlords; _images = images; _storage = storage; }

    private async Task<string> AuthorizeAsync(Guid propertyId, Guid roomId, CancellationToken ct)
    {
        var context = await OperatorAccessGuard.RequireOperatorAsync(_current, _landlords, ct);
        if (!await _landlords.HasPropertyAccessAsync(context.ActorUserId, propertyId, ct) ||
            !await _images.RoomOwnedAsync(propertyId, roomId, context.OwnerLandlordUserId, ct))
            throw new BusinessException(BusinessErrorType.NotFound, "ROOM_NOT_FOUND", "Không tìm thấy phòng.");
        return context.OwnerLandlordUserId;
    }

    public async Task<IReadOnlyList<RoomImage>> ListAsync(Guid propertyId, Guid roomId, CancellationToken ct)
    { await AuthorizeAsync(propertyId, roomId, ct); return await _images.ListAsync(roomId, ct); }

    public async Task<RoomImage> UploadAsync(Guid propertyId, Guid roomId, string fileName, long length, Stream stream, CancellationToken ct)
    {
        var owner = await AuthorizeAsync(propertyId, roomId, ct);
        if (length <= 0 || length > MaxFileBytes)
            throw new BusinessException(BusinessErrorType.Validation, "IMAGE_SIZE_INVALID", "Ảnh phải có dung lượng từ 1 byte đến 10 MiB.");
        var images = await _images.ListAsync(roomId, ct);
        if (images.Count >= 20)
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_IMAGE_LIMIT", "Mỗi phòng chỉ được lưu tối đa 20 ảnh.");
        var header = new byte[12];
        var read = 0;
        while (read < header.Length)
        { var n = await stream.ReadAsync(header.AsMemory(read), ct); if (n == 0) break; read += n; }
        var mime = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF ? "image/jpeg"
            : read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? "image/webp" : null;
        if (mime is null)
            throw new BusinessException(BusinessErrorType.Validation, "IMAGE_FORMAT_INVALID", "Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.");
        var suffix = mime switch { "image/jpeg" => "jpg", "image/png" => "png", _ => "webp" };
        var id = Guid.NewGuid();
        var name = $"landlords/{owner}/properties/{propertyId}/rooms/{roomId}/{id}.{suffix}";
        var cleanFileName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(cleanFileName)) cleanFileName = $"{id}.{suffix}";
        if (cleanFileName.Length > 255) cleanFileName = cleanFileName[..255];
        var image = new RoomImage { Id = id, RoomId = roomId, BlobName = name,
            OriginalFileName = cleanFileName, ContentType = mime, SizeBytes = length,
            SortOrder = images.Count == 0 ? 0 : images.Max(x => x.SortOrder) + 1, CreatedAt = DateTime.UtcNow };
        using var upload = new PrefixStream(header[..read], stream);
        await _storage.UploadAsync(name, upload, mime, ct);
        try { await _images.AddAsync(image, ct); }
        catch { try { await _storage.DeleteAsync(name, CancellationToken.None); } catch { /* reconciliation handles orphan */ } throw; }
        return image;
    }

    public async Task<(RoomImage Image, Stream Content)> OpenAsync(Guid propertyId, Guid roomId, Guid imageId, CancellationToken ct)
    {
        await AuthorizeAsync(propertyId, roomId, ct);
        var image = await _images.GetAsync(roomId, imageId, ct) ??
            throw new BusinessException(BusinessErrorType.NotFound, "IMAGE_NOT_FOUND", "Không tìm thấy ảnh.");
        return (image, await _storage.OpenReadAsync(image.BlobName, ct));
    }

    public async Task DeleteAsync(Guid propertyId, Guid roomId, Guid imageId, CancellationToken ct)
    {
        await AuthorizeAsync(propertyId, roomId, ct);
        var image = await _images.GetAsync(roomId, imageId, ct) ??
            throw new BusinessException(BusinessErrorType.NotFound, "IMAGE_NOT_FOUND", "Không tìm thấy ảnh.");
        await _images.DeleteAndQueueCleanupAsync(image, ct);
    }

    public async Task ReorderAsync(Guid propertyId, Guid roomId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await AuthorizeAsync(propertyId, roomId, ct);
        var images = await _images.ListAsync(roomId, ct);
        if (ids.Count != images.Count || ids.Distinct().Count() != ids.Count || !ids.ToHashSet().SetEquals(images.Select(x => x.Id)))
            throw new BusinessException(BusinessErrorType.Validation, "IMAGE_ORDER_INVALID", "Danh sách sắp xếp ảnh không hợp lệ.");
        await _images.SetOrderAsync(roomId, ids, ct);
    }

    private sealed class PrefixStream(byte[] prefix, Stream inner) : Stream
    {
        private int _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { var n = Read(new Span<byte>(buffer, offset, count)); return n; }
        public override int Read(Span<byte> buffer) { var n = Math.Min(buffer.Length, prefix.Length - _position); if (n > 0) { prefix.AsSpan(_position, n).CopyTo(buffer); _position += n; return n; } return inner.Read(buffer); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { var n = Math.Min(buffer.Length, prefix.Length - _position); if (n > 0) { prefix.AsMemory(_position, n).CopyTo(buffer); _position += n; return n; } return await inner.ReadAsync(buffer, cancellationToken); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
