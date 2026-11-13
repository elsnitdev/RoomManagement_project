using RoomManagement.Domain.Enums;
using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;
namespace RoomManagement.Business.Interfaces;

public interface IContractStore
{
  /// <summary>Kiểm tra phòng có thuộc khu trọ đang hoạt động của chủ trọ hay không.</summary>
  Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct);
  /// <summary>Tìm hợp đồng thuộc khu trọ của chủ trọ theo mã hợp đồng.</summary>
  Task<Contract?> GetOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct);
  /// <summary>Lấy danh sách hợp đồng của chủ trọ theo bộ lọc và phân trang.</summary>
  Task<(IReadOnlyList<Contract> Items, long Total)> ListOwnedAsync(
         Guid propertyId, string ownerId, Guid? roomId, ContractStatus? status,
         int skip, int take, CancellationToken ct);
  /// <summary>Kiểm tra hồ sơ người thuê có do chủ trọ tạo hay không.</summary>
  Task<bool> TenantProfileOwnedAsync(Guid tenantId, string ownerId, CancellationToken ct);
  /// <summary>Kiểm tra hợp đồng có người thuê chưa rời đi hay không.</summary>
  Task<bool> HasCurrentTenantAsync(Guid contractId, CancellationToken ct);
  /// <summary>Kiểm tra người thuê đã tham gia và chưa rời hợp đồng hay chưa.</summary>
  Task<bool> TenantAlreadyCurrentAsync(Guid contractId, Guid tenantId, CancellationToken ct);
  /// <summary>Thêm người thuê vào hợp đồng bằng email sau khi kiểm tra điều kiện thuê.</summary>
  Task<ContractTenant> AddUserAsync(Contract contract, string email, DateOnly joinDate, CancellationToken ct);
  /// <summary>Gắn hồ sơ người thuê do chủ trọ quản lý với hợp đồng phòng.</summary>
  Task<ContractTenant> AddProfileAsync(Contract contract, Guid tenantId, string ownerId,
      DateOnly joinDate, bool isContactPerson, ContractTenantRole role, CancellationToken ct);
  /// <summary>Đổi người liên hệ ưu tiên của hợp đồng.</summary>
  Task<ContractTenant> SetContactAsync(Contract contract, Guid linkId, CancellationToken ct);
  /// <summary>Lấy chi tiết hợp đồng thuộc quyền sở hữu của chủ trọ.</summary>
  Task<ContractDetail?> GetDetailOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct);
  /// <summary>Lấy chi tiết hợp đồng liên quan đến tài khoản người thuê.</summary>
  Task<ContractDetail?> GetDetailMineAsync(Guid contractId, string userId, CancellationToken ct);
  /// <summary>Lấy danh sách hợp đồng của người thuê, có phân trang.</summary>
  Task<(IReadOnlyList<ContractDetail> Items, long Total)> ListMineAsync(string userId, int skip, int take, CancellationToken ct);
  /// <summary>Tìm liên kết người thuê trong hợp đồng.</summary>
  Task<ContractTenant?> GetTenantAsync(Guid contractId, Guid linkId, CancellationToken ct);
  /// <summary>Ghi ngày rời trong giao dịch sau khi kiểm tra người liên hệ và thành viên còn lại.</summary>
  Task<ContractTenant> LeaveSafelyAsync(Contract contract, Guid linkId, DateOnly leaveDate, CancellationToken ct);
  /// <summary>Chốt ngày kết thúc cuối tháng cho hợp đồng cũ chưa có EndDate sau khi khóa phòng và kiểm tra dữ liệu liên quan.</summary>
  Task<Contract> SetLegacyEndDateAsync(Contract contract, DateOnly endDate, DateOnly today, CancellationToken ct);
  /// <summary>Tìm phòng theo mã định danh.</summary>
  Task<Room?> GetRoomAsync(Guid roomId, CancellationToken ct);
  /// <summary>Kiểm tra phòng có hợp đồng đang hoạt động trùng thời gian hay không.</summary>
  Task<bool> HasOverlappingContractAsync(Contract contract, CancellationToken ct);
  /// <summary>Chuyển các hợp đồng đang hoạt động đã quá hạn sang hết hạn.</summary>
  Task ExpireDueAsync(DateOnly today, CancellationToken ct);
  /// <summary>Kích hoạt hợp đồng sau khi kiểm tra phòng và người thuê.</summary>
  Task ActivateSafelyAsync(Contract contract, CancellationToken ct);
  /// <summary>Đóng hợp đồng nếu trạng thái hiện tại khớp trạng thái dự kiến.</summary>
  Task CloseSafelyAsync(Contract contract, ContractStatus expectedStatus,
      ContractStatus newStatus, DateOnly closedOn, string? reason, CancellationToken ct);
  Task StartEarlyExitAsync(Contract contract, string reason, CancellationToken ct);
  Task HandoverAsync(Contract contract, DateOnly handoverOn, CancellationToken ct);
  Task<MeterReading> RecordContractReadingAsync(Contract contract, UtilityType type, short year, byte month,
      decimal previousValue, decimal currentValue, CancellationToken ct);
  Task<IReadOnlyList<MeterReading>> ListContractReadingsAsync(Guid contractId, CancellationToken ct);
  Task CompleteSettlementAsync(Contract contract, string disposition, decimal refundAmount, string? note, CancellationToken ct);
  Task<ContractSettlement?> GetSettlementAsync(Guid contractId, CancellationToken ct);
  Task<ContractSettlement> SaveRefundEvidenceAsync(Guid contractId, Guid imageId, string blobName,
      string fileName, string contentType, long sizeBytes, string sha256, DateTime uploadedAt, CancellationToken ct);
  Task<DepositApplication> ApplyDepositAsync(Contract contract, Guid invoiceId, decimal amount,
      string reason, string idempotencyKey, string ownerId, CancellationToken ct);
  /// <summary>Tạo hợp đồng, hóa đơn cọc và khoản thu ban đầu trong một giao dịch.</summary>
  Task CreateWithDepositAsync(Guid propertyId, string ownerId, ContractCreation creation, CancellationToken ct);
  /// <summary>Lưu thay đổi hợp đồng và xử lý lỗi trùng dữ liệu.</summary>
  Task SaveAsync(CancellationToken ct);

  /// <summary>Lưu bản cam kết được chủ trọ xác nhận; phiên bản cũ cùng loại được giữ lại trong lịch sử.</summary>
  Task<MemberCommitment> ConfirmCommitmentAsync(Contract contract, Guid linkId,
      ConfirmMemberCommitmentInput input, string ownerId, CancellationToken ct);
  /// <summary>Lấy lịch sử cam kết của một thành viên trong hợp đồng.</summary>
  Task<IReadOnlyList<MemberCommitment>> ListCommitmentsAsync(Guid contractId, Guid linkId, CancellationToken ct);
  /// <summary>Gửi thông báo rời trọ vào bảng yêu cầu của người thuê, với thời điểm do hệ thống cấp.</summary>
  Task<TenantRequest> CreateMoveOutNoticeAsync(Contract contract, CreateMoveOutNoticeInput input,
      string ownerId, CancellationToken ct);
  /// <summary>Lấy các thông báo rời trọ liên quan đến một hợp đồng.</summary>
  Task<IReadOnlyList<TenantRequest>> ListMoveOutNoticesAsync(Guid contractId, CancellationToken ct);
  /// <summary>Ghi nhận quyết định xử lý thông báo rời trọ của chủ trọ.</summary>
  Task<TenantRequest> ResolveMoveOutNoticeAsync(Contract contract, Guid requestId,
      string resolutionNote, string ownerId, CancellationToken ct);
  /// <summary>Lấy các khoảng hợp đồng có hiệu lực của một profile trong một phòng để tính thời gian ở.</summary>
  Task<IReadOnlyList<ContractTenurePeriod>> ListTenurePeriodsAsync(Guid tenantId, Guid roomId,
      DateOnly asOf, CancellationToken ct);

}
