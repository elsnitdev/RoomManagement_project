using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/properties/{propertyId:guid}/contracts/{contractId:guid}/settlement/refund-evidence")]
public sealed class SettlementRefundEvidenceController(SettlementRefundEvidenceService service) : ControllerBase
{
    public sealed record RefundEvidenceResponse(Guid Id, string FileName, string ContentType,
        long SizeBytes, DateTime UploadedAt)
    {
        public static RefundEvidenceResponse? From(ContractSettlement settlement) =>
            settlement.RefundEvidenceId is { } id && settlement.RefundEvidenceFileName is { } name &&
            settlement.RefundEvidenceContentType is { } contentType &&
            settlement.RefundEvidenceSizeBytes is { } size && settlement.RefundEvidenceUploadedAt is { } uploadedAt
                ? new(id, name, contentType, size, uploadedAt) : null;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid propertyId, Guid contractId, CancellationToken ct) =>
        Ok(new ApiResponse<RefundEvidenceResponse?>(RefundEvidenceResponse.From(
            await service.GetAsync(propertyId, contractId, ct))));

    [HttpPost]
    [RequestSizeLimit(SettlementRefundEvidenceService.MaxFileBytes + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid propertyId, Guid contractId,
        [FromForm] Guid imageId, [FromForm] IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(propertyId, contractId, imageId, file.FileName, file.Length, stream, ct);
        return StatusCode(StatusCodes.Status201Created,
            new ApiResponse<RefundEvidenceResponse>(RefundEvidenceResponse.From(result)!));
    }

    [HttpGet("{imageId:guid}/content")]
    public async Task<IActionResult> Content(Guid propertyId, Guid contractId, Guid imageId, CancellationToken ct)
    {
        var (settlement, stream) = await service.OpenAsync(propertyId, contractId, imageId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, settlement.RefundEvidenceContentType!);
    }
}
