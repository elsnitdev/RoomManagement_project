using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Infrastructure;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Services;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.LandlordOrManager)]
[Route("api/properties/{propertyId:guid}/meter-reading-import")]
public sealed class MeterReadingImportController(MeterImportService service) : ControllerBase
{
    [HttpGet("readings")]
    public async Task<ApiResponse<MeterImportResult>> Readings(Guid propertyId, [FromQuery] short year,
        [FromQuery] byte month, CancellationToken ct) =>
        new(await service.ListAsync(propertyId, year, month, ct));

    [HttpGet("template")]
    public async Task<IActionResult> Template(Guid propertyId, [FromQuery] short year, [FromQuery] byte month,
        CancellationToken ct)
    {
        var result = await service.TemplateAsync(propertyId, year, month, ct);
        return File(MeterImportWorkbook.Create(result),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"chi-so-dien-nuoc-{year:D4}-{month:D2}.xlsx");
    }

    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MeterImportService.MaxFileBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = MeterImportService.MaxFileBytes)]
    public async Task<ApiResponse<MeterImportResult>> Preview(Guid propertyId, [FromQuery] short year,
        [FromQuery] byte month, [FromForm] IFormFile file, CancellationToken ct)
    {
        MeterImportService.Period(year, month);
        await service.AuthorizeAsync(propertyId, ct);
        if (file.Length <= 0 || file.Length > MeterImportService.MaxFileBytes ||
            !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw BusinessException.BadRequest("Chọn file Excel .xlsx không quá 4 MB.", "METER_IMPORT_FILE_INVALID");

        await using var stream = file.OpenReadStream();
        var parsed = MeterImportWorkbook.Read(stream, propertyId, year, month);
        var inspected = await service.ValidateAsync(propertyId, year, month, parsed.Select(x => x.Input).ToList(), ct);
        return new(inspected with { Rows = inspected.Rows.Select((row, index) =>
        {
            var source = parsed[index];
            return row with
            {
                ExcelRow = source.ExcelRow,
                Electricity = WithExcelError(row.Electricity, source.ElectricityError),
                Water = WithExcelError(row.Water, source.WaterError),
            };
        }).ToList() });
    }

    [HttpPost("validate")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<ApiResponse<MeterImportResult>> Validate(Guid propertyId,
        [FromBody] MeterImportBatchRequest request, CancellationToken ct) =>
        new(await service.ValidateAsync(propertyId, request.Year, request.Month, request.Rows, ct));

    [HttpPost("commit")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<ApiResponse<MeterImportResult>> Commit(Guid propertyId,
        [FromBody] MeterImportBatchRequest request, CancellationToken ct) =>
        new(await service.CommitAsync(propertyId, request.Year, request.Month, request.Rows, ct));

    private static MeterImportCell WithExcelError(MeterImportCell cell, string? error) =>
        error is null ? cell : cell with
        {
            RecordedValue = cell.Status == "AlreadyRecorded" ? cell.CurrentValue : cell.RecordedValue,
            CurrentValue = null,
            Status = "Error", Code = "METER_IMPORT_CELL_INVALID", Message = error,
        };
}

public sealed record MeterImportBatchRequest(short Year, byte Month, IReadOnlyList<MeterImportRowInput> Rows);
