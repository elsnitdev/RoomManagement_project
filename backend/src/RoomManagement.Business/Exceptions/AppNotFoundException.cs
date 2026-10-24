namespace RoomManagement.Business.Exceptions;

/// <summary>
/// Ngoại lệ ném ra khi không tìm thấy dữ liệu được yêu cầu.
/// Ví dụ: Admin duyệt một tài khoản có Id không tồn tại.
/// Khi bắt gặp ngoại lệ này, tầng API sẽ trả về HTTP Status 404 Not Found.
/// </summary>
public class AppNotFoundException : Exception
{
    public AppNotFoundException(string message) : base(message)
    {
    }
}
