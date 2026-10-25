using Microsoft.Extensions.Logging;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Mappings;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

/// <summary>
/// Service xử lý việc Admin xem, duyệt hoặc từ chối các yêu cầu đăng ký chủ trọ.
/// Tuân thủ quy chuẩn Clean Architecture: Tầng Business kiểm tra logic nghiệp vụ, trạng thái,
/// và giao tiếp với dữ liệu thông qua ILandlordApprovalStore thay vì gọi DbContext trực tiếp.
/// </summary>
public sealed class LandlordApprovalService : ILandlordApprovalService
{
    /// <summary>Độ dài tối đa của lý do từ chối (khớp với ràng buộc CHECK/nvarchar(500) trong DB).</summary>
    private const int MaxRejectionReasonLength = 500;

    private readonly ILandlordApprovalStore _approvalStore;
    private readonly ILogger<LandlordApprovalService> _logger;

    public LandlordApprovalService(
        ILandlordApprovalStore approvalStore,
        ILogger<LandlordApprovalService> logger)
    {
        _approvalStore = approvalStore;
        _logger = logger;
    }

    /// <summary>
    /// Lấy toàn bộ yêu cầu đăng ký chủ trọ đang chờ duyệt, yêu cầu gửi sớm nhất xếp trước.
    /// Dữ liệu được ánh xạ sang DTO trước khi trả về.
    /// </summary>
    public async Task<IReadOnlyList<LandlordRegistrationDto>> GetPendingRegistrationsAsync(CancellationToken cancellationToken = default)
    {
        // Gọi store để lấy danh sách Entity AppUser với điều kiện Role = Landlord và ApprovalStatus = Pending
        var pendingLandlords = await _approvalStore.GetPendingLandlordsAsync(cancellationToken);

        // Ánh xạ sang danh sách DTO để không lộ Entity ra ngoài
        return pendingLandlords.Select(u => u.ToLandlordRegistrationDto()).ToList();
    }

    /// <summary>
    /// Duyệt yêu cầu đăng ký: chuyển sang Approved và kích hoạt tài khoản.
    /// Từ thời điểm này tài khoản chính thức có quyền Landlord và có thể quản lý khu trọ.
    /// </summary>
    /// <exception cref="AppNotFoundException">Không tìm thấy yêu cầu đăng ký.</exception>
    /// <exception cref="AppConflictException">Yêu cầu đã được xử lý trước đó.</exception>
    public async Task<LandlordRegistrationDto> ApproveAsync(string userId, CancellationToken cancellationToken = default)
    {
        // BƯỚC 1: Lấy yêu cầu qua Store và đảm bảo nó vẫn đang ở trạng thái Pending
        var landlord = await GetPendingLandlordAsync(userId, cancellationToken);

        // BƯỚC 2: Cập nhật các trạng thái nghiệp vụ
        landlord.ApprovalStatus = ApprovalStatuses.Approved;
        landlord.IsActive = true;            // Mở khóa tài khoản để chủ trọ đăng nhập và hoạt động
        landlord.RejectionReason = null;     // Xóa lý do từ chối (nếu có trước đó) để làm sạch dữ liệu
        landlord.ReviewedAt = DateTime.UtcNow;

        // BƯỚC 3: Yêu cầu Store lưu thay đổi vào cơ sở dữ liệu
        await _approvalStore.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin đã DUYỆT yêu cầu đăng ký chủ trọ của {UserName} (ID: {UserId}).",
            landlord.UserName, landlord.Id);

        // BƯỚC 4: Chuyển đổi sang DTO trả về cho tầng API
        return landlord.ToLandlordRegistrationDto();
    }

    /// <summary>
    /// Từ chối yêu cầu đăng ký: chuyển sang Rejected và lưu lý do từ chối.
    /// Tài khoản vẫn giữ trạng thái chưa kích hoạt (IsActive = false) nên không thể quản lý khu trọ.
    /// </summary>
    /// <exception cref="AppValidationException">Không nhập lý do hoặc lý do vượt quá giới hạn ký tự.</exception>
    /// <exception cref="AppNotFoundException">Không tìm thấy yêu cầu đăng ký.</exception>
    /// <exception cref="AppConflictException">Yêu cầu đã được xử lý trước đó.</exception>
    public async Task<LandlordRegistrationDto> RejectAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        // BƯỚC 1: Kiểm tra tính hợp lệ của lý do từ chối (bắt buộc nhập và không quá 500 ký tự)
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppValidationException("Vui lòng nhập lý do từ chối.");
        }

        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > MaxRejectionReasonLength)
        {
            throw new AppValidationException($"Lý do từ chối không được vượt quá {MaxRejectionReasonLength} ký tự.");
        }

        // BƯỚC 2: Lấy yêu cầu qua Store và đảm bảo nó vẫn đang ở trạng thái Pending
        var landlord = await GetPendingLandlordAsync(userId, cancellationToken);

        // BƯỚC 3: Cập nhật trạng thái từ chối
        landlord.ApprovalStatus = ApprovalStatuses.Rejected;
        landlord.IsActive = false;           // Đảm bảo tài khoản vẫn bị vô hiệu hóa
        landlord.RejectionReason = trimmedReason;
        landlord.ReviewedAt = DateTime.UtcNow;

        // BƯỚC 4: Yêu cầu Store lưu thay đổi vào cơ sở dữ liệu
        await _approvalStore.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin đã TỪ CHỐI yêu cầu đăng ký chủ trọ của {UserName} (ID: {UserId}). Lý do: {Reason}",
            landlord.UserName, landlord.Id, trimmedReason);

        // BƯỚC 5: Chuyển đổi sang DTO trả về cho tầng API
        return landlord.ToLandlordRegistrationDto();
    }

    /// <summary>
    /// Hàm dùng chung tìm tài khoản chủ trọ theo Id và kiểm tra điều kiện trạng thái Chờ duyệt.
    /// Chống việc duyệt 2 lần hoặc duyệt một tài khoản đã bị từ chối/đã duyệt trước đó.
    /// </summary>
    private async Task<AppUser> GetPendingLandlordAsync(string userId, CancellationToken cancellationToken)
    {
        // 1. Tìm tài khoản chủ trọ theo userId qua Store (chỉ tìm tài khoản có Role là Landlord)
        var landlord = await _approvalStore.FindLandlordByIdAsync(userId, cancellationToken)
            ?? throw new AppNotFoundException($"Không tìm thấy yêu cầu đăng ký chủ trọ của tài khoản có Id '{userId}'.");

        // 2. Chặn xung đột nếu yêu cầu đã được duyệt hoặc từ chối trước đó
        if (landlord.ApprovalStatus != ApprovalStatuses.Pending)
        {
            throw new AppConflictException(
                $"Yêu cầu của tài khoản '{landlord.UserName}' đã được xử lý trước đó (trạng thái hiện tại: {landlord.ApprovalStatus}).");
        }

        return landlord;
    }
}
