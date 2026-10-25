using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Stores;

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

    public async Task<AppUser?> FindByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default)
    {
        var matches = await _dbContext.Users
            .Where(u => u.NormalizedUserName == normalizedLogin || u.NormalizedEmail == normalizedLogin)
            .Take(2)
            .ToListAsync(cancellationToken);
        // Username and email each have a unique index, but may collide across accounts.
        return matches.Count == 1 ? matches[0] : null;
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
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 2601 or 2627 } &&
            ex.Entries.Count > 0 && ex.Entries.All(entry => entry.Entity is AppUser))
        {
            // Database uniqueness remains authoritative when requests race past prechecks.
            throw new AppConflictException("Tên đăng nhập, email hoặc số CCCD đã được sử dụng.");
        }
    }
}
