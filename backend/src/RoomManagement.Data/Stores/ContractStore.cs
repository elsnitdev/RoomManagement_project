using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class ContractStore : IContractStore
{
  private readonly RoomManagementDbContext _db;
  public ContractStore(RoomManagementDbContext db)
  {
    _db = db;
  }
  // các query thuộc contract thuộc landlord sẽ đi qua phần này 
  //Owned – Lọc các hợp đồng thuộc quyền sở hữu của chủ trọ.
  /// <summary>Tạo truy vấn hợp đồng thuộc khu trọ của chủ trọ.</summary>
  private IQueryable<Contract> Owned(Guid propertyId, string ownerId)
  {
    return
    from contract in _db.Contracts
    join room in _db.Rooms on contract.RoomId equals room.Id
    join property in _db.Properties on room.PropertyId equals property.Id
    where property.Id == propertyId && property.OwnerUserId == ownerId
    select contract;
  }
  // RoomOwnedAsync – Kiểm tra quyền sở hữu phòng trọ.
  /// <inheritdoc />
  public Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct)
  {
    return
        _db.Rooms.AnyAsync(room => room.Id == roomId && room.PropertyId == propertyId &&
            _db.Properties.Any(property => property.Id == propertyId &&
                property.OwnerUserId == ownerId && property.IsActive), ct);
  }
  //GetOwnedAsync – Lấy hợp đồng theo ID thuộc chủ trọ.
  /// <inheritdoc />
  public Task<Contract?> GetOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct) =>
        Owned(propertyId, ownerId).SingleOrDefaultAsync(c => c.Id == contractId, ct);

  //ListOwnedAsync – Lấy danh sách hợp đồng của chủ trọ, hỗ trợ lọc và phân trang.
  /// <inheritdoc />
  public async Task<(IReadOnlyList<Contract> Items, long Total)> ListOwnedAsync(
      Guid propertyId, string ownerId, Guid? roomId, ContractStatus? status,
      int skip, int take, CancellationToken ct)
  {
    var query = Owned(propertyId, ownerId).AsNoTracking();
    if (roomId.HasValue) query = query.Where(c => c.RoomId == roomId.Value);
    if (status.HasValue) query = query.Where(c => c.Status == status.Value);
    var total = await query.LongCountAsync(ct);
    var items = await query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
        .Skip(skip).Take(take).ToListAsync(ct);
    return (items, total);
  }

  // TenantProfileOwnedAsync – Kiểm tra hồ sơ người thuê có do chủ trọ tạo hay không.
  /// <inheritdoc />
  public Task<bool> TenantProfileOwnedAsync(Guid tenantId, string ownerId, CancellationToken ct)
  {
    return _db.TenantProfiles.AnyAsync(t => t.Id == tenantId && t.CreatedByUserId == ownerId, ct);
  }

  //HasCurrentTenantAsync – Kiểm tra hợp đồng có người thuê chưa rời đi hay không.
  /// <inheritdoc />
  public Task<bool> HasCurrentTenantAsync(Guid contractId, CancellationToken ct)
  {
    return _db.ContractTenants.AnyAsync(t => t.ContractId == contractId && t.LeaveDate == null, ct);
  }

  //TenantAlreadyCurrentAsync – Kiểm tra người thuê đã có trong hợp đồng và chưa rời đi hay chưa.
  /// <inheritdoc />
  public Task<bool> TenantAlreadyCurrentAsync(Guid contractId, Guid tenantId, CancellationToken ct)
  {
    return _db.ContractTenants.AnyAsync(t => t.ContractId == contractId &&
            t.TenantId == tenantId && t.LeaveDate == null, ct);
  }

  //GetRoomAsync – Lấy thông tin phòng theo ID.
  /// <inheritdoc />
  public Task<Room?> GetRoomAsync(Guid roomId, CancellationToken ct) =>
      _db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roomId, ct);

  //HasOverlappingContractAsync – Kiểm tra phòng có hợp đồng đang hoạt động bị trùng thời gian hay không.
  /// <inheritdoc />
  public Task<bool> HasOverlappingContractAsync(Contract contract, CancellationToken ct) =>
      _db.Contracts.AnyAsync(c => c.Id != contract.Id && c.RoomId == contract.RoomId &&
          c.Status == ContractStatus.Active && c.StartDate <= (contract.EndDate ?? DateOnly.MaxValue) &&
          (c.EndDate == null || c.EndDate >= contract.StartDate), ct);

  //DetailAsync – Tổng hợp chi tiết hợp đồng, người thuê, công nợ, tiền cọc và trạng thái thanh toán.
  /// <summary>Ghép hợp đồng với phòng, người thuê, công nợ và tiền cọc.</summary>
  private async Task<ContractDetail> DetailAsync(Contract contract, CancellationToken ct, string? tenantUserId = null)
  {
    var roomNumber = await _db.Rooms.Where(r => r.Id == contract.RoomId).Select(r => r.RoomNumber).SingleAsync(ct);

    var tenants = await (from link in _db.ContractTenants.AsNoTracking()
                         join profile in _db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
                         where link.ContractId == contract.Id &&
                             (tenantUserId == null || profile.UserId == tenantUserId)
                         orderby link.JoinDate, link.Id
                         select new ContractTenantInfo(link.Id, profile.Id, profile.UserId, profile.FullName,
                             profile.Email, link.JoinDate, link.LeaveDate)).ToListAsync(ct);

    var outstanding = await _db.InvoiceBalances.Where(x => x.ContractId == contract.Id &&
            x.Status == "Issued" && x.RemainingAmount > 0)
        .SumAsync(x => (decimal?)x.RemainingAmount, ct) ?? 0;

    var deposit = await _db.ContractDepositBalances.Where(x => x.ContractId == contract.Id)
        .SumAsync(x => (decimal?)x.DepositHeld, ct) ?? 0;
    var settled = await _db.ContractSettlements.AnyAsync(x => x.ContractId == contract.Id, ct);

    var drafts = await _db.Invoices.AnyAsync(x => x.ContractId == contract.Id && x.Status == InvoiceStatus.Draft, ct);

    return new ContractDetail(contract, roomNumber, tenants, outstanding, deposit, settled, drafts);
  }

  //GetDetailOwnedAsync – Lấy chi tiết hợp đồng thuộc quyền sở hữu của chủ trọ.
  /// <inheritdoc />
  public async Task<ContractDetail?> GetDetailOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct)
  {
    var contract = await Owned(propertyId, ownerId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == contractId, ct);
    return contract is null ? null : await DetailAsync(contract, ct);
  }

  //Mine – Lọc các hợp đồng liên quan đến tài khoản người thuê.
  /// <summary>Tạo truy vấn hợp đồng liên quan đến tài khoản người thuê.</summary>
  private IQueryable<Contract> Mine(string userId) => _db.Contracts.Where(c =>
      c.Status != ContractStatus.Draft && c.Status != ContractStatus.Cancelled &&
      c.StartDate <= RoomManagement.Business.Services.VietnamDate.Today &&
      _db.ContractTenants.Any(link => link.ContractId == c.Id &&
          _db.TenantProfiles.Any(p => p.Id == link.TenantId && p.UserId == userId)));

  //GetDetailMineAsync – Lấy chi tiết hợp đồng của người thuê.
  /// <inheritdoc />
  public async Task<ContractDetail?> GetDetailMineAsync(Guid contractId, string userId, CancellationToken ct)
  {
    var contract = await Mine(userId).AsNoTracking().SingleOrDefaultAsync(c => c.Id == contractId, ct);
    return contract is null ? null : await DetailAsync(contract, ct, userId);
  }

  //ListMineAsync – Lấy danh sách hợp đồng của người thuê, hỗ trợ phân trang.
  /// <inheritdoc />
  public async Task<(IReadOnlyList<ContractDetail> Items, long Total)> ListMineAsync(string userId, int skip, int take, CancellationToken ct)
  {
    var query = Mine(userId).AsNoTracking();
    var total = await query.LongCountAsync(ct);
    var contracts = await query.OrderByDescending(x => x.StartDate).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    var details = new List<ContractDetail>();
    foreach (var contract in contracts) details.Add(await DetailAsync(contract, ct, userId));
    return (details, total);
  }

  //GetTenantAsync – Lấy thông tin liên kết người thuê trong hợp đồng.
  /// <inheritdoc />
  public Task<ContractTenant?> GetTenantAsync(Guid contractId, Guid linkId, CancellationToken ct) =>
      _db.ContractTenants.SingleOrDefaultAsync(x => x.ContractId == contractId && x.Id == linkId, ct);


  //AddUserAsync – Thêm người thuê bằng email vào hợp đồng, kiểm tra sức chứa và xung đột.
  /// <inheritdoc />
  public async Task<ContractTenant> AddUserAsync(Contract contract, string email, DateOnly joinDate, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    var normalized = email.Trim().ToUpperInvariant();
    var user = await _db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized &&
        u.Role == UserRoles.Tenant && u.IsActive && u.ApprovalStatus == ApprovalStatuses.Approved, ct);
    if (user is null)
      throw new BusinessException(BusinessErrorType.NotFound, "TENANT_ACCOUNT_NOT_FOUND", "Không tìm thấy tài khoản người thuê đang hoạt động.");
    if (string.IsNullOrWhiteSpace(user.FullName) || string.IsNullOrWhiteSpace(user.PhoneNumber))
      throw new BusinessException(BusinessErrorType.Validation, "TENANT_PROFILE_INCOMPLETE", "Người thuê cần hoàn thiện họ tên và số điện thoại.");
    var profile = await _db.TenantProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id, ct);
    if (profile is null)
    {
      profile = new TenantProfile
      {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        CreatedByUserId = null,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        Email = user.Email,
        CreatedAt = DateTime.UtcNow
      };
      _db.TenantProfiles.Add(profile);
    }
    var room = await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .SingleAsync(ct);
    var count = await _db.ContractTenants.CountAsync(x => x.ContractId == contract.Id &&
        (x.LeaveDate == null || x.LeaveDate >= joinDate), ct);
    if (count >= room.MaxOccupants || await _db.ContractTenants.AnyAsync(link => link.TenantId == profile.Id &&
        (link.LeaveDate == null || link.LeaveDate >= joinDate) && link.ContractId != contract.Id &&
        _db.Contracts.Any(c => c.Id == link.ContractId && c.Status == ContractStatus.Active &&
            c.StartDate <= (contract.EndDate ?? DateOnly.MaxValue) &&
            (c.EndDate == null || c.EndDate >= joinDate)), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "TENANT_OCCUPANCY_CONFLICT", "Phòng đã đủ người hoặc người thuê đã có hợp đồng trùng thời gian.");
    if (await _db.ContractTenants.AnyAsync(x => x.ContractId == contract.Id && x.TenantId == profile.Id &&
        (x.LeaveDate == null || x.LeaveDate >= joinDate), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "TENANT_ALREADY_ADDED", "Người thuê đã thuộc hợp đồng.");
    var row = new ContractTenant
    {
      Id = Guid.NewGuid(),
      ContractId = contract.Id,
      TenantId = profile.Id,
      JoinDate = joinDate,
      CreatedAt = DateTime.UtcNow
    };
    _db.ContractTenants.Add(row);
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
    return row;
  }

  //ExpireDueAsync – Tự động cập nhật các hợp đồng quá hạn sang trạng thái Expired.
  /// <inheritdoc />
  public async Task ExpireDueAsync(DateOnly today, CancellationToken ct)
  {
    await _db.Contracts.Where(c => c.Status == ContractStatus.Active && c.EndDate != null && c.EndDate < today)
        .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Status, ContractStatus.Expired)
            .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), ct);
  }

  //ActivateSafelyAsync – Kích hoạt hợp đồng an toàn, kiểm tra trạng thái phòng, người thuê và trùng lịch.
  /// <inheritdoc />
  public async Task ActivateSafelyAsync(Contract contract, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    var room = await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .SingleAsync(ct);
    var currentStatus = await _db.Contracts.AsNoTracking().Where(c => c.Id == contract.Id)
        .Select(c => c.Status).SingleAsync(ct);
    if (currentStatus != ContractStatus.Draft)
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_ALREADY_CHANGED", "Hợp đồng đã thay đổi trạng thái.");
    if (contract.EndDate is { } end && end < RoomManagement.Business.Services.VietnamDate.Today)
      throw new BusinessException(BusinessErrorType.InvalidOperation,
          "CONTRACT_ALREADY_EXPIRED", "Hợp đồng đã quá ngày kết thúc.");
    if (room.Status != RoomStatus.Available || await HasOverlappingContractAsync(contract, ct))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_ROOM_CONFLICT", "Phòng đã có hợp đồng trùng thời gian hoặc không khả dụng.");
    var tenants = await _db.ContractTenants.Where(x => x.ContractId == contract.Id && x.LeaveDate == null)
        .Select(x => x.TenantId).ToListAsync(ct);
    if (tenants.Count == 0 || tenants.Count > room.MaxOccupants ||
        await _db.ContractTenants.AnyAsync(link => tenants.Contains(link.TenantId) && link.ContractId != contract.Id &&
            (link.LeaveDate == null || link.LeaveDate >= contract.StartDate) &&
            _db.Contracts.Any(c => c.Id == link.ContractId && c.Status == ContractStatus.Active &&
                c.StartDate <= (contract.EndDate ?? DateOnly.MaxValue) &&
                (c.EndDate == null || c.EndDate >= contract.StartDate)), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_TENANT_CONFLICT", "Người thuê có hợp đồng trùng thời gian hoặc phòng vượt sức chứa.");
    contract.Status = ContractStatus.Active;
    contract.UpdatedAt = DateTime.UtcNow;
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
  }

  //CloseSafelyAsync – Đóng hợp đồng an toàn, cập nhật trạng thái, ngày đóng và lý do.
  /// <inheritdoc />
  public async Task CloseSafelyAsync(Contract contract, ContractStatus expectedStatus,
    ContractStatus newStatus, DateOnly closedOn, string? reason, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .AsNoTracking().SingleAsync(ct);
    var currentStatus = await _db.Contracts.AsNoTracking().Where(c => c.Id == contract.Id)
        .Select(c => c.Status).SingleAsync(ct);
    if (currentStatus != expectedStatus)
      throw new BusinessException(BusinessErrorType.Conflict,
          "CONTRACT_ALREADY_CHANGED", "Hợp đồng đã thay đổi trạng thái.");
    contract.Status = newStatus;
    contract.ClosedOn = closedOn;
    contract.ClosureReason = reason;
    contract.UpdatedAt = DateTime.UtcNow;
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
  }

  //AddAsync – Thêm hợp đồng mới vào database.
  /// <inheritdoc />
  public async Task AddAsync(Contract contract, CancellationToken ct)
  {
    _db.Contracts.Add(contract);
    await SaveAsync(ct);
  }
  //AddTenantAsync – Thêm liên kết người thuê vào hợp đồng.
  /// <inheritdoc />
  public async Task AddTenantAsync(ContractTenant tenant, CancellationToken ct)
  {
    _db.ContractTenants.Add(tenant);
    await SaveAsync(ct);
  }
  // SaveAsync – Lưu thay đổi vào database và xử lý lỗi trùng dữ liệu.
  /// <inheritdoc />
  public async Task SaveAsync(CancellationToken ct)
  {
    try { await _db.SaveChangesAsync(ct); }
    catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
    {
      throw new BusinessException(BusinessErrorType.Conflict,
          "CONTRACT_CONFLICT", "Mã hợp đồng, phòng đang thuê hoặc liên kết người thuê bị trùng.", ex);
    }
  }
}
