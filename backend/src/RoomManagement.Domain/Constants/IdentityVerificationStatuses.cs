namespace RoomManagement.Domain.Constants;

/// <summary>
/// Trạng thái xác minh danh tính (SĐT/CCCD) của tài khoản người dùng.
/// </summary>
public static class IdentityVerificationStatuses
{
    public const string Unverified = "Unverified";
    public const string Pending = "Pending";
    public const string Verified = "Verified";
}
