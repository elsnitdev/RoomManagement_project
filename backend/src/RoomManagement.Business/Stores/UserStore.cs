using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Data;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Stores;

/// <summary>
/// Hiện thực hóa interface IUserStore bằng Entity Framework Core qua RoomManagementDbContext.
/// Chịu trách nhiệm thực thi các truy vấn và thao tác lưu trữ với cơ sở dữ liệu.
/// </summary>
public sealed class UserStore : IUserStore
{
    private readonly RoomManagementDbContext _dbContext;

    public UserStore(RoomManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Kiểm tra xem tên đăng nhập (đã chuẩn hóa) đã tồn tại trong DB hay chưa.
    /// </summary>
    public async Task<bool> ExistsByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.AnyAsync(
            u => u.NormalizedUserName == normalizedUserName,
            cancellationToken);
    }

    /// <summary>
    /// Kiểm tra xem email (đã chuẩn hóa) đã tồn tại trong DB hay chưa.
    /// </summary>
    public async Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.AnyAsync(
            u => u.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    /// <summary>
    /// Kiểm tra số CCCD đã được dùng bởi tài khoản khác hay chưa.
    /// </summary>
    public async Task<bool> ExistsByCitizenIdAsync(string citizenId, string? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.AnyAsync(
            u => u.CitizenId == citizenId && u.Id != excludeUserId,
            cancellationToken);
    }

    /// <summary>
    /// Tìm người dùng theo ID không theo dõi thay đổi (tối ưu hiệu năng đọc).
    /// </summary>
    public async Task<AppUser?> FindByIdAsNoTrackingAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    /// <summary>
    /// Tìm người dùng theo ID có tracking để cập nhật trạng thái/thông tin cá nhân.
    /// </summary>
    public async Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    /// <summary>
    /// Thêm người dùng mới vào DbContext.
    /// </summary>
    public async Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    /// <summary>
    /// Lưu các thay đổi vào cơ sở dữ liệu.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
