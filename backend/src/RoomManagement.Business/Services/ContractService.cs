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
    var owner = await OwnerAsync(ct);
    return await _store.GetOwnedAsync(propertyId, contractId, owner, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound,
            "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
  }

  public async Task<PageResult<Contract>> ListAsync(Guid propertyId, ContractFilter filter, CancellationToken ct)
  {
    var owner = await OwnerAsync(ct);
    if (filter.Page < 1 || filter.PageSize is < 1 or > 100)
      throw new BusinessException(BusinessErrorType.Validation,
          "PAGE_INVALID", "Trang hoặc số bản ghi không hợp lệ.");
    var (items, total) = await _store.ListOwnedAsync(propertyId, owner,
        filter.RoomId, filter.Status, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
    return new PageResult<Contract>(items, total, filter.Page, filter.PageSize);
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
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status != ContractStatus.Draft ||
        !await _store.HasCurrentTenantAsync(contractId, ct))
      throw new BusinessException(BusinessErrorType.InvalidOperation,
          "CONTRACT_CANNOT_ACTIVATE", "Hợp đồng chưa sẵn sàng kích hoạt.");
    contract.Status = ContractStatus.Active;
    contract.UpdatedAt = DateTime.UtcNow;
    await _store.SaveAsync(ct);
    return contract;
  }
}
