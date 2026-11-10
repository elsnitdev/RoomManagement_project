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

  public async Task<CreateContractResult> CreateAsync(Guid propertyId, CreateContractInput input, CancellationToken ct)
  {
    var owner = await OwnerAsync(ct);
    if (!await _store.RoomOwnedAsync(propertyId, input.RoomId, owner, ct))
      throw new BusinessException(BusinessErrorType.NotFound,
          "ROOM_NOT_FOUND", "Không tìm thấy phòng trong khu trọ.");
    var code = input.ContractCode?.Trim().ToUpperInvariant() ?? "";
    if (!Regex.IsMatch(code, "^[A-Z0-9._-]{1,50}$") ||
        input.EndDate is not { } end || end < input.StartDate ||
        end.Day != DateTime.DaysInMonth(end.Year, end.Month) ||
        input.MonthlyRent < 0 || input.DepositAmount < 0 ||
        decimal.Round(input.DepositAmount, 2) != input.DepositAmount ||
        input.PaymentDueDay is < 1 or > 31 || input.Note is { Length: > 1000 })
      throw new BusinessException(BusinessErrorType.Validation,
          "CONTRACT_INVALID", "Thông tin hợp đồng không hợp lệ.");
    var now = DateTime.UtcNow;
    if (!Enum.IsDefined(input.DepositPaymentStatus) ||
        (input.DepositAmount == 0 && input.DepositPaymentStatus == InitialDepositPaymentStatus.Paid) ||
        (input.DepositPaymentStatus == InitialDepositPaymentStatus.Paid &&
            (input.DepositPaymentMethod is not (PaymentMethod.Cash or PaymentMethod.BankTransfer) ||
             input.DepositPaidAt is { } paidAt && paidAt.UtcDateTime > now)) ||
        (input.DepositPaymentStatus == InitialDepositPaymentStatus.Unpaid &&
            (input.DepositPaymentMethod is not null || input.DepositPaidAt is not null)))
      throw new BusinessException(BusinessErrorType.Validation,
          "DEPOSIT_PAYMENT_INVALID", "Thông tin thanh toán tiền cọc không hợp lệ.");
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
      CreatedAt = now
    };
    Invoice? invoice = null;
    InvoiceItem? item = null;
    Payment? payment = null;
    PaymentReviewEvent? review = null;
    if (input.DepositAmount > 0)
    {
      var today = VietnamDate.Today;
      var invoiceId = Guid.NewGuid();
      invoice = new Invoice
      {
        Id = invoiceId, ContractId = contract.Id, RoomId = contract.RoomId,
        InvoiceCode = $"DEP-{invoiceId:N}", InvoiceType = InvoiceType.Deposit,
        IssueDate = today, DueDate = contract.StartDate > today ? contract.StartDate : today,
        TotalAmount = contract.DepositAmount, Status = InvoiceStatus.Issued, CreatedAt = now
      };
      item = new InvoiceItem
      {
        Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = InvoiceItemType.Deposit,
        Description = "Tiền đặt cọc", Quantity = 1, UnitPrice = contract.DepositAmount,
        Amount = contract.DepositAmount, CreatedAt = now
      };
      if (input.DepositPaymentStatus == InitialDepositPaymentStatus.Paid)
      {
        payment = new Payment
        {
          Id = Guid.NewGuid(), InvoiceId = invoiceId,
          ClaimedAmount = contract.DepositAmount, VerifiedAmount = contract.DepositAmount,
          PaymentMethod = input.DepositPaymentMethod!.Value, Status = PaymentStatus.Success,
          PaidAt = input.DepositPaidAt?.UtcDateTime ?? now, VerifiedAt = now,
          VerifiedByUserId = owner, SubmittedByUserId = owner,
          IdempotencyKey = $"deposit-initial:{contract.Id:N}",
          Note = "Chủ trọ ghi nhận đã nhận đủ cọc khi tạo hợp đồng.", CreatedAt = now
        };
        review = new PaymentReviewEvent
        {
          Id = Guid.NewGuid(), PaymentId = payment.Id,
          PreviousStatus = PaymentStatus.Pending, NewStatus = PaymentStatus.Success,
          NewVerifiedAmount = contract.DepositAmount,
          Reason = "Chủ trọ xác nhận đã nhận đủ cọc khi tạo hợp đồng.",
          ReviewedByUserId = owner, ReviewedAt = now
        };
      }
    }
    await _store.CreateWithDepositAsync(propertyId, owner,
        new ContractCreation(contract, invoice, item, payment, review), ct);
    return new CreateContractResult(contract, invoice?.Id);
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

  public async Task<Contract> SetLegacyEndDateAsync(Guid propertyId, Guid contractId,
      DateOnly endDate, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    var today = VietnamDate.Today;
    if (contract.EndDate is not null || contract.Status is not (ContractStatus.Draft or ContractStatus.Active))
      throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_END_DATE_NOT_EDITABLE",
          "Chỉ hợp đồng cũ chưa có ngày kết thúc mới được chốt ngày.");
    if (endDate < contract.StartDate || endDate.Day != DateTime.DaysInMonth(endDate.Year, endDate.Month) ||
        contract.Status == ContractStatus.Active && endDate < today)
      throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_END_DATE_INVALID",
          "Ngày kết thúc phải là cuối tháng và không trước thời gian hợp đồng đang hiệu lực.");
    return await _store.SetLegacyEndDateAsync(contract, endDate, today, ct);
  }

  public async Task<ContractTenant> LeaveAsync(Guid propertyId, Guid contractId, Guid linkId, DateOnly leaveDate, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    return await _store.LeaveSafelyAsync(contract, linkId, leaveDate, ct);
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
        (contract.EndDate is { } end && input.JoinDate > end))
      throw new BusinessException(BusinessErrorType.Validation,
          "CONTRACT_TENANT_INVALID", "Người thuê hoặc ngày tham gia không hợp lệ.");
    return await _store.AddProfileAsync(contract, input.TenantId, owner,
        input.JoinDate, input.IsContactPerson, ct);
  }

  public async Task<ContractTenant> SetContactAsync(Guid propertyId, Guid contractId,
      Guid linkId, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status is not (ContractStatus.Draft or ContractStatus.Active))
      throw new BusinessException(BusinessErrorType.InvalidOperation,
          "CONTRACT_CANNOT_CHANGE_CONTACT", "Không thể đổi người liên hệ của hợp đồng này.");
    return await _store.SetContactAsync(contract, linkId, ct);
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

  public async Task<MemberCommitment> ConfirmCommitmentAsync(Guid propertyId, Guid contractId,
      Guid linkId, ConfirmMemberCommitmentInput input, CancellationToken ct)
  {
    var owner = await OwnerAsync(ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (input.CommitmentType != CommitmentType.RoomRulesAgreement ||
        input.SignedOn > VietnamDate.Today ||
        string.IsNullOrWhiteSpace(input.SignerName) || input.SignerName.Length > 200 ||
        string.IsNullOrWhiteSpace(input.TermsSnapshot) || input.TermsSnapshot.Length > 10_000 ||
        input.SignerRelation is { Length: > 100 } ||
        (input.SignerTenantProfileId.HasValue && string.IsNullOrWhiteSpace(input.SignerRelation)))
      throw new BusinessException(BusinessErrorType.Validation, "COMMITMENT_INVALID", "Thông tin cam kết không hợp lệ.");
    return await _store.ConfirmCommitmentAsync(contract, linkId, input, owner, ct);
  }

  public async Task<IReadOnlyList<MemberCommitment>> ListCommitmentsAsync(Guid propertyId,
      Guid contractId, Guid linkId, CancellationToken ct)
  {
    await GetAsync(propertyId, contractId, ct);
    if (await _store.GetTenantAsync(contractId, linkId, ct) is null)
      throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_TENANT_NOT_FOUND", "Không tìm thấy thành viên trong hợp đồng.");
    return await _store.ListCommitmentsAsync(contractId, linkId, ct);
  }

  public async Task<TenantRequest> CreateMoveOutNoticeAsync(Guid propertyId, Guid contractId,
      CreateMoveOutNoticeInput input, CancellationToken ct)
  {
    var owner = await OwnerAsync(ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200 ||
        string.IsNullOrWhiteSpace(input.Description) || input.Description.Length > 2000)
      throw new BusinessException(BusinessErrorType.Validation, "MOVE_OUT_NOTICE_INVALID", "Nội dung thông báo không hợp lệ.");
    return await _store.CreateMoveOutNoticeAsync(contract, input, owner, ct);
  }

  public async Task<IReadOnlyList<TenantRequest>> ListMoveOutNoticesAsync(Guid propertyId,
      Guid contractId, CancellationToken ct)
  {
    await GetAsync(propertyId, contractId, ct);
    return await _store.ListMoveOutNoticesAsync(contractId, ct);
  }

  public async Task<TenantRequest> ResolveMoveOutNoticeAsync(Guid propertyId, Guid contractId,
      Guid requestId, string resolutionNote, CancellationToken ct)
  {
    var owner = await OwnerAsync(ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (string.IsNullOrWhiteSpace(resolutionNote) || resolutionNote.Length > 1000)
      throw new BusinessException(BusinessErrorType.Validation, "RESOLUTION_NOTE_INVALID", "Ghi chú xử lý không hợp lệ.");
    return await _store.ResolveMoveOutNoticeAsync(contract, requestId, resolutionNote, owner, ct);
  }

  public async Task<IReadOnlyList<ContractTenureSpan>> GetTenureAsync(Guid propertyId,
      Guid contractId, Guid linkId, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    var link = await _store.GetTenantAsync(contractId, linkId, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_TENANT_NOT_FOUND", "Không tìm thấy thành viên trong hợp đồng.");
    var periods = await _store.ListTenurePeriodsAsync(link.TenantId, contract.RoomId, VietnamDate.Today, ct);
    return ContractTenureCalculator.Calculate(periods);
  }
}
