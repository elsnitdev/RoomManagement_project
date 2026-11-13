using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

public sealed record RulesEvidenceResponse(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt, int VersionNumber, bool IsLegacyContractRules)
{
    public static RulesEvidenceResponse From(RulesEvidenceItem x) => new(x.Evidence.Id, x.Evidence.OriginalFileName,
        x.Evidence.ContentType, x.Evidence.SizeBytes, x.Evidence.CreatedAt, x.VersionNumber, x.IsLegacyContractRules);
}

[ApiController]
[Authorize(Roles = UserRoles.Tenant)]
[Route("api/me/contract-rules/{contractId:guid}/tenants/{linkId:guid}/evidence")]
public sealed class MyRulesEvidenceController(ContractRulesEvidenceService service) : ControllerBase
{
    private string UserId => User.FindFirst("sub")?.Value ?? "";
    [HttpGet]
    public async Task<IActionResult> List(Guid contractId, Guid linkId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<RulesEvidenceResponse>>((await service.TenantListAsync(contractId, linkId, UserId, ct)).Select(RulesEvidenceResponse.From).ToList()));

    [HttpPost]
    [RequestSizeLimit(ContractRulesEvidenceService.MaxFileBytes + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid contractId, Guid linkId, [FromForm] IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var evidence = await service.UploadAsync(contractId, linkId, UserId, file.FileName, file.Length, stream, ct);
        var item = (await service.TenantListAsync(contractId, linkId, UserId, ct)).Single(x => x.Evidence.Id == evidence.Id);
        return StatusCode(201, new ApiResponse<RulesEvidenceResponse>(RulesEvidenceResponse.From(item)));
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid contractId, Guid linkId, Guid id, CancellationToken ct)
    {
        var (evidence, stream) = await service.OpenAsync(await service.TenantListAsync(contractId, linkId, UserId, ct), id, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, evidence.ContentType, evidence.OriginalFileName);
    }
}

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/properties/{propertyId:guid}/contracts/{contractId:guid}/tenants/{linkId:guid}/rules-evidence")]
public sealed class LandlordRulesEvidenceController(ContractRulesEvidenceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId, Guid contractId, Guid linkId, CancellationToken ct) =>
        Ok(new ApiResponse<IReadOnlyList<RulesEvidenceResponse>>((await service.OwnerListAsync(propertyId, contractId, linkId, ct)).Select(RulesEvidenceResponse.From).ToList()));

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid propertyId, Guid contractId, Guid linkId, Guid id, CancellationToken ct)
    {
        var (evidence, stream) = await service.OpenAsync(await service.OwnerListAsync(propertyId, contractId, linkId, ct), id, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, evidence.ContentType, evidence.OriginalFileName);
    }
}
