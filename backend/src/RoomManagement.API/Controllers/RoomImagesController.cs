using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/properties/{propertyId:guid}/rooms/{roomId:guid}/images")]
public sealed class RoomImagesController(RoomImageService images) : ControllerBase
{
    public sealed record ImageResponse(Guid Id, string FileName, string ContentType, long SizeBytes, int SortOrder, DateTime CreatedAt)
    {
        public static ImageResponse From(RoomImage image) => new(image.Id, image.OriginalFileName,
            image.ContentType, image.SizeBytes, image.SortOrder, image.CreatedAt);
    }
    public sealed record ReorderImagesRequest(IReadOnlyList<Guid> ImageIds);

    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId, Guid roomId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<ImageResponse>>((await images.ListAsync(propertyId, roomId, ct))
            .Select(ImageResponse.From).ToList()));

    [HttpPost]
    [RequestSizeLimit(RoomImageService.MaxFileBytes + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid propertyId, Guid roomId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await images.UploadAsync(propertyId, roomId, file.FileName, file.Length, stream, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<ImageResponse>(ImageResponse.From(result)));
    }

    [HttpGet("{imageId:guid}/content")]
    public async Task<IActionResult> Content(Guid propertyId, Guid roomId, Guid imageId, CancellationToken ct)
    {
        var (image, stream) = await images.OpenAsync(propertyId, roomId, imageId, ct);
        Response.Headers.CacheControl = "private, max-age=300";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, image.ContentType);
    }

    [HttpDelete("{imageId:guid}")]
    public async Task<IActionResult> Delete(Guid propertyId, Guid roomId, Guid imageId, CancellationToken ct)
    { await images.DeleteAsync(propertyId, roomId, imageId, ct); return NoContent(); }

    [HttpPut("order")]
    public async Task<IActionResult> Reorder(Guid propertyId, Guid roomId, ReorderImagesRequest request, CancellationToken ct)
    { await images.ReorderAsync(propertyId, roomId, request.ImageIds, ct); return NoContent(); }
}
