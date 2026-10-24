using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

/// <summary>
/// Service triển khai các logic nghiệp vụ liên quan đến tài khoản người dùng.
/// </summary>
public sealed class UserService : IUserService
{
    private readonly RoomManagementDbContext _dbContext;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly ILogger<UserService> _logger;

    public UserService(
        RoomManagementDbContext dbContext,
        IPasswordHasher<AppUser> passwordHasher,
        ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Xử lý tạo tài khoản mới từ phía Quản trị viên (Admin) và phân quyền Landlord hoặc Tenant.
    /// </summary>
    /// <param name="dto">Dữ liệu tài khoản do Admin cung cấp.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ bất đồng bộ.</param>
    /// <returns>Đối tượng UserDto chứa thông tin người dùng vừa được tạo.</returns>
    /// <exception cref="AppValidationException">Ném ra khi dữ liệu nhập vào vi phạm quy tắc nghiệp vụ (sai Role, mật khẩu yếu...).</exception>
    /// <exception cref="AppConflictException">Ném ra khi Tên đăng nhập hoặc Email đã tồn tại trong hệ thống.</exception>
    public async Task<UserDto> CreateUserByAdminAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        // -------------------------------------------------------------------------
        // BƯỚC 1: KIỂM TRA TÍNH HỢP LỆ CỦA CÁC TRƯỜNG DỮ LIỆU CƠ BẢN
        // -------------------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(dto.UserName))
        {
            throw new AppValidationException("Tên đăng nhập (UserName) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new AppValidationException("Mật khẩu ban đầu không được để trống.");
        }

        if (dto.Password.Length < 6)
        {
            throw new AppValidationException("Mật khẩu phải có độ dài tối thiểu từ 6 ký tự trở lên để đảm bảo an toàn.");
        }

        // -------------------------------------------------------------------------
        // BƯỚC 2: RÀNG BUỘC VAI TRÒ (ROLE) - CHẶN LEO THANG ĐẶC QUYỀN
        // Admin chỉ được phép gán: "Landlord" (Chủ trọ) hoặc "Tenant" (Khách thuê).
        // -------------------------------------------------------------------------
        var trimmedRole = dto.Role?.Trim() ?? string.Empty;

        // Nếu cố tình tạo thêm Admin qua luồng này thì từ chối ngay lập tức
        if (string.Equals(trimmedRole, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Admin cố tình tạo thêm tài khoản có quyền Admin thông qua API tạo người dùng thường.");
            throw new AppValidationException("Không được phép tạo thêm tài khoản Admin qua chức năng này. Chỉ cho phép chỉ định vai trò 'Landlord' hoặc 'Tenant'.");
        }

        // Kiểm tra xem vai trò có khớp với Landlord hoặc Tenant không
        string assignedRole;
        if (string.Equals(trimmedRole, UserRoles.Landlord, StringComparison.OrdinalIgnoreCase))
        {
            assignedRole = UserRoles.Landlord;
        }
        else if (string.Equals(trimmedRole, UserRoles.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            assignedRole = UserRoles.Tenant;
        }
        else
        {
            throw new AppValidationException($"Vai trò '{dto.Role}' không hợp lệ. Vui lòng chỉ chọn một trong hai vai trò: '{UserRoles.Landlord}' (Chủ trọ) hoặc '{UserRoles.Tenant}' (Khách thuê).");
        }

        // -------------------------------------------------------------------------
        // BƯỚC 3: CHUẨN HÓA DỮ LIỆU ĐỂ PHỤC VỤ TÌM KIẾM VÀ SO SÁNH DUY NHẤT
        // -------------------------------------------------------------------------
        var normalizedUserName = dto.UserName.Trim().ToUpperInvariant();
        var normalizedEmail = string.IsNullOrWhiteSpace(dto.Email)
            ? null
            : dto.Email.Trim().ToUpperInvariant();

        // -------------------------------------------------------------------------
        // BƯỚC 4: KIỂM TRA TRÙNG LẶP DỮ LIỆU TRONG DATABASE (USERNAME VÀ EMAIL)
        // -------------------------------------------------------------------------
        // 4.1. Kiểm tra tên đăng nhập đã tồn tại chưa
        var isUserNameTaken = await _dbContext.Users.AnyAsync(
            u => u.NormalizedUserName == normalizedUserName,
            cancellationToken);

        if (isUserNameTaken)
        {
            _logger.LogWarning("Thao tác tạo người dùng thất bại: Tên đăng nhập '{UserName}' đã tồn tại.", dto.UserName);
            throw new AppConflictException($"Tên đăng nhập '{dto.UserName.Trim()}' đã có người sử dụng. Vui lòng chọn tên đăng nhập khác.");
        }

        // 4.2. Kiểm tra email đã tồn tại chưa (nếu có cung cấp email)
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var isEmailTaken = await _dbContext.Users.AnyAsync(
                u => u.NormalizedEmail == normalizedEmail,
                cancellationToken);

            if (isEmailTaken)
            {
                _logger.LogWarning("Thao tác tạo người dùng thất bại: Email '{Email}' đã tồn tại.", dto.Email);
                throw new AppConflictException($"Địa chỉ email '{dto.Email.Trim()}' đã được liên kết với một tài khoản khác trong hệ thống.");
            }
        }

        // -------------------------------------------------------------------------
        // BƯỚC 5: KHỞI TẠO THỰC THỂ NGƯỜI DÙNG (APPUSER)
        // -------------------------------------------------------------------------
        var newUser = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = dto.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Role = assignedRole,
            IsActive = true, // Tài khoản do Admin tạo được kích hoạt sẵn để sử dụng ngay
            CreatedAt = DateTime.UtcNow
        };

        // -------------------------------------------------------------------------
        // BƯỚC 6: BĂM MẬT KHẨU (PASSWORD HASHING) AN TOÀN TRƯỚC KHI LƯU
        // Tuyệt đối không lưu mật khẩu dạng văn bản rõ (plain text) vào cơ sở dữ liệu.
        // -------------------------------------------------------------------------
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, dto.Password);

        // -------------------------------------------------------------------------
        // BƯỚC 7: LƯU THÔNG TIN VÀO CƠ SỞ DỮ LIỆU THÔNG QUA EF CORE
        // -------------------------------------------------------------------------
        await _dbContext.Users.AddAsync(newUser, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin đã tạo thành công tài khoản: {UserName} (ID: {UserId}) với vai trò '{Role}'",
            newUser.UserName, newUser.Id, newUser.Role);

        // -------------------------------------------------------------------------
        // BƯỚC 8: ÁNH XẠ THỰC THỂ SANG USERDTO TRẢ VỀ (LOẠI BỎ PASSWORDHASH)
        // -------------------------------------------------------------------------
        return new UserDto
        {
            Id = newUser.Id,
            UserName = newUser.UserName,
            Email = newUser.Email,
            Role = newUser.Role,
            IsActive = newUser.IsActive,
            CreatedAt = newUser.CreatedAt
        };
    }
}
