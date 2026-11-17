using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.API.Controllers;

public sealed record ContractDocumentResponse(Guid Id, string FileName, string? ContentType,
    long? SizeBytes, int? PageOrder, int? OfferVersion, DateTime UploadedAt)
{
    public static ContractDocumentResponse From(ContractDocument document) => new(document.Id,
        document.FileName, document.FileType, document.SizeBytes, document.PageOrder,
        document.OfferVersion, document.UploadedAt);
}

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/properties/{propertyId:guid}/contracts/{contractId:guid}/documents")]
public sealed class ContractDocumentsController(ContractDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId, Guid contractId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<ContractDocumentResponse>>(
            (await service.OwnerListAsync(propertyId, contractId, ct)).Select(ContractDocumentResponse.From).ToList()));

    [HttpPost]
    [RequestSizeLimit(ContractDocumentService.MaxFileBytes + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid propertyId, Guid contractId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var document = await service.UploadAsync(propertyId, contractId, file.FileName, file.Length, stream, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<ContractDocumentResponse>(ContractDocumentResponse.From(document)));
    }

    [HttpGet("{documentId:guid}/content")]
    public async Task<IActionResult> Content(Guid propertyId, Guid contractId, Guid documentId, CancellationToken ct)
    {
        var (document, stream) = await service.OpenOwnerAsync(propertyId, contractId, documentId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, document.FileType ?? "application/octet-stream", document.FileName);
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid propertyId, Guid contractId, Guid documentId, CancellationToken ct)
    { await service.DeleteAsync(propertyId, contractId, documentId, ct); return NoContent(); }
}

[ApiController]
[Authorize(Roles = UserRoles.Tenant)]
[Route("api/me/contracts/{contractId:guid}/documents")]
public sealed class MyContractDocumentsController(ContractDocumentService service) : ControllerBase
{
    private string UserId => User.FindFirst("sub")?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> List(Guid contractId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<ContractDocumentResponse>>(
            (await service.TenantListAsync(contractId, UserId, ct)).Select(ContractDocumentResponse.From).ToList()));

    [HttpGet("{documentId:guid}/content")]
    public async Task<IActionResult> Content(Guid contractId, Guid documentId, CancellationToken ct)
    {
        var (document, stream) = await service.OpenTenantAsync(contractId, documentId, UserId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, document.FileType ?? "application/octet-stream", document.FileName);
    }
}
