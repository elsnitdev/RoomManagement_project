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
}
