using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
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

        // Existing administrators are never reset or modified during startup.
        if (await dbContext.Users.AnyAsync(u => u.Role == UserRoles.Admin, cancellationToken))
        {
            logger.LogInformation("Tài khoản quản trị viên (Admin) ban đầu đã tồn tại trong hệ thống. Bỏ qua bước seed.");
            return;
        }

        var userName = _options.UserName?.Trim();
        var email = string.IsNullOrWhiteSpace(_options.Email) ? null : _options.Email.Trim();
        if (string.IsNullOrEmpty(userName) || userName.Length is < 3 or > 50)
            throw new InvalidOperationException("InitialAdmin:UserName must contain between 3 and 50 characters.");
        if (_options.Password.Length < 12)
            throw new InvalidOperationException("InitialAdmin:Password must contain at least 12 characters.");
        if (email is not null && (email.Length > 256 || !new EmailAddressAttribute().IsValid(email)))
            throw new InvalidOperationException("InitialAdmin:Email must be a valid email of at most 256 characters.");

        var normalizedUserName = userName.ToUpperInvariant();
        var normalizedEmail = email?.ToUpperInvariant();
        if (await dbContext.Users.AnyAsync(u => u.NormalizedUserName == normalizedUserName ||
            (normalizedEmail != null && u.NormalizedEmail == normalizedEmail), cancellationToken))
        {
            throw new InvalidOperationException("Initial admin username or email belongs to another account. Choose unused credentials; no account was promoted.");
        }

        // Tạo admin
        var adminUser = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            Email = email,
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
