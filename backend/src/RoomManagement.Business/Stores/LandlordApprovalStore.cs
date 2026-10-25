using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Stores;

/// <summary>
/// Hiện thực hóa interface ILandlordApprovalStore sử dụng Entity Framework Core và RoomManagementDbContext.
/// Chịu trách nhiệm thực thi các câu truy vấn và lưu dữ liệu trực tiếp với SQL Server.
/// </summary>
public sealed class LandlordApprovalStore : ILandlordApprovalStore
{
    private readonly RoomManagementDbContext _dbContext;

    public LandlordApprovalStore(RoomManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lấy danh sách các tài khoản chủ trọ đang ở trạng thái Chờ duyệt.
    /// Dùng AsNoTracking để tối ưu hiệu năng đọc (không cần quản lý trạng thái entity).
    /// </summary>
    public async Task<IReadOnlyList<AppUser>> GetPendingLandlordsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRoles.Landlord && u.ApprovalStatus == ApprovalStatuses.Pending)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Tìm kiếm tài khoản chủ trọ theo ID (có tracking) để tầng Business có thể cập nhật trạng thái
    /// duyệt / từ chối và lưu lại vào cơ sở dữ liệu.
    /// </summary>
    public async Task<AppUser?> FindLandlordByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRoles.Landlord, cancellationToken);
    }

    /// <summary>
    /// Lưu tất cả thay đổi trên DbContext vào cơ sở dữ liệu.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
