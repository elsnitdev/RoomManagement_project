namespace RoomManagement.Business.DTOs;

public sealed record MeterImportRowInput(Guid RoomId, decimal? Electricity, decimal? Water,
    decimal? ExpectedElectricityPrevious = null, decimal? ExpectedWaterPrevious = null,
    bool ElectricityManual = false, bool WaterManual = false,
    decimal? ExpectedElectricityCurrent = null, decimal? ExpectedWaterCurrent = null,
    decimal? InitialElectricityPrevious = null, decimal? InitialWaterPrevious = null,
    Guid? ContractId = null);

public sealed record MeterImportCell(string Status, decimal? PreviousValue, decimal? CurrentValue,
    string? Code = null, string? Message = null, Guid? ReadingId = null, decimal? RecordedValue = null,
    bool CanEnterOpening = false)
{
    public decimal? Consumption => PreviousValue is { } start && CurrentValue is { } end && end >= start
        ? end - start : null;
}

public sealed record MeterImportRow(Guid RoomId, string RoomNumber, int? ExcelRow, MeterImportCell Electricity,
    MeterImportCell Water, Guid? ContractId = null, string? ContractCode = null);

public sealed record MeterImportResult(Guid PropertyId, short Year, byte Month, IReadOnlyList<MeterImportRow> Rows);

public sealed record ParsedMeterImportRow(MeterImportRowInput Input, int ExcelRow,
    string? ElectricityError, string? WaterError);
