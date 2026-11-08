namespace RoomManagement.Business.Interfaces;

public interface IObjectStorage
{
    Task UploadAsync(string name, Stream content, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string name, CancellationToken ct);
    Task DeleteAsync(string name, CancellationToken ct);
}
