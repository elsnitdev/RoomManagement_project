using RoomManagement.Business.DTOs.Users;

namespace RoomManagement.Business.Interfaces;

/// <summary>
/// Giao diện định nghĩa các nghiệp vụ liên quan đến quản lý người dùng.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Nghiệp vụ Quản trị viên (Admin) tạo tài khoản và chỉ định vai trò cho người dùng (Landlord hoặc Tenant).
    /// </summary>
    /// <param name="dto">Dữ liệu thông tin tài khoản cần tạo do Admin cung cấp.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ bất đồng bộ nếu cần.</param>
    /// <returns>Thông tin người dùng vừa được tạo thành công (UserDto).</returns>
    Task<UserDto> CreateUserByAdminAsync(CreateUserDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ người dùng tự đăng ký làm chủ trọ.
    /// Tài khoản được tạo ở trạng thái "Chờ duyệt" và chưa thể quản lý khu trọ cho tới khi Admin duyệt.
    /// </summary>
    /// <param name="dto">Thông tin tài khoản và thông tin cá nhân (bắt buộc đầy đủ).</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thông tin tài khoản vừa đăng ký.</returns>
    Task<UserDto> RegisterLandlordAsync(RegisterLandlordDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra hồ sơ cá nhân của người dùng đã đủ các thông tin bắt buộc chưa.
    /// Dùng cho tài khoản do Admin tạo hộ: lần đầu đăng nhập phải gọi để biết có cần bắt điền thêm không.
    /// </summary>
    Task<ProfileStatusDto> GetProfileStatusAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật / bổ sung thông tin cá nhân. Sau khi cập nhật, hồ sơ bắt buộc phải đầy đủ.
    /// </summary>
    Task<ProfileStatusDto> CompleteProfileAsync(string userId, CompleteProfileDto dto, CancellationToken cancellationToken = default);
}
