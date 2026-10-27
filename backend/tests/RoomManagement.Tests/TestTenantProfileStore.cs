using System.Collections.Concurrent;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

/// <summary>
/// In-memory fake store phục vụ chạy Unit Tests nhanh cho TenantProfileService mà không cần SQL Server.
/// </summary>
internal sealed class TestTenantProfileStore : ITenantProfileStore
{
    public ConcurrentDictionary<Guid, TenantProfile> Profiles { get; } = new();
    public HashSet<(string LandlordId, Guid ProfileId)> ContractAccessList { get; } = [];

    public Task<TenantProfile?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(Profiles.GetValueOrDefault(id));
    }

    public Task<TenantProfile?> FindByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(Profiles.GetValueOrDefault(id));
    }

    public Task<bool> ExistsByCitizenIdAsync(string citizenId, Guid? excludeId = null, CancellationToken ct = default)
    {
        var exists = Profiles.Values.Any(p =>
            string.Equals(p.CitizenId, citizenId, StringComparison.OrdinalIgnoreCase) &&
            (excludeId == null || p.Id != excludeId.Value));
        return Task.FromResult(exists);
    }

    public Task<bool> HasAccessPermissionAsync(string landlordId, Guid tenantProfileId, CancellationToken ct = default)
    {
        if (!Profiles.TryGetValue(tenantProfileId, out var profile))
        {
            return Task.FromResult(false);
        }

        if (profile.CreatedByUserId == landlordId)
        {
            return Task.FromResult(true);
        }

        var hasContract = ContractAccessList.Contains((landlordId, tenantProfileId));
        return Task.FromResult(hasContract);
    }

    public Task<(IReadOnlyList<TenantProfile> Items, int TotalCount)> GetByLandlordAsync(
        string landlordId, string? keyword, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var accessible = Profiles.Values.Where(p =>
            p.CreatedByUserId == landlordId ||
            ContractAccessList.Contains((landlordId, p.Id)));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            accessible = accessible.Where(p =>
                p.FullName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                p.PhoneNumber.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        var total = accessible.Count();
        var items = accessible
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<TenantProfile> Items, int TotalCount)>((items, total));
    }

    public Task AddAsync(TenantProfile profile, CancellationToken ct = default)
    {
        Profiles[profile.Id] = profile;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        // Chống trùng số CCCD khi lưu
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
