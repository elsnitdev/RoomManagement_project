using RoomManagement.Business.DTOs.Users;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Mappings;

/// <summary>
/// Các hàm chuyển đổi từ thực thể AppUser sang DTO.
/// Gom về một chỗ để các Service không phải lặp lại đoạn gán từng thuộc tính.
/// Lưu ý: tuyệt đối không đưa PasswordHash ra ngoài.
/// </summary>
public static class UserMappings
{
    /// <summary>Chuyển AppUser sang UserDto (thông tin tài khoản cơ bản).</summary>
    public static UserDto ToUserDto(this AppUser user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        Role = user.Role,
        IsActive = user.IsActive,
        ApprovalStatus = user.ApprovalStatus,
        CreatedAt = user.CreatedAt
    };

    /// <summary>Chuyển AppUser sang LandlordRegistrationDto (dùng cho màn hình duyệt của Admin).</summary>
    public static LandlordRegistrationDto ToLandlordRegistrationDto(this AppUser user) => new()
    {
        UserId = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        CitizenId = user.CitizenId,
        DateOfBirth = user.DateOfBirth,
        Address = user.Address,
        ApprovalStatus = user.ApprovalStatus,
        RejectionReason = user.RejectionReason,
        CreatedAt = user.CreatedAt,
        ReviewedAt = user.ReviewedAt
    };
}
