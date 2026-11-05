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
  /// <summary>Lấy chi tiết hợp đồng thuộc quyền sở hữu của chủ trọ.</summary>
  Task<ContractDetail?> GetDetailOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct);
  /// <summary>Lấy chi tiết hợp đồng liên quan đến tài khoản người thuê.</summary>
  Task<ContractDetail?> GetDetailMineAsync(Guid contractId, string userId, CancellationToken ct);
  /// <summary>Lấy danh sách hợp đồng của người thuê, có phân trang.</summary>
  Task<(IReadOnlyList<ContractDetail> Items, long Total)> ListMineAsync(string userId, int skip, int take, CancellationToken ct);
  /// <summary>Tìm liên kết người thuê trong hợp đồng.</summary>
  Task<ContractTenant?> GetTenantAsync(Guid contractId, Guid linkId, CancellationToken ct);
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
  /// <summary>Thêm hợp đồng mới và lưu vào cơ sở dữ liệu.</summary>
  Task AddAsync(Contract contract, CancellationToken ct);
  /// <summary>Thêm liên kết người thuê vào hợp đồng và lưu lại.</summary>
  Task AddTenantAsync(ContractTenant tenant, CancellationToken ct);
  /// <summary>Lưu thay đổi hợp đồng và xử lý lỗi trùng dữ liệu.</summary>
  Task SaveAsync(CancellationToken ct);

}
