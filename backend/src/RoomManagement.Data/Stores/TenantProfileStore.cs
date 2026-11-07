using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data.Stores;

/// <summary>
/// Hiện thực hóa interface ITenantProfileStore bằng Entity Framework Core qua RoomManagementDbContext.
/// Chịu trách nhiệm thực thi các câu lệnh truy vấn (LINQ to SQL) và thao tác lưu trữ hồ sơ người thuê trên SQL Server.
/// </summary>
public sealed class TenantProfileStore : ITenantProfileStore
{
    // Biến giữ kết nối và phiên làm việc với cơ sở dữ liệu (Dependency Injection)
    private readonly RoomManagementDbContext _dbContext;

    // Hàm khởi tạo (Constructor): .NET tự động truyền RoomManagementDbContext vào đây khi khởi tạo Store
    public TenantProfileStore(RoomManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Tìm kiếm hồ sơ người thuê theo ID CÓ tracking (theo dõi thay đổi) để phục vụ cập nhật thông tin.
    /// Khi sửa thuộc tính của đối tượng trả về và gọi SaveChangesAsync, EF Core sẽ tự sinh câu lệnh UPDATE.
    /// </summary>
    public async Task<TenantProfile?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        // FirstOrDefaultAsync: trả về bản ghi đầu tiên khớp Id, hoặc trả về null nếu không tìm thấy
        return await _dbContext.TenantProfiles
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>
    /// Tìm kiếm hồ sơ người thuê theo ID KHÔNG tracking (tối ưu hiệu năng khi chỉ đọc dữ liệu).
    /// </summary>
    public async Task<TenantProfile?> FindByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default)
    {
        // AsNoTracking(): báo cho EF Core không cần lưu bản sao đối tượng vào bộ nhớ theo dõi, giúp đọc nhanh hơn
        return await _dbContext.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>
    /// Kiểm tra số CCCD/CMND đã tồn tại trong hệ thống hay chưa (chống trùng lặp CCCD toàn hệ thống).
    /// </summary>
    public async Task<bool> ExistsByCitizenIdAsync(string landlordId, string citizenId, Guid? excludeId = null, CancellationToken ct = default)
    {
        // AnyAsync: tương đương câu lệnh IF EXISTS trong SQL, trả về true nếu có ít nhất 1 bản ghi thỏa mãn
        // (excludeId == null || p.Id != excludeId.Value): nếu đang sửa hồ sơ thì bỏ qua chính hồ sơ đang sửa đó
        return await _dbContext.TenantProfiles
            .AnyAsync(p => p.CreatedByUserId == landlordId && p.CitizenId == citizenId && (excludeId == null || p.Id != excludeId.Value), ct);
    }

    /// <summary>
    /// Kiểm tra quyền truy cập của chủ trọ đối với hồ sơ người thuê:
    /// 1. Do chính chủ trọ tạo (CreatedByUserId == landlordId), HOẶC
    /// 2. Đã có hợp đồng thuê tại phòng thuộc khu trọ do chủ trọ sở hữu.
    /// </summary>
    public async Task<bool> HasAccessPermissionAsync(string landlordId, Guid tenantProfileId, CancellationToken ct = default)
    {
        return await _dbContext.TenantProfiles.AsNoTracking()
            .AnyAsync(p => p.Id == tenantProfileId && p.CreatedByUserId == landlordId, ct);
    }

    public Task<bool> IsContractContactAsync(Guid tenantProfileId, CancellationToken ct = default) =>
        _dbContext.ContractTenants.AnyAsync(link => link.TenantId == tenantProfileId &&
            link.IsContactPerson && link.LeaveDate == null &&
            _dbContext.Contracts.Any(c => c.Id == link.ContractId &&
                (c.Status == ContractStatus.Draft || c.Status == ContractStatus.Active)), ct);

    /// <summary>
    /// Lấy danh sách hồ sơ người thuê thuộc quyền quản lý của chủ trọ có phân trang và tìm kiếm theo từ khóa.
    /// </summary>
    public async Task<(IReadOnlyList<TenantProfile> Items, int TotalCount)> GetByLandlordAsync(
        string landlordId, string? keyword, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        // Mỗi chủ trọ chỉ nhìn thấy hồ sơ do mình tạo.
        var query = _dbContext.TenantProfiles
            .AsNoTracking()
            .Where(p => p.CreatedByUserId == landlordId);

        // Nếu người dùng có nhập từ khóa tìm kiếm (khác null và không phải toàn khoảng trắng)
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // Cắt bỏ khoảng trắng thừa ở 2 đầu từ khóa
            var trimmed = keyword.Trim();

            // Nối thêm điều kiện WHERE LIKE '%trimmed%' cho Họ tên, Số điện thoại hoặc Số CCCD
            query = query.Where(p =>
                p.FullName.Contains(trimmed) ||
                (p.PhoneNumber != null && p.PhoneNumber.Contains(trimmed)) ||
                (p.CitizenId != null && p.CitizenId.Contains(trimmed)));
        }

        // Thực thi câu lệnh SELECT COUNT(*) xuống SQL để biết tổng số bản ghi thỏa mãn điều kiện lọc
        var totalCount = await query.CountAsync(ct);

        // Đảm bảo số trang >= 1 và kích thước trang >= 1 để tránh lỗi khi tính vị trí phân trang
        var validPageIndex = pageIndex > 0 ? pageIndex : 1;
        var validPageSize = pageSize > 0 ? pageSize : 10;

        // Sắp xếp hồ sơ mới tạo lên đầu (OrderByDescending), bỏ qua các trang trước (Skip),
        // lấy đúng số lượng của trang hiện tại (Take) và thực thi truy vấn lấy danh sách (ToListAsync)
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((validPageIndex - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync(ct);

        // Trả về một Tuple (cặp giá trị) gồm danh sách bản ghi của trang hiện tại và tổng số bản ghi
        return (items, totalCount);
    }

    /// <summary>
    /// Đánh dấu thêm hồ sơ người thuê mới vào DbContext (chưa ghi xuống DB cho tới khi gọi SaveChangesAsync).
    /// </summary>
    public async Task AddAsync(TenantProfile profile, CancellationToken ct = default)
    {
        await _dbContext.TenantProfiles.AddAsync(profile, ct);
    }

    /// <summary>
    /// Lưu thực sự tất cả các thay đổi (INSERT / UPDATE) xuống cơ sở dữ liệu SQL Server.
    /// Bắt ngoại lệ SQL Server mã 2601 hoặc 2627 khi vi phạm ràng buộc Unique Index trên cột CitizenId.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            // Gửi lệnh INSERT/UPDATE xuống SQL Server
            await _dbContext.SaveChangesAsync(ct);
        }
        // Từ khóa 'when' trong C#: chỉ bắt lỗi DbUpdateException nếu nguyên nhân bên trong là lỗi trùng lặp của SQL Server (2601 hoặc 2627)
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 2601 or 2627 } &&
            ex.Entries.Count > 0 && ex.Entries.All(entry => entry.Entity is TenantProfile))
        {
            // Chuyển đổi lỗi kỹ thuật của SQL thành lỗi nghiệp vụ AppConflictException (tương ứng HTTP 409 Conflict)
            throw new AppConflictException("Số CCCD/CMND đã được sử dụng bởi hồ sơ người thuê khác.", "CITIZEN_ID_CONFLICT");
        }
    }
}
