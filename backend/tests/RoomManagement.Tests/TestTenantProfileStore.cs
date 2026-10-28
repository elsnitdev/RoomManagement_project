using System.Collections.Concurrent;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

/// <summary>
/// In-memory fake store (Kho dữ liệu giả lập trên bộ nhớ RAM) phục vụ chạy Unit Tests nhanh cho TenantProfileService
/// mà không cần kết nối tới cơ sở dữ liệu SQL Server thật.
/// </summary>
internal sealed class TestTenantProfileStore : ITenantProfileStore
{
    // Từ điển an toàn đa luồng (ConcurrentDictionary) lưu các hồ sơ người thuê trong RAM theo khóa là Id (Guid)
    public ConcurrentDictionary<Guid, TenantProfile> Profiles { get; } = new();

    // Tập hợp lưu các cặp (LandlordId, ProfileId) giả lập việc người thuê đã ký hợp đồng tại khu trọ của chủ trọ đó
    public HashSet<(string LandlordId, Guid ProfileId)> ContractAccessList { get; } = [];

    // Tìm hồ sơ theo ID trong bộ nhớ RAM (Task.FromResult giúp bọc kết quả thường thành kết quả bất đồng bộ Task)
    public Task<TenantProfile?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(Profiles.GetValueOrDefault(id));
    }

    public Task<TenantProfile?> FindByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(Profiles.GetValueOrDefault(id));
    }

    // Kiểm tra xem có hồ sơ nào trong bộ nhớ đang dùng số CCCD này hay chưa (bỏ qua hồ sơ có Id == excludeId)
    public Task<bool> ExistsByCitizenIdAsync(string citizenId, Guid? excludeId = null, CancellationToken ct = default)
    {
        var exists = Profiles.Values.Any(p =>
            string.Equals(p.CitizenId, citizenId, StringComparison.OrdinalIgnoreCase) &&
            (excludeId == null || p.Id != excludeId.Value));
        return Task.FromResult(exists);
    }

    // Kiểm tra quyền truy cập của chủ trọ: là người tạo hồ sơ HOẶC có trong danh sách hợp đồng giả lập
    public Task<bool> HasAccessPermissionAsync(string landlordId, Guid tenantProfileId, CancellationToken ct = default)
    {
        // Thử lấy hồ sơ từ Dictionary, nếu không có thì trả về false
        if (!Profiles.TryGetValue(tenantProfileId, out var profile))
        {
            return Task.FromResult(false);
        }

        // Nếu chủ trọ là người tạo -> có quyền
        if (profile.CreatedByUserId == landlordId)
        {
            return Task.FromResult(true);
        }

        // Kiểm tra trong danh sách hợp đồng giả lập
        var hasContract = ContractAccessList.Contains((landlordId, tenantProfileId));
        return Task.FromResult(hasContract);
    }

    // Lọc danh sách hồ sơ của chủ trọ theo từ khóa và cắt trang trên bộ nhớ RAM
    public Task<(IReadOnlyList<TenantProfile> Items, int TotalCount)> GetByLandlordAsync(
        string landlordId, string? keyword, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        // 1. Lọc các hồ sơ mà chủ trọ này có quyền xem
        var accessible = Profiles.Values.Where(p =>
            p.CreatedByUserId == landlordId ||
            ContractAccessList.Contains((landlordId, p.Id)));

        // 2. Nếu có từ khóa tìm kiếm thì lọc tiếp theo Họ tên, Số điện thoại hoặc Số CCCD (không phân biệt hoa/thường)
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            accessible = accessible.Where(p =>
                p.FullName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                p.PhoneNumber.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                (p.CitizenId != null && p.CitizenId.Contains(trimmed, StringComparison.OrdinalIgnoreCase)));
        }

        // 3. Đếm tổng số hồ sơ và thực hiện phân trang bằng Skip / Take
        var total = accessible.Count();
        var items = accessible
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<TenantProfile> Items, int TotalCount)>((items, total));
    }

    // Thêm hồ sơ vào Dictionary trong RAM
    public Task AddAsync(TenantProfile profile, CancellationToken ct = default)
    {
        Profiles[profile.Id] = profile;
        return Task.CompletedTask;
    }

    // Mô phỏng thao tác lưu xuống DB: kiểm tra nếu có 2 hồ sơ trùng số CCCD thì ném lỗi xung đột AppConflictException
    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        // Nhóm các hồ sơ có CCCD theo số CCCD, nếu có nhóm nào nhiều hơn 1 bản ghi tức là bị trùng
        var duplicate = Profiles.Values
            .Where(p => !string.IsNullOrEmpty(p.CitizenId))
            .GroupBy(p => p.CitizenId!)
            .Any(g => g.Count() > 1);

        if (duplicate)
        {
            throw new AppConflictException(
                "Số CCCD/CMND đã được sử dụng bởi hồ sơ người thuê khác.",
                "CITIZEN_ID_CONFLICT");
        }

        return Task.CompletedTask;
    }
}
