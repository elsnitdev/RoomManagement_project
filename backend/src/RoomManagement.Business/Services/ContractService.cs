using System.Text.RegularExpressions;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class ContractService
{
  private readonly ICurrentLandlord _current;
  private readonly ILandlordStore _landlords;
  private readonly IContractStore _store;

  public ContractService(ICurrentLandlord current, ILandlordStore landlords, IContractStore store)
  {
    _current = current;
    _landlords = landlords;
    _store = store;
  }

  private async Task<string> OwnerAsync(CancellationToken ct)
  {
    var owner = _current.GetUserId();
    if (string.IsNullOrWhiteSpace(owner) || !await _landlords.IsActiveLandlordAsync(owner, ct))
      throw new BusinessException(BusinessErrorType.InvalidOperation,
          "LANDLORD_UNAVAILABLE", "Chủ trọ không khả dụng.");
    return owner;
  }

  public async Task<Contract> CreateAsync(Guid propertyId, CreateContractInput input, CancellationToken ct)
  {
    var owner = await OwnerAsync(ct);
    if (!await _store.RoomOwnedAsync(propertyId, input.RoomId, owner, ct))
      throw new BusinessException(BusinessErrorType.NotFound,
          "ROOM_NOT_FOUND", "Không tìm thấy phòng trong khu trọ.");
    var code = input.ContractCode?.Trim().ToUpperInvariant() ?? "";
    if (!Regex.IsMatch(code, "^[A-Z0-9._-]{1,50}$") ||
        (input.EndDate is { } end && end < input.StartDate) ||
        input.MonthlyRent < 0 || input.DepositAmount < 0 ||
        input.PaymentDueDay is < 1 or > 31 || input.Note is { Length: > 1000 })
      throw new BusinessException(BusinessErrorType.Validation,
          "CONTRACT_INVALID", "Thông tin hợp đồng không hợp lệ.");
    var contract = new Contract
    {
      Id = Guid.NewGuid(),
      ContractCode = code,
      RoomId = input.RoomId,
      StartDate = input.StartDate,
      EndDate = input.EndDate,
      MonthlyRent = input.MonthlyRent,
      DepositAmount = input.DepositAmount,
      PaymentDueDay = input.PaymentDueDay,
      Note = input.Note?.Trim(),
      Status = ContractStatus.Draft,
      CreatedAt = DateTime.UtcNow
    };
    await _store.AddAsync(contract, ct);
    return contract;
  }

  public async Task<Contract> GetAsync(Guid propertyId, Guid contractId, CancellationToken ct)
  {
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    var owner = await OwnerAsync(ct);
    return await _store.GetOwnedAsync(propertyId, contractId, owner, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound,
            "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
  }

  public async Task<ContractDetail> GetDetailAsync(Guid propertyId, Guid contractId, CancellationToken ct)
  {
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    var owner = await OwnerAsync(ct);
    return await _store.GetDetailOwnedAsync(propertyId, contractId, owner, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
  }

  public async Task<ContractDetail> GetMineAsync(Guid contractId, string userId, CancellationToken ct)
  {
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    return await _store.GetDetailMineAsync(contractId, userId, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
  }

  public async Task<PageResult<ContractDetail>> ListMineAsync(string userId, int page, int pageSize, CancellationToken ct)
  {
    if (page < 1 || pageSize is < 1 or > 100)
      throw new BusinessException(BusinessErrorType.Validation, "PAGE_INVALID", "Trang hoặc số bản ghi không hợp lệ.");
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    var (items, total) = await _store.ListMineAsync(userId, (page - 1) * pageSize, pageSize, ct);
    return new PageResult<ContractDetail>(items, total, page, pageSize);
  }

  public async Task<ContractTenant> AddUserAsync(Guid propertyId, Guid contractId, AddContractUserInput input, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status is not (ContractStatus.Draft or ContractStatus.Active) || string.IsNullOrWhiteSpace(input.Email) ||
        input.JoinDate < contract.StartDate || (contract.EndDate is { } end && input.JoinDate > end))
      throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_TENANT_INVALID", "Người thuê hoặc ngày tham gia không hợp lệ.");
    return await _store.AddUserAsync(contract, input.Email, input.JoinDate, ct);
  }

  public async Task<Contract> CancelAsync(Guid propertyId, Guid contractId, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status != ContractStatus.Draft)
      throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_CANNOT_CANCEL", "Chỉ có thể hủy hợp đồng nháp.");
    await _store.CloseSafelyAsync(contract, ContractStatus.Draft, ContractStatus.Cancelled,
        VietnamDate.Today, null, ct);
    return contract;
  }

  public async Task<Contract> TerminateAsync(Guid propertyId, Guid contractId, string reason, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status != ContractStatus.Active || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
      throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_CANNOT_TERMINATE", "Không thể kết thúc hợp đồng tại thời điểm này.");
    var today = VietnamDate.Today;
    await _store.CloseSafelyAsync(contract, ContractStatus.Active,
        today < contract.StartDate ? ContractStatus.Cancelled : ContractStatus.Terminated,
        today, reason.Trim(), ct);
    return contract;
  }

  public async Task<ContractTenant> LeaveAsync(Guid propertyId, Guid contractId, Guid linkId, DateOnly leaveDate, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    var link = await _store.GetTenantAsync(contractId, linkId, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_TENANT_NOT_FOUND", "Không tìm thấy người thuê.");
    if (contract.Status != ContractStatus.Active || link.LeaveDate != null || leaveDate < link.JoinDate ||
        (contract.EndDate is { } end && leaveDate > end))
      throw new BusinessException(BusinessErrorType.Validation, "LEAVE_DATE_INVALID", "Ngày rời hợp đồng không hợp lệ.");
    var detail = await GetDetailAsync(propertyId, contractId, ct);
    if (!detail.Tenants.Any(t => t.Id != linkId && t.JoinDate <= leaveDate &&
        (t.LeaveDate == null || t.LeaveDate >= leaveDate)))
      throw new BusinessException(BusinessErrorType.InvalidOperation, "LAST_TENANT_CANNOT_LEAVE", "Hãy kết thúc hợp đồng khi người thuê cuối cùng rời phòng.");
    link.LeaveDate = leaveDate;
    await _store.SaveAsync(ct);
    return link;
  }

  public async Task<PageResult<Contract>> ListAsync(Guid propertyId, ContractFilter filter, CancellationToken ct)
  {
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    var owner = await OwnerAsync(ct);
    if (filter.Page < 1 || filter.PageSize is < 1 or > 100)
      throw new BusinessException(BusinessErrorType.Validation,
          "PAGE_INVALID", "Trang hoặc số bản ghi không hợp lệ.");
    var (items, total) = await _store.ListOwnedAsync(propertyId, owner,
        filter.RoomId, filter.Status, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
    return new PageResult<Contract>(items, total, filter.Page, filter.PageSize);
  }

  public async Task<PageResult<ContractDetail>> ListDetailsAsync(Guid propertyId, ContractFilter filter, CancellationToken ct)
  {
    var page = await ListAsync(propertyId, filter, ct);
    var owner = await OwnerAsync(ct);
    var details = new List<ContractDetail>();
    foreach (var contract in page.Items)
    {
      var detail = await _store.GetDetailOwnedAsync(propertyId, contract.Id, owner, ct);
      if (detail is not null) details.Add(detail);
    }
    return new PageResult<ContractDetail>(details, page.TotalItems, page.Page, page.PageSize);
  }

  public async Task<ContractTenant> AddTenantAsync(
      Guid propertyId, Guid contractId, AddContractTenantInput input, CancellationToken ct)
  {
    var owner = await OwnerAsync(ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status is not (ContractStatus.Draft or ContractStatus.Active) ||
        input.JoinDate < contract.StartDate ||
        (contract.EndDate is { } end && input.JoinDate > end) ||
        !await _store.TenantProfileOwnedAsync(input.TenantId, owner, ct) ||
        await _store.TenantAlreadyCurrentAsync(contractId, input.TenantId, ct))
      throw new BusinessException(BusinessErrorType.Validation,
          "CONTRACT_TENANT_INVALID", "Người thuê hoặc ngày tham gia không hợp lệ.");
    var row = new ContractTenant
    {
      Id = Guid.NewGuid(),
      ContractId = contractId,
      TenantId = input.TenantId,
      JoinDate = input.JoinDate,
      CreatedAt = DateTime.UtcNow
    };
    await _store.AddTenantAsync(row, ct);
    return row;
  }

  public async Task<Contract> ActivateAsync(Guid propertyId, Guid contractId, CancellationToken ct)
  {
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    var room = await _store.GetRoomAsync(contract.RoomId, ct);
    if (contract.Status != ContractStatus.Draft ||
        contract.EndDate is { } end && end < VietnamDate.Today ||
        !await _store.HasCurrentTenantAsync(contractId, ct) ||
        room is null || room.Status != RoomStatus.Available ||
        await _store.HasOverlappingContractAsync(contract, ct))
      throw new BusinessException(BusinessErrorType.InvalidOperation,
          "CONTRACT_CANNOT_ACTIVATE", "Hợp đồng chưa sẵn sàng kích hoạt.");
    await _store.ActivateSafelyAsync(contract, ct);
    return contract;
  }
}
