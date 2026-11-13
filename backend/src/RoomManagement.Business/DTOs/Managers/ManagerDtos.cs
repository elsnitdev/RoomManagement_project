namespace RoomManagement.Business.DTOs.Managers;

public sealed record ManagerAssignedPropertyDto(Guid PropertyId, string PropertyName, DateTime AssignedAt);

public sealed record ManagerDto(
    string Id,
    string UserName,
    string? Email,
    string? FullName,
    string? PhoneNumber,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    IReadOnlyList<ManagerAssignedPropertyDto> AssignedProperties);

public sealed record CreateManagerInput(
    string UserName,
    string Password,
    string? Email,
    string? FullName,
    string? PhoneNumber,
    IReadOnlyList<Guid>? PropertyIds);

public sealed record ManagerFilter(int Page, int PageSize, string? Search, bool? IsActive);
