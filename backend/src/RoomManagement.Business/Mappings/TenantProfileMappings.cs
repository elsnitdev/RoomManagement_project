using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Mappings;

/// <summary>
/// Lớp tĩnh chứa các phương thức mở rộng (Extension Methods) giúp chuyển đổi qua lại giữa Entity và DTO.
/// </summary>
public static class TenantProfileMappings
{
    /// <summary>
    /// Chuyển đổi thực thể TenantProfile (tầng Domain) sang TenantProfileDto (tầng Business).
    /// Từ khóa 'this' trước tham số đầu tiên biến hàm này thành Extension Method,
    /// cho phép gọi trực tiếp dạng: profile.ToTenantProfileDto()
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
