namespace RoomManagement.Business.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi dữ liệu đầu vào không hợp lệ với quy tắc nghiệp vụ.
/// Ví dụ: Chọn vai trò (Role) không được phép, mật khẩu không đúng chính sách...
/// Khi bắt gặp ngoại lệ này, tầng API sẽ trả về HTTP Status 400 Bad Request.
/// </summary>
public class AppValidationException : Exception
{
    public AppValidationException(string message) : base(message)
    {
    }
}
