using System.ComponentModel.DataAnnotations;

namespace RoomManagement.API.Contracts.Users;

/// <summary>
/// Yêu cầu Admin gửi lên khi từ chối một đơn đăng ký chủ trọ.
/// </summary>
public sealed class RejectLandlordRequest
{
    /// <summary>Lý do từ chối (bắt buộc, tối đa 500 ký tự).</summary>
    [Required(ErrorMessage = "Vui lòng nhập lý do từ chối.")]
    [StringLength(500, ErrorMessage = "Lý do từ chối không được vượt quá 500 ký tự.")]
    public string Reason { get; init; } = string.Empty;
}
