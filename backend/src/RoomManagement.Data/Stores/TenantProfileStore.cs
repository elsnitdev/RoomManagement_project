using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Data.Stores;

/// <summary>
/// Hiện thực hóa interface ITenantProfileStore bằng Entity Framework Core qua RoomManagementDbContext.
/// Chịu trách nhiệm thực thi các truy vấn và thao tác lưu trữ hồ sơ người thuê trên cơ sở dữ liệu SQL Server.
/// </summary>
public sealed class TenantProfileStore : ITenantProfileStore
{
    private readonly RoomManagementDbContext _dbContext;

    public TenantProfileStore(RoomManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Tìm kiếm hồ sơ người thuê theo ID có tracking để phục vụ cập nhật thông tin.
    /// </summary>
    public async Task<TenantProfile?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.TenantProfiles
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>
    /// Tìm kiếm hồ sơ người thuê theo ID không tracking (tối ưu hiệu năng đọc).
    /// </summary>
    public async Task<TenantProfile?> FindByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>
    /// Kiểm tra số CCCD/CMND đã tồn tại trong hệ thống hay chưa (chống trùng lặp CCCD toàn hệ thống).
    /// </summary>
    public async Task<bool> ExistsByCitizenIdAsync(string citizenId, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _dbContext.TenantProfiles
            .AnyAsync(p => p.CitizenId == citizenId && (excludeId == null || p.Id != excludeId.Value), ct);
    }

    /// <summary>
    /// Kiểm tra quyền truy cập của chủ trọ đối với hồ sơ người thuê:
    /// 1. Do chính chủ trọ tạo (CreatedByUserId == landlordId), HOẶC
    /// 2. Đã có hợp đồng thuê tại phòng thuộc khu trọ do chủ trọ sở hữu.
    /// </summary>
    public async Task<bool> HasAccessPermissionAsync(string landlordId, Guid tenantProfileId, CancellationToken ct = default)
    {
        var profile = await _dbContext.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == tenantProfileId, ct);

        if (profile == null)
        {
            return false;
        }

        if (profile.CreatedByUserId == landlordId)
        {
            return true;
        }

        return await (
            from ctItem in _dbContext.ContractTenants
            join c in _dbContext.Contracts on ctItem.ContractId equals c.Id
            join r in _dbContext.Rooms on c.RoomId equals r.Id
            join p in _dbContext.Properties on r.PropertyId equals p.Id
            where ctItem.TenantId == tenantProfileId && p.OwnerUserId == landlordId
            select ctItem.Id
        ).AnyAsync(ct);
    }

    /// <summary>
    /// Lấy danh sách hồ sơ người thuê thuộc quyền quản lý của chủ trọ có phân trang và tìm kiếm theo từ khóa.
    /// </summary>
    public async Task<(IReadOnlyList<TenantProfile> Items, int TotalCount)> GetByLandlordAsync(
        string landlordId, string? keyword, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var contractTenantProfileIds =
            from ctItem in _dbContext.ContractTenants
            join c in _dbContext.Contracts on ctItem.ContractId equals c.Id
            join r in _dbContext.Rooms on c.RoomId equals r.Id
            join prop in _dbContext.Properties on r.PropertyId equals prop.Id
            where prop.OwnerUserId == landlordId
            select ctItem.TenantId;

        var query = _dbContext.TenantProfiles
            .AsNoTracking()
            .Where(p => p.CreatedByUserId == landlordId || contractTenantProfileIds.Contains(p.Id));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(p =>
                p.FullName.Contains(trimmed) ||
                p.PhoneNumber.Contains(trimmed));
        }

        var totalCount = await query.CountAsync(ct);

        var validPageIndex = pageIndex > 0 ? pageIndex : 1;
        var validPageSize = pageSize > 0 ? pageSize : 10;

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((validPageIndex - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    /// <summary>
    /// Thêm hồ sơ người thuê mới vào cơ sở dữ liệu.
    /// </summary>
    public async Task AddAsync(TenantProfile profile, CancellationToken ct = default)
    {
        await _dbContext.TenantProfiles.AddAsync(profile, ct);
    }

    /// <summary>
    /// Lưu các thay đổi vào cơ sở dữ liệu.
    /// Bắt ngoại lệ SQL Server 2601 hoặc 2627 khi vi phạm ràng buộc unique trên CitizenId.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 2601 or 2627 } &&
            ex.Entries.Count > 0 && ex.Entries.All(entry => entry.Entity is TenantProfile))
        {
            throw new AppConflictException("Số CCCD/CMND đã được sử dụng bởi hồ sơ người thuê khác.", "CITIZEN_ID_CONFLICT");
        }
    }
}
