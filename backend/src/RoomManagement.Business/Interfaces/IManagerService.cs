using RoomManagement.Business.DTOs;
using RoomManagement.Business.DTOs.Managers;

namespace RoomManagement.Business.Interfaces;

public interface IManagerService
{
    Task<ManagerDto> CreateAsync(CreateManagerInput input, CancellationToken ct = default);
    Task<PageResult<ManagerDto>> ListAsync(ManagerFilter filter, CancellationToken ct = default);
    Task<ManagerDto> GetAsync(string managerId, CancellationToken ct = default);
    Task<ManagerDto> AssignPropertiesAsync(string managerId, IReadOnlyList<Guid> propertyIds, CancellationToken ct = default);
    Task RevokePropertyAsync(string managerId, Guid propertyId, CancellationToken ct = default);
    Task<ManagerDto> SetLockStateAsync(string managerId, bool isActive, CancellationToken ct = default);
    Task DeleteAsync(string managerId, CancellationToken ct = default);
}
