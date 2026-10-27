using RoomManagement.Business.Errors;

namespace RoomManagement.Business.Services;

internal static class RowVersionGuard
{
    public static void Verify(string supplied, byte[] current)
    {
        byte[] version;
        try
        {
            version = Convert.FromBase64String(supplied ?? string.Empty);
        }
        catch (FormatException)
        {
            throw InvalidVersion();
        }

        if (version.Length != 8)
            throw InvalidVersion();

        if (!version.AsSpan().SequenceEqual(current))
            throw new BusinessException(BusinessErrorType.Conflict, "ROW_VERSION_CONFLICT",
                "Dữ liệu đã thay đổi. Tải lại bản ghi trước khi cập nhật.");
    }

    private static BusinessException InvalidVersion() =>
        new(BusinessErrorType.Validation, "ROW_VERSION_INVALID", "rowVersion phải là chuỗi Base64 của 8 byte.");
}
