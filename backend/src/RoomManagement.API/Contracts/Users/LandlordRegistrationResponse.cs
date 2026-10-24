using RoomManagement.Business.DTOs.Users;

namespace RoomManagement.API.Contracts.Users;

/// <summary>
/// Dữ liệu một yêu cầu đăng ký chủ trọ trả về cho màn hình xét duyệt của Admin.
/// </summary>
public sealed class LandlordRegistrationResponse
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }

    public string? FullName { get; init; }
    public string? PhoneNumber { get; init; }
    public string? CitizenId { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Address { get; init; }

    /// <summary>Pending / Approved / Rejected.</summary>
    public string ApprovalStatus { get; init; } = string.Empty;
    public string? RejectionReason { get; init; }

    /// <summary>Thời điểm gửi yêu cầu (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Thời điểm Admin xử lý (UTC).</summary>
    public DateTime? ReviewedAt { get; init; }

    /// <summary>
    /// Chuyển từ DTO tầng Business sang dữ liệu trả về cho Client.
    /// </summary>
    public static LandlordRegistrationResponse FromDto(LandlordRegistrationDto dto) => new()
    {
        UserId = dto.UserId,
        UserName = dto.UserName,
        Email = dto.Email,
        FullName = dto.FullName,
        PhoneNumber = dto.PhoneNumber,
        CitizenId = dto.CitizenId,
        DateOfBirth = dto.DateOfBirth,
        Address = dto.Address,
        ApprovalStatus = dto.ApprovalStatus,
        RejectionReason = dto.RejectionReason,
        CreatedAt = dto.CreatedAt,
        ReviewedAt = dto.ReviewedAt
    };
}
