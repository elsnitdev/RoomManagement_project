using RoomManagement.Business.Errors;

namespace RoomManagement.Business.Services;

internal static class PropertyRoomValidation
{
    public static void Required(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            Invalid(name);
    }
    public static void Optional(string? value, int maxLength, string name)
    {
        if (value?.Trim().Length > maxLength)
            Invalid(name);
    }
    public static void Page(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100
            || ((long)page - 1) * pageSize > int.MaxValue)
            Invalid("Trang hoặc kích thước trang");
    }
    public static void Search(string? search)
    {
        if (search?.Length > 150)
            Invalid("Từ khóa tìm kiếm");
    }
    public static void Room(string? number, string? name, decimal? area, decimal price, int occupants, string? description)
    {
        Required(number, 50, "Mã phòng");
        Optional(name, 100, "Tên phòng");
        Optional(description, 1000, "Mô tả");
        if (area is <= 0 or > 99999999.99m || price is < 0 or > 9999999999999999.99m || occupants < 1)
            Invalid("Diện tích, giá hoặc số người tối đa");
        if (area.HasValue && decimal.Round(area.Value, 2) != area.Value || decimal.Round(price, 2) != price)
            Invalid("Diện tích hoặc giá có quá hai chữ số thập phân");
    }
    private static void Invalid(string name) =>
        throw new BusinessException(BusinessErrorType.Validation, "VALIDATION_ERROR", $"{name} không hợp lệ.");
}
