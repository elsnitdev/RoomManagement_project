using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Mappings;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

/// <summary>
/// Service xử lý việc Admin xem, duyệt hoặc từ chối các yêu cầu đăng ký chủ trọ.
/// </summary>
public sealed class LandlordApprovalService : ILandlordApprovalService
{
    /// <summary>Độ dài tối đa của lý do từ chối (khớp với cột nvarchar(500) trong DB).</summary>
    private const int MaxRejectionReasonLength = 500;

    private readonly RoomManagementDbContext _dbContext;
    private readonly ILogger<LandlordApprovalService> _logger;

    public LandlordApprovalService(
        RoomManagementDbContext dbContext,
        ILogger<LandlordApprovalService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Lấy toàn bộ yêu cầu đăng ký chủ trọ đang chờ duyệt, yêu cầu gửi sớm nhất xếp trước.
    /// </summary>
    public async Task<IReadOnlyList<LandlordRegistrationDto>> GetPendingRegistrationsAsync(CancellationToken cancellationToken = default)
    {
        var pendingLandlords = await _dbContext.Users
            .AsNoTracking() // Chỉ đọc, không cần theo dõi thay đổi
            .Where(u => u.Role == UserRoles.Landlord && u.ApprovalStatus == ApprovalStatuses.Pending)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        return pendingLandlords.Select(u => u.ToLandlordRegistrationDto()).ToList();
    }

    /// <summary>
    /// Duyệt yêu cầu đăng ký: chuyển sang Approved và kích hoạt tài khoản.
    /// Từ thời điểm này tài khoản chính thức có quyền Landlord.
    /// </summary>
    /// <exception cref="AppNotFoundException">Không tìm thấy yêu cầu đăng ký.</exception>
    /// <exception cref="AppConflictException">Yêu cầu đã được xử lý trước đó.</exception>
    public async Task<LandlordRegistrationDto> ApproveAsync(string userId, CancellationToken cancellationToken = default)
    {
        // BƯỚC 1: Lấy yêu cầu và đảm bảo nó vẫn đang chờ duyệt
        var landlord = await GetPendingLandlordAsync(userId, cancellationToken);

        // BƯỚC 2: Cập nhật trạng thái
        landlord.ApprovalStatus = ApprovalStatuses.Approved;
        landlord.IsActive = true;            // Mở khóa tài khoản
        landlord.RejectionReason = null;     // Xóa lý do từ chối (nếu có) cho sạch dữ liệu
        landlord.ReviewedAt = DateTime.UtcNow;

        // BƯỚC 3: Lưu thay đổi
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin đã DUYỆT yêu cầu đăng ký chủ trọ của {UserName} (ID: {UserId}).",
            landlord.UserName, landlord.Id);

        return landlord.ToLandlordRegistrationDto();
    }

    /// <summary>
    /// Từ chối yêu cầu đăng ký: chuyển sang Rejected và lưu lý do.
    /// Tài khoản vẫn giữ trạng thái chưa kích hoạt nên không thể quản lý khu trọ.
    /// </summary>
    /// <exception cref="AppValidationException">Không nhập lý do hoặc lý do quá dài.</exception>
    /// <exception cref="AppNotFoundException">Không tìm thấy yêu cầu đăng ký.</exception>
    /// <exception cref="AppConflictException">Yêu cầu đã được xử lý trước đó.</exception>
    public async Task<LandlordRegistrationDto> RejectAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        // BƯỚC 1: Lý do từ chối là bắt buộc để người đăng ký biết vì sao bị từ chối
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppValidationException("Vui lòng nhập lý do từ chối.");
        }

        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > MaxRejectionReasonLength)
        {
            throw new AppValidationException($"Lý do từ chối không được vượt quá {MaxRejectionReasonLength} ký tự.");
        }

        // BƯỚC 2: Lấy yêu cầu và đảm bảo nó vẫn đang chờ duyệt
        var landlord = await GetPendingLandlordAsync(userId, cancellationToken);

        // BƯỚC 3: Cập nhật trạng thái
        landlord.ApprovalStatus = ApprovalStatuses.Rejected;
        landlord.IsActive = false;           // Đảm bảo tài khoản vẫn bị khóa
        landlord.RejectionReason = trimmedReason;
        landlord.ReviewedAt = DateTime.UtcNow;

        // BƯỚC 4: Lưu thay đổi
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin đã TỪ CHỐI yêu cầu đăng ký chủ trọ của {UserName} (ID: {UserId}). Lý do: {Reason}",
            landlord.UserName, landlord.Id, trimmedReason);

        return landlord.ToLandlordRegistrationDto();
    }

    /// <summary>
    /// Tìm tài khoản chủ trọ theo Id và kiểm tra nó đang ở trạng thái Chờ duyệt.
    /// Dùng chung cho cả Duyệt và Từ chối, nhờ đó không thể duyệt 2 lần
    /// hoặc duyệt một tài khoản đã bị từ chối.
    /// </summary>
    private async Task<AppUser> GetPendingLandlordAsync(string userId, CancellationToken cancellationToken)
    {
        // Chỉ tìm trong các tài khoản chủ trọ; tài khoản Admin/Tenant không phải "yêu cầu đăng ký"
        var landlord = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Id == userId && u.Role == UserRoles.Landlord,
            cancellationToken)
            ?? throw new AppNotFoundException($"Không tìm thấy yêu cầu đăng ký chủ trọ của tài khoản có Id '{userId}'.");

        if (landlord.ApprovalStatus != ApprovalStatuses.Pending)
        {
            throw new AppConflictException(
                $"Yêu cầu của tài khoản '{landlord.UserName}' đã được xử lý trước đó (trạng thái hiện tại: {landlord.ApprovalStatus}).");
        }

        return landlord;
    }
}
