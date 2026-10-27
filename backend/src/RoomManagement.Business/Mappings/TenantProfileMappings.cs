using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Mappings;

/// <summary>
/// Các hàm mở rộng ánh xạ giữa Entity TenantProfile và DTO.
/// </summary>
public static class TenantProfileMappings
{
    /// <summary>
    /// Chuyển đổi thực thể TenantProfile sang TenantProfileDto.
    /// </summary>
    public static TenantProfileDto ToTenantProfileDto(this TenantProfile profile) => new()
    {
        Id = profile.Id,
        UserId = profile.UserId,
        CreatedByUserId = profile.CreatedByUserId,
        FullName = profile.FullName,
        PhoneNumber = profile.PhoneNumber,
        Email = profile.Email,
        CitizenId = profile.CitizenId,
        DateOfBirth = profile.DateOfBirth,
        Gender = profile.Gender,
        PermanentAddress = profile.PermanentAddress,
        CreatedAt = profile.CreatedAt,
        UpdatedAt = profile.UpdatedAt
    };
}
