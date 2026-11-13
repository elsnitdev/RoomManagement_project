using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/payments/{paymentId:guid}/evidence")]
public sealed class PaymentEvidenceController(PaymentEvidenceService service) : ControllerBase
{
    public sealed record EvidenceResponse(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt)
    {
        public static EvidenceResponse From(PaymentEvidence image) =>
            new(image.Id, image.OriginalFileName, image.ContentType, image.SizeBytes, image.CreatedAt);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid paymentId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<EvidenceResponse>>((await service.ListAsync(paymentId, ct)).Select(EvidenceResponse.From).ToList()));

    [HttpPost]
    [RequestSizeLimit(PaymentEvidenceService.MaxFileBytes + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid paymentId, [FromForm] Guid imageId, [FromForm] IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(paymentId, imageId, file.FileName, file.Length, stream, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<EvidenceResponse>(EvidenceResponse.From(result)));
    }

    [HttpGet("{imageId:guid}/content")]
    public async Task<IActionResult> Content(Guid paymentId, Guid imageId, CancellationToken ct)
    {
        var (image, stream) = await service.OpenAsync(paymentId, imageId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, image.ContentType);
    }
}
