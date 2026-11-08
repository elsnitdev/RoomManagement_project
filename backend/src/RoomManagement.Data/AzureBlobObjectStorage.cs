using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.Data;

public sealed class AzureBlobObjectStorage : IObjectStorage
{
    private readonly BlobContainerClient _container;
    public AzureBlobObjectStorage(IConfiguration config)
    {
        var connection = config["AzureBlob:ConnectionString"];
        var accountUri = config["AzureBlob:AccountUri"];
        var container = config["AzureBlob:ContainerName"] ?? "room-images";
        if (string.IsNullOrWhiteSpace(connection) && string.IsNullOrWhiteSpace(accountUri))
            throw new InvalidOperationException("Configure AzureBlob:AccountUri or AzureBlob:ConnectionString.");
        var service = !string.IsNullOrWhiteSpace(connection) ? new BlobServiceClient(connection)
            : new BlobServiceClient(new Uri(accountUri!), new DefaultAzureCredential());
        _container = service.GetBlobContainerClient(container);
    }

    public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken ct)
    {
        await _container.GetBlobClient(name).UploadAsync(content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } }, ct);
    }

    public async Task<Stream> OpenReadAsync(string name, CancellationToken ct) =>
        await _container.GetBlobClient(name).OpenReadAsync(cancellationToken: ct);

    public async Task DeleteAsync(string name, CancellationToken ct) =>
        await _container.GetBlobClient(name).DeleteIfExistsAsync(cancellationToken: ct);
}
