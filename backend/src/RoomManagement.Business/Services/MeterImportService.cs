using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.Business.Services;

public sealed class MeterImportService(ICurrentLandlord current, ILandlordStore landlords,
    IBillingInputStore billing, IMeterImportStore store)
{
    public const int MaxRows = 2000;
    public const int MaxFileBytes = 4 * 1024 * 1024;

    public async Task<MeterImportResult> TemplateAsync(Guid propertyId, short year, byte month, CancellationToken ct)
    {
        var result = await ListAsync(propertyId, year, month, ct);
        if (result.Rows.Count == 0)
            throw Validation("METER_IMPORT_NO_OCCUPIED_ROOMS", "Kỳ này không có phòng đang có người thuê để xuất file.");
        return result;
    }

    public async Task<MeterImportResult> ListAsync(Guid propertyId, short year, byte month, CancellationToken ct)
    {
        var period = Period(year, month);
        await AuthorizeAsync(propertyId, ct);
        var result = await store.InspectAsync(propertyId, period, null, ct);
        if (result.Rows.Count > MaxRows)
            throw Validation("METER_IMPORT_TOO_MANY_ROWS", $"Khu trọ vượt giới hạn {MaxRows} phòng.");
        return result;
    }

    public async Task<MeterImportResult> ValidateAsync(Guid propertyId, short year, byte month,
        IReadOnlyList<MeterImportRowInput> rows, CancellationToken ct)
    {
        var period = Period(year, month);
        await AuthorizeAsync(propertyId, ct);
        ValidateRows(rows);
        return await store.InspectAsync(propertyId, period, rows, ct);
    }

    public async Task<MeterImportResult> CommitAsync(Guid propertyId, short year, byte month,
        IReadOnlyList<MeterImportRowInput> rows, CancellationToken ct)
    {
        var period = Period(year, month);
        var actor = await AuthorizeAsync(propertyId, ct);
        ValidateRows(rows);
        var results = new List<MeterImportRow>(rows.Count);
        foreach (var row in rows)
            results.Add(await store.SaveAsync(propertyId, period, actor, row, ct));
        return new MeterImportResult(propertyId, year, month, results);
    }

    public async Task<OperatorContext> AuthorizeAsync(Guid propertyId, CancellationToken ct)
    {
        var actor = await OperatorAccessGuard.RequireOperatorAsync(current, landlords, ct);
        await OperatorAccessGuard.EnsurePropertyAccessAsync(actor, propertyId, landlords, ct);
        if (!await billing.PropertyOwnedAsync(propertyId, actor.OwnerLandlordUserId, ct))
            throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ đang hoạt động.");
        return actor;
    }

    public static DateOnly Period(short year, byte month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw Validation("METER_IMPORT_PERIOD_INVALID", "Kỳ ghi chỉ số không hợp lệ.");
        return new DateOnly(year, month, 1);
    }

    private static void ValidateRows(IReadOnlyList<MeterImportRowInput> rows)
    {
        if (rows.Count == 0 || rows.Count > MaxRows || rows.Any(x => x is null || x.RoomId == Guid.Empty))
            throw Validation("METER_IMPORT_ROWS_INVALID", $"Cần từ 1 đến {MaxRows} dòng có mã phòng hợp lệ.");
        if (rows.Any(x => x.ContractId == Guid.Empty) ||
            rows.GroupBy(x => x.RoomId).Any(g => g.Count() > 1 &&
                (g.Any(x => x.ContractId is null) || g.Select(x => x.ContractId).Distinct().Count() != g.Count())))
            throw Validation("METER_IMPORT_DUPLICATE_ROOM", "Mỗi dòng phải có cặp phòng và hợp đồng khác nhau.");
    }

    private static BusinessException Validation(string code, string message) =>
        new(BusinessErrorType.Validation, code, message);
}
