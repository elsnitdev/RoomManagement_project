using RoomManagement.Business.Errors;

namespace RoomManagement.Business.Services;

public static class RoomNumbering
{
    public static string Scope(Guid? rowId, int? floor) => rowId is { } id
        ? floor is { } level ? $"R:{id:N}:F:{level}" : $"R:{id:N}"
        : $"F:{floor}";

    public static string Format(string? rowCode, int? floor, int sequence)
    {
        if (sequence is < 1 or > 99 || floor is <= 0 || (rowCode is null && floor is null))
            throw new BusinessException(BusinessErrorType.Validation, "ROOM_NUMBER_INVALID", "Không thể tạo mã phòng từ dãy/tầng đã chọn.");
        var prefix = rowCode?.ToUpperInvariant() ?? "";
        return floor is null ? $"{prefix}{sequence:00}" : $"{prefix}{floor}{sequence:00}";
    }
}
