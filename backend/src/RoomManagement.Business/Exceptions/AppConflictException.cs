namespace RoomManagement.Business.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi tài nguyên bị xung đột dữ liệu trong hệ thống.
/// Ví dụ: Tên đăng nhập (UserName) hoặc Email đã tồn tại.
/// Khi bắt gặp ngoại lệ này, tầng API sẽ trả về HTTP Status 409 Conflict.
/// </summary>
public class AppConflictException : Exception
{
    public AppConflictException(string message) : base(message)
    {
    }
}
