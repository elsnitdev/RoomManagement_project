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

    var tenantRows = await (from link in _db.ContractTenants.AsNoTracking()
                         join profile in _db.TenantProfiles.AsNoTracking() on link.TenantId equals profile.Id
                         where link.ContractId == contract.Id &&
                             (tenantUserId == null || profile.UserId == tenantUserId)
                         orderby link.JoinDate, link.Id
                         select new ContractTenantInfo(link.Id, profile.Id, profile.UserId, profile.FullName,
                             profile.PhoneNumber, profile.Email, link.JoinDate, link.LeaveDate, link.IsContactPerson)).ToListAsync(ct);
    var linkIds = tenantRows.Select(x => x.Id).ToArray();
    var currentCommitments = await _db.MemberCommitments.AsNoTracking()
        .Where(x => linkIds.Contains(x.ContractTenantId) && x.SupersededAt == null &&
            x.CommitmentType == CommitmentType.RoomRulesAgreement)
        .ToDictionaryAsync(x => x.ContractTenantId, ct);
    var tenants = new List<ContractTenantInfo>();
    foreach (var tenant in tenantRows)
    {
      currentCommitments.TryGetValue(tenant.Id, out var commitment);
      var periods = await ListTenurePeriodsAsync(tenant.TenantId, contract.RoomId,
          RoomManagement.Business.Services.VietnamDate.Today, ct);
      tenants.Add(tenant with
      {
        CurrentCommitmentId = commitment?.Id,
        CommitmentSignedOn = commitment?.SignedOn,
        CommitmentConfirmedAt = commitment?.ConfirmedAt,
        EvidenceStatus = commitment?.EvidenceStatus,
        HasConfirmedCommitment = commitment?.ConfirmedAt != null,
        TenureSpans = RoomManagement.Business.Services.ContractTenureCalculator.Calculate(periods)
      });
    }
    var visibleTenantIds = tenants.Select(x => x.TenantId).ToArray();
    var moveOutNotices = await _db.TenantRequests.AsNoTracking()
        .Where(x => x.ContractId == contract.Id && x.Category == TenantRequestCategory.MoveOutNotice &&
            (tenantUserId == null || visibleTenantIds.Contains(x.RequestedByTenantId)))
        .OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).ToListAsync(ct);

    var outstanding = await _db.InvoiceBalances.Where(x => x.ContractId == contract.Id &&
            x.Status == "Issued" && x.RemainingAmount > 0)
        .SumAsync(x => (decimal?)x.RemainingAmount, ct) ?? 0;

    var deposit = await _db.ContractDepositBalances.Where(x => x.ContractId == contract.Id)
        .SumAsync(x => (decimal?)x.DepositHeld, ct) ?? 0;
    var settled = await _db.ContractSettlements.AnyAsync(x => x.ContractId == contract.Id, ct);

    var drafts = await _db.Invoices.AnyAsync(x => x.ContractId == contract.Id && x.Status == InvoiceStatus.Draft, ct);

    return new ContractDetail(contract, roomNumber, tenants, outstanding, deposit, settled, drafts,
        moveOutNotices);
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

  /// <summary>Khóa phòng và lưu ngày rời để hai thao tác rời hoặc đổi liên hệ không làm sai trạng thái hợp đồng.</summary>
  public async Task<ContractTenant> LeaveSafelyAsync(Contract contract, Guid linkId,
      DateOnly leaveDate, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .AsNoTracking().SingleAsync(ct);
    if (!await _db.Contracts.AnyAsync(x => x.Id == contract.Id && x.Status == ContractStatus.Active, ct))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_ALREADY_CHANGED", "Hợp đồng không còn hiệu lực.");
    var link = await _db.ContractTenants.SingleOrDefaultAsync(x => x.ContractId == contract.Id && x.Id == linkId, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_TENANT_NOT_FOUND", "Không tìm thấy người thuê.");
    if (link.LeaveDate != null || leaveDate < link.JoinDate ||
        (contract.EndDate is { } end && leaveDate > end))
      throw new BusinessException(BusinessErrorType.Validation, "LEAVE_DATE_INVALID", "Ngày rời hợp đồng không hợp lệ.");
    if (link.IsContactPerson)
      throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTACT_MUST_BE_REASSIGNED",
          "Chọn người liên hệ khác trước khi người liên hệ hiện tại rời hợp đồng.");
    var nextDay = leaveDate == DateOnly.MaxValue ? leaveDate : leaveDate.AddDays(1);
    if (contract.EndDate != leaveDate &&
        !await _db.ContractTenants.AnyAsync(x => x.ContractId == contract.Id && x.Id != link.Id &&
            x.JoinDate <= nextDay && (x.LeaveDate == null || x.LeaveDate >= nextDay), ct))
      throw new BusinessException(BusinessErrorType.InvalidOperation, "LAST_TENANT_CANNOT_LEAVE",
          "Hãy kết thúc hợp đồng khi người thuê cuối cùng rời phòng.");
    link.LeaveDate = leaveDate;
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
    return link;
  }

  /// <summary>Chốt EndDate của hợp đồng cũ trong giao dịch, không cắt mất người thuê hoặc hóa đơn đã ghi.</summary>
  public async Task<Contract> SetLegacyEndDateAsync(Contract contract, DateOnly endDate,
      DateOnly today, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .AsNoTracking().SingleAsync(ct);
    var current = await _db.Contracts.AsNoTracking().Where(x => x.Id == contract.Id)
        .Select(x => new { x.Status, x.StartDate, x.EndDate }).SingleAsync(ct);
    if (current.EndDate is not null || current.Status is not (ContractStatus.Draft or ContractStatus.Active))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_END_DATE_ALREADY_SET",
          "Hợp đồng đã có ngày kết thúc hoặc đã đổi trạng thái.");
    if (endDate < current.StartDate || endDate.Day != DateTime.DaysInMonth(endDate.Year, endDate.Month) ||
        current.Status == ContractStatus.Active && endDate < today)
      throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_END_DATE_INVALID",
          "Ngày kết thúc hợp đồng không hợp lệ.");
    if (await _db.ContractTenants.AnyAsync(x => x.ContractId == contract.Id &&
        (x.JoinDate > endDate || x.LeaveDate != null && x.LeaveDate > endDate), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_MEMBER_AFTER_END_DATE",
          "Có thành viên tham gia hoặc rời sau ngày kết thúc dự kiến.");
    if (await _db.Invoices.AnyAsync(x => x.ContractId == contract.Id &&
        x.InvoiceType == InvoiceType.Monthly && x.Status != InvoiceStatus.Cancelled &&
        x.BillingYear != null && x.BillingMonth != null &&
        (x.BillingYear > endDate.Year || x.BillingYear == endDate.Year && x.BillingMonth > endDate.Month), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_INVOICE_AFTER_END_DATE",
          "Hợp đồng đã có hóa đơn tháng sau ngày kết thúc dự kiến.");
    if (await _db.Contracts.AnyAsync(x => x.Id != contract.Id && x.RoomId == contract.RoomId &&
        x.Status == ContractStatus.Active && x.StartDate <= endDate &&
        (x.EndDate == null || x.EndDate >= current.StartDate), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_ROOM_CONFLICT",
          "Phòng có hợp đồng đang hiệu lực bị chồng kỳ.");
    contract.EndDate = endDate;
    contract.UpdatedAt = DateTime.UtcNow;
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
    return contract;
  }


  //AddUserAsync – Thêm người thuê bằng email vào hợp đồng, kiểm tra sức chứa và xung đột.
  /// <inheritdoc />
  public async Task<ContractTenant> AddUserAsync(Contract contract, string email, DateOnly joinDate, CancellationToken ct)
  {
    var normalized = email.Trim().ToUpperInvariant();
    var user = await _db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized &&
        u.Role == UserRoles.Tenant && u.IsActive && u.ApprovalStatus == ApprovalStatuses.Approved, ct);
    if (user is null)
      throw new BusinessException(BusinessErrorType.NotFound, "TENANT_ACCOUNT_NOT_FOUND", "Không tìm thấy tài khoản người thuê đang hoạt động.");
    var profile = await _db.TenantProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "TENANT_PROFILE_NOT_FOUND",
            "Người thuê cần có hồ sơ trước khi được thêm vào hợp đồng.");
    var hasContact = await _db.ContractTenants.AnyAsync(x => x.ContractId == contract.Id &&
        x.IsContactPerson && x.LeaveDate == null, ct);
    return await AddProfileCoreAsync(contract, profile.Id, joinDate, !hasContact, ct);
  }

  /// <summary>Thêm một profile do chủ trọ quản lý vào hợp đồng sau khi kiểm tra sức chứa.</summary>
  public async Task<ContractTenant> AddProfileAsync(Contract contract, Guid tenantId, string ownerId,
      DateOnly joinDate, bool isContactPerson, CancellationToken ct)
  {
    if (!await TenantProfileOwnedAsync(tenantId, ownerId, ct))
      throw new BusinessException(BusinessErrorType.NotFound, "TENANT_PROFILE_NOT_FOUND",
          "Không tìm thấy hồ sơ người thuê thuộc chủ trọ.");
    return await AddProfileCoreAsync(contract, tenantId, joinDate, isContactPerson, ct);
  }

  /// <summary>Khóa phòng và tạo liên kết thành viên, chặn trùng người hoặc vượt sức chứa.</summary>
  private async Task<ContractTenant> AddProfileCoreAsync(Contract contract, Guid tenantId,
      DateOnly joinDate, bool isContactPerson, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    var room = await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .SingleAsync(ct);
    var currentStatus = await _db.Contracts.AsNoTracking().Where(c => c.Id == contract.Id)
        .Select(c => c.Status).SingleAsync(ct);
    if (currentStatus is not (ContractStatus.Draft or ContractStatus.Active))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_ALREADY_CHANGED", "Hợp đồng đã thay đổi trạng thái.");
    var profile = await _db.TenantProfiles.AsNoTracking().SingleAsync(p => p.Id == tenantId, ct);
    if (string.IsNullOrWhiteSpace(profile.FullName))
      throw new BusinessException(BusinessErrorType.Validation, "TENANT_PROFILE_INCOMPLETE", "Hồ sơ người thuê cần có họ tên.");
    if (isContactPerson && string.IsNullOrWhiteSpace(profile.PhoneNumber) && string.IsNullOrWhiteSpace(profile.Email))
      throw new BusinessException(BusinessErrorType.Validation, "CONTACT_DETAILS_REQUIRED", "Người liên hệ cần có số điện thoại hoặc email.");
    if (isContactPerson && await _db.ContractTenants.AnyAsync(x => x.ContractId == contract.Id &&
        x.IsContactPerson && x.LeaveDate == null, ct))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTACT_ALREADY_SET", "Hợp đồng đã có người liên hệ.");
    var count = await _db.ContractTenants.CountAsync(x => x.ContractId == contract.Id &&
        (x.LeaveDate == null || x.LeaveDate >= joinDate), ct);
    if (count >= room.MaxOccupants || await _db.ContractTenants.AnyAsync(link => link.TenantId == tenantId &&
        (link.LeaveDate == null || link.LeaveDate >= joinDate) && link.ContractId != contract.Id &&
        _db.Contracts.Any(c => c.Id == link.ContractId && c.Status == ContractStatus.Active &&
            c.StartDate <= (contract.EndDate ?? DateOnly.MaxValue) &&
            (c.EndDate == null || c.EndDate >= joinDate)), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "TENANT_OCCUPANCY_CONFLICT", "Phòng đã đủ người hoặc người thuê đã có hợp đồng trùng thời gian.");
    if (await _db.ContractTenants.AnyAsync(x => x.ContractId == contract.Id && x.TenantId == tenantId &&
        (x.LeaveDate == null || x.LeaveDate >= joinDate), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "TENANT_ALREADY_ADDED", "Người thuê đã thuộc hợp đồng.");
    var row = new ContractTenant
    {
      Id = Guid.NewGuid(),
      ContractId = contract.Id,
      TenantId = tenantId,
      JoinDate = joinDate,
      IsContactPerson = isContactPerson,
      CreatedAt = DateTime.UtcNow
    };
    _db.ContractTenants.Add(row);
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
    return row;
  }

  /// <summary>Chuyển đầu mối liên hệ ưu tiên sang thành viên đủ điều kiện trong cùng hợp đồng.</summary>
  public async Task<ContractTenant> SetContactAsync(Contract contract, Guid linkId, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .AsNoTracking().SingleAsync(ct);
    var currentStatus = await _db.Contracts.Where(x => x.Id == contract.Id)
        .Select(x => x.Status).SingleAsync(ct);
    if (currentStatus is not (ContractStatus.Draft or ContractStatus.Active))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_ALREADY_CHANGED",
          "Hợp đồng không còn cho phép đổi người liên hệ.");
    var today = RoomManagement.Business.Services.VietnamDate.Today;
    var link = await _db.ContractTenants.SingleOrDefaultAsync(x => x.ContractId == contract.Id &&
        x.Id == linkId && x.LeaveDate == null &&
        (currentStatus != ContractStatus.Active || x.JoinDate <= today), ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_TENANT_NOT_FOUND", "Không tìm thấy thành viên đang thuộc hợp đồng.");
    if (currentStatus == ContractStatus.Active &&
        !await _db.MemberCommitments.AnyAsync(x => x.ContractTenantId == linkId &&
            x.CommitmentType == CommitmentType.RoomRulesAgreement && x.SupersededAt == null &&
            (x.ConfirmedAt != null || x.EvidenceStatus == CommitmentEvidenceStatus.LegacyPendingEvidence), ct))
      throw new BusinessException(BusinessErrorType.InvalidOperation, "COMMITMENT_REQUIRED",
          "Người liên hệ cần có cam kết đã xác nhận.");
    var contact = await _db.TenantProfiles.AsNoTracking().SingleAsync(x => x.Id == link.TenantId, ct);
    if (string.IsNullOrWhiteSpace(contact.PhoneNumber) && string.IsNullOrWhiteSpace(contact.Email))
      throw new BusinessException(BusinessErrorType.Validation, "CONTACT_DETAILS_REQUIRED", "Người liên hệ cần có số điện thoại hoặc email.");
    var previous = await _db.ContractTenants.Where(x => x.ContractId == contract.Id && x.IsContactPerson &&
        x.LeaveDate == null && x.Id != linkId).ToListAsync(ct);
    foreach (var old in previous) old.IsContactPerson = false;
    await SaveAsync(ct);
    link.IsContactPerson = true;
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
    return link;
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
    var contactCount = await (from link in _db.ContractTenants
                              join profile in _db.TenantProfiles on link.TenantId equals profile.Id
                              where link.ContractId == contract.Id && link.IsContactPerson &&
                                  link.LeaveDate == null && link.JoinDate <= contract.StartDate &&
                                  (profile.PhoneNumber != null && profile.PhoneNumber != "" ||
                                   profile.Email != null && profile.Email != "")
                              select link.Id).CountAsync(ct);
    if (tenants.Count == 0 || tenants.Count > room.MaxOccupants ||
        contactCount != 1 ||
        !await _db.ContractTenants.AnyAsync(x => x.ContractId == contract.Id &&
            x.JoinDate <= contract.StartDate && x.LeaveDate == null, ct) ||
        await _db.ContractTenants.AnyAsync(link => tenants.Contains(link.TenantId) && link.ContractId != contract.Id &&
            (link.LeaveDate == null || link.LeaveDate >= contract.StartDate) &&
            _db.Contracts.Any(c => c.Id == link.ContractId && c.Status == ContractStatus.Active &&
                c.StartDate <= (contract.EndDate ?? DateOnly.MaxValue) &&
                (c.EndDate == null || c.EndDate >= contract.StartDate)), ct))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_TENANT_CONFLICT", "Người thuê có hợp đồng trùng thời gian hoặc phòng vượt sức chứa.");
    var missingCommitment = await _db.ContractTenants.AnyAsync(link => link.ContractId == contract.Id &&
        link.LeaveDate == null && !_db.MemberCommitments.Any(commitment =>
            commitment.ContractTenantId == link.Id &&
            commitment.CommitmentType == CommitmentType.RoomRulesAgreement &&
            commitment.SupersededAt == null && commitment.ConfirmedAt != null), ct);
    if (missingCommitment)
      throw new BusinessException(BusinessErrorType.InvalidOperation, "COMMITMENT_REQUIRED",
          "Mỗi thành viên cần có cam kết được chủ trọ xác nhận trước khi kích hoạt.");
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

  /// <summary>Ghi nhận cam kết đã ký ngoài hệ thống và xác nhận bởi chủ trọ; giữ phiên bản cũ khi ký lại.</summary>
  public async Task<MemberCommitment> ConfirmCommitmentAsync(Contract contract, Guid linkId,
      ConfirmMemberCommitmentInput input, string ownerId, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .AsNoTracking().SingleAsync(ct);
    var status = await _db.Contracts.Where(x => x.Id == contract.Id).Select(x => x.Status).SingleAsync(ct);
    if (status is not (ContractStatus.Draft or ContractStatus.Active))
      throw new BusinessException(BusinessErrorType.Conflict, "CONTRACT_ALREADY_CHANGED", "Hợp đồng không còn nhận cam kết.");
    var link = await _db.ContractTenants.SingleOrDefaultAsync(x => x.Id == linkId && x.ContractId == contract.Id, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_TENANT_NOT_FOUND", "Không tìm thấy thành viên trong hợp đồng.");
    if (link.LeaveDate is not null)
      throw new BusinessException(BusinessErrorType.InvalidOperation, "TENANT_ALREADY_LEFT", "Người thuê đã rời hợp đồng.");
    var member = await _db.TenantProfiles.AsNoTracking().SingleAsync(x => x.Id == link.TenantId, ct);
    if (member.DateOfBirth is not { } birthday || birthday > input.SignedOn)
      throw new BusinessException(BusinessErrorType.Validation, "TENANT_DOB_REQUIRED",
          "Hồ sơ người thuê cần ngày sinh hợp lệ trước khi xác nhận cam kết.");
    var signer = input.SignerTenantProfileId is { } signerId
        ? await _db.TenantProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == signerId &&
            x.CreatedByUserId == ownerId, ct) : null;
    if (input.SignerTenantProfileId.HasValue && signer is null)
      throw new BusinessException(BusinessErrorType.NotFound, "SIGNER_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ người ký thay.");
    if (birthday.AddYears(18) > input.SignedOn &&
        (signer is null || signer.Id == member.Id || signer.DateOfBirth is not { } signerBirthday ||
         signerBirthday.AddYears(18) > input.SignedOn || string.IsNullOrWhiteSpace(input.SignerRelation)))
      throw new BusinessException(BusinessErrorType.Validation, "MINOR_SIGNER_REQUIRED",
          "Người thuê chưa đủ tuổi cần người trưởng thành ký thay và ghi rõ quan hệ.");

    var now = DateTime.UtcNow;
    var old = await _db.MemberCommitments.SingleOrDefaultAsync(x => x.ContractTenantId == linkId &&
        x.CommitmentType == input.CommitmentType && x.SupersededAt == null, ct);
    if (old is not null)
    {
      old.SupersededAt = now;
      await SaveAsync(ct);
    }
    var commitment = new MemberCommitment
    {
      Id = Guid.NewGuid(), ContractTenantId = linkId, CommitmentType = input.CommitmentType,
      SignedOn = input.SignedOn, SignerName = input.SignerName.Trim(),
      SignerTenantProfileId = input.SignerTenantProfileId, SignerRelation = input.SignerRelation?.Trim(),
      TermsSnapshot = input.TermsSnapshot.Trim(), ConfirmedAt = now,
      ConfirmedByUserId = ownerId, EvidenceStatus = CommitmentEvidenceStatus.PendingUpload,
      CreatedAt = now
    };
    _db.MemberCommitments.Add(commitment);
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
    return commitment;
  }

  /// <summary>Đọc toàn bộ phiên bản cam kết của một thành viên để giữ dấu vết khi ký bổ sung.</summary>
  public async Task<IReadOnlyList<MemberCommitment>> ListCommitmentsAsync(Guid contractId, Guid linkId, CancellationToken ct) =>
      await (from commitment in _db.MemberCommitments.AsNoTracking()
             join link in _db.ContractTenants.AsNoTracking() on commitment.ContractTenantId equals link.Id
             where link.ContractId == contractId && link.Id == linkId
             orderby commitment.CreatedAt descending, commitment.Id
             select commitment).ToListAsync(ct);

  /// <summary>Tạo thông báo rời trọ trong bảng yêu cầu hiện có, chỉ dùng thời điểm máy chủ.</summary>
  public async Task<TenantRequest> CreateMoveOutNoticeAsync(Contract contract,
      CreateMoveOutNoticeInput input, string ownerId, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .AsNoTracking().SingleAsync(ct);
    if (!await _db.Contracts.AnyAsync(x => x.Id == contract.Id && x.Status == ContractStatus.Active, ct) ||
        !await _db.ContractTenants.AnyAsync(x => x.ContractId == contract.Id && x.TenantId == input.TenantId &&
            x.JoinDate <= RoomManagement.Business.Services.VietnamDate.Today && x.LeaveDate == null, ct) ||
        !await _db.TenantProfiles.AnyAsync(x => x.Id == input.TenantId && x.CreatedByUserId == ownerId, ct))
      throw new BusinessException(BusinessErrorType.InvalidOperation, "MOVE_OUT_NOTICE_INVALID", "Người gửi phải đang thuộc hợp đồng của chủ trọ.");
    if (await _db.TenantRequests.AnyAsync(x => x.ContractId == contract.Id &&
        x.RequestedByTenantId == input.TenantId && x.Category == TenantRequestCategory.MoveOutNotice &&
        x.Status != TenantRequestStatus.Resolved, ct))
      throw new BusinessException(BusinessErrorType.Conflict, "MOVE_OUT_NOTICE_OPEN", "Người thuê đã có thông báo rời đang xử lý.");
    var notice = new TenantRequest
    {
      Id = Guid.NewGuid(), ContractId = contract.Id, RequestedByTenantId = input.TenantId,
      Category = TenantRequestCategory.MoveOutNotice, Title = input.Title.Trim(),
      Description = input.Description.Trim(), RequestedAt = DateTime.UtcNow,
      Status = TenantRequestStatus.Pending, Priority = TenantRequestPriority.Medium
    };
    _db.TenantRequests.Add(notice);
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
    return notice;
  }

  /// <summary>Liệt kê các thông báo rời trọ của hợp đồng để chủ trọ theo dõi và xử lý.</summary>
  public async Task<IReadOnlyList<TenantRequest>> ListMoveOutNoticesAsync(Guid contractId, CancellationToken ct) =>
      await _db.TenantRequests.AsNoTracking().Where(x => x.ContractId == contractId &&
          x.Category == TenantRequestCategory.MoveOutNotice)
          .OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).ToListAsync(ct);

  /// <summary>Đóng thông báo rời và lưu ghi chú quyết định; không tự đổi ngày rời hay tiền hoàn.</summary>
  public async Task<TenantRequest> ResolveMoveOutNoticeAsync(Contract contract, Guid requestId,
      string resolutionNote, string ownerId, CancellationToken ct)
  {
    await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await _db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {contract.RoomId}")
        .AsNoTracking().SingleAsync(ct);
    var notice = await _db.TenantRequests.SingleOrDefaultAsync(x => x.Id == requestId &&
        x.ContractId == contract.Id && x.Category == TenantRequestCategory.MoveOutNotice, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "MOVE_OUT_NOTICE_NOT_FOUND", "Không tìm thấy thông báo rời trọ.");
    if (notice.Status == TenantRequestStatus.Resolved)
      throw new BusinessException(BusinessErrorType.Conflict, "MOVE_OUT_NOTICE_RESOLVED", "Thông báo đã được xử lý.");
    notice.Status = TenantRequestStatus.Resolved;
    notice.ResolutionNote = resolutionNote.Trim();
    notice.AssignedToUserId = ownerId;
    notice.UpdatedAt = DateTime.UtcNow;
    notice.ResolvedAt = notice.UpdatedAt;
    await SaveAsync(ct);
    await transaction.CommitAsync(ct);
    return notice;
  }

  /// <summary>Lấy khoảng tham gia các hợp đồng từng có hiệu lực để tính chuỗi ở của cùng profile và phòng.</summary>
  public async Task<IReadOnlyList<ContractTenurePeriod>> ListTenurePeriodsAsync(Guid tenantId,
      Guid roomId, DateOnly asOf, CancellationToken ct)
  {
    var rows = await (from link in _db.ContractTenants.AsNoTracking()
                      join contract in _db.Contracts.AsNoTracking() on link.ContractId equals contract.Id
                      where link.TenantId == tenantId && contract.RoomId == roomId &&
                          (contract.Status == ContractStatus.Active || contract.Status == ContractStatus.Expired ||
                           contract.Status == ContractStatus.Terminated) && contract.StartDate <= asOf &&
                          _db.MemberCommitments.Any(commitment => commitment.ContractTenantId == link.Id &&
                              commitment.CommitmentType == CommitmentType.RoomRulesAgreement &&
                              commitment.SupersededAt == null &&
                              (commitment.ConfirmedAt != null ||
                               commitment.EvidenceStatus == CommitmentEvidenceStatus.LegacyPendingEvidence))
                      select new { link.JoinDate, link.LeaveDate, contract.StartDate,
                          contract.EndDate, contract.ClosedOn }).ToListAsync(ct);
    var periods = new List<ContractTenurePeriod>();
    foreach (var row in rows)
    {
      var from = row.JoinDate > row.StartDate ? row.JoinDate : row.StartDate;
      var to = asOf;
      if (row.LeaveDate is { } left && left < to) to = left;
      if (row.EndDate is { } ended && ended < to) to = ended;
      if (row.ClosedOn is { } closed && closed < to) to = closed;
      if (from <= to) periods.Add(new ContractTenurePeriod(from, to));
    }
    return periods;
  }
}
