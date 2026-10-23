using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Seeders;

public sealed class AdminUserSeeder(
    RoomManagementDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IOptions<InitialAdminOptions> options,
    ILogger<AdminUserSeeder> logger)
{
    private readonly InitialAdminOptions _options = options.Value;

    // Tạo tài khoản admin ban đầu
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Kiểm tra mật khẩu admin
        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            logger.LogWarning("Chưa cấu hình mật khẩu cho Initial Admin (InitialAdmin:Password trống). Bỏ qua việc tạo tài khoản Admin ban đầu.");
            return;
        }

        // Chuẩn hóa username và email
        var normalizedUserName = _options.UserName.Trim().ToUpperInvariant();
        var normalizedEmail = string.IsNullOrWhiteSpace(_options.Email)
            ? null
            : _options.Email.Trim().ToUpperInvariant();

        // Kiểm tra xem đã tồn tại tài khoản Admin hoặc tài khoản có cùng UserName chưa
        var exists = await dbContext.Users.AnyAsync(
            u => u.NormalizedUserName == normalizedUserName || u.Role == UserRoles.Admin,
            cancellationToken);

        // Nếu đã tồn tại thì return
        if (exists)
        {
            logger.LogInformation("Tài khoản quản trị viên (Admin) ban đầu đã tồn tại trong hệ thống. Bỏ qua bước seed.");
            return;
        }

        // Tạo admin
        var adminUser = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = _options.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            Email = _options.Email?.Trim(),
            NormalizedEmail = normalizedEmail,
            Role = UserRoles.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Hash mật khẩu
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, _options.Password);

        // Thêm admin vào database
        await dbContext.Users.AddAsync(adminUser, cancellationToken);
        // Lưu thay đổi vào database
        await dbContext.SaveChangesAsync(cancellationToken);

        // Ghi log
        logger.LogInformation("Khởi tạo thành công tài khoản Initial Admin: {UserName} ({Email}) với quyền {Role}", adminUser.UserName, adminUser.Email, adminUser.Role);
    }
}
