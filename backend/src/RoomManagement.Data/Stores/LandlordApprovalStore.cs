using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Stores;

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
    /// Tìm kiếm tài khoản chủ trọ theo ID (không tracking) để tầng Business có thể quyết định trạng thái
    /// duyệt / từ chối và lưu lại vào cơ sở dữ liệu.
    /// </summary>
    public async Task<AppUser?> FindLandlordByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRoles.Landlord, cancellationToken);
    }

    /// <summary>
    /// Cập nhật nguyên tử các trường xét duyệt, không ghi đè thông tin hồ sơ.
    /// </summary>
    public async Task<bool> TryReviewPendingAsync(AppUser reviewedUser, CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.Users
            .Where(u => u.Id == reviewedUser.Id && u.Role == UserRoles.Landlord &&
                        u.ApprovalStatus == ApprovalStatuses.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.ApprovalStatus, reviewedUser.ApprovalStatus)
                .SetProperty(u => u.IsActive, reviewedUser.IsActive)
                .SetProperty(u => u.RejectionReason, reviewedUser.RejectionReason)
                .SetProperty(u => u.ReviewedAt, reviewedUser.ReviewedAt), cancellationToken);
        return affected == 1;
    }
}
