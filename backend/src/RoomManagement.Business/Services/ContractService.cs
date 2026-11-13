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

  private async Task<OperatorContext> OperatorAsync(CancellationToken ct) =>
      await OperatorAccessGuard.RequireOperatorAsync(_current, _landlords, ct);

  private async Task<OperatorContext> PropertyOperatorAsync(Guid propertyId, CancellationToken ct)
  {
    var context = await OperatorAsync(ct);
    await OperatorAccessGuard.EnsurePropertyAccessAsync(context, propertyId, _landlords, ct,
        "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
    return context;
  }

  public async Task<CreateContractResult> CreateAsync(Guid propertyId, CreateContractInput input, CancellationToken ct)
  {
    var context = await OperatorAsync(ct);
    await OperatorAccessGuard.EnsurePropertyAccessAsync(context, propertyId, _landlords, ct,
        "ROOM_NOT_FOUND", "Không tìm thấy phòng trong khu trọ.");
    var owner = context.OwnerLandlordUserId;
    var actor = context.ActorUserId;
    if (!await _store.RoomOwnedAsync(propertyId, input.RoomId, owner, ct))
      throw new BusinessException(BusinessErrorType.NotFound,
          "ROOM_NOT_FOUND", "Không tìm thấy phòng trong khu trọ.");
    var code = input.ContractCode?.Trim().ToUpperInvariant() ?? "";
    if (!Regex.IsMatch(code, "^[A-Z0-9._-]{1,50}$") ||
        input.EndDate is not { } end || end < input.StartDate ||
        end.Day != DateTime.DaysInMonth(end.Year, end.Month) ||
        input.MonthlyRent < 0 || input.DepositAmount < 0 ||
        decimal.Round(input.DepositAmount, 2) != input.DepositAmount ||
        input.PaymentDueDay is < 1 or > 31 || input.Note is { Length: > 1000 } ||
        !Enum.IsDefined(input.TenancyMode) ||
        input.RoomServices is { } services && (services.Any(x => x.ServiceId == Guid.Empty ||
            x.Quantity <= 0 || x.CustomPrice is < 0 ||
            x.CustomPrice is { } price && price != decimal.Round(price, 2)) ||
            services.Select(x => x.ServiceId).Distinct().Count() != services.Count))
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
      TenancyMode = input.TenancyMode,
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
          VerifiedByUserId = actor, SubmittedByUserId = actor,
          IdempotencyKey = $"deposit-initial:{contract.Id:N}",
          Note = "Chủ trọ ghi nhận đã nhận đủ cọc khi tạo hợp đồng.", CreatedAt = now
        };
        review = new PaymentReviewEvent
        {
          Id = Guid.NewGuid(), PaymentId = payment.Id,
          PreviousStatus = PaymentStatus.Pending, NewStatus = PaymentStatus.Success,
          NewVerifiedAmount = contract.DepositAmount,
          Reason = "Chủ trọ xác nhận đã nhận đủ cọc khi tạo hợp đồng.",
          ReviewedByUserId = actor, ReviewedAt = now
        };
      }
    }
    await _store.CreateWithDepositAsync(propertyId, owner,
        new ContractCreation(contract, invoice, item, payment, review, input.RoomServices), ct);
    return new CreateContractResult(contract, invoice?.Id);
  }

  public async Task<Contract> GetAsync(Guid propertyId, Guid contractId, CancellationToken ct)
  {
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    var context = await PropertyOperatorAsync(propertyId, ct);
    return await _store.GetOwnedAsync(propertyId, contractId, context.OwnerLandlordUserId, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound,
            "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
  }

  public async Task<ContractDetail> GetDetailAsync(Guid propertyId, Guid contractId, CancellationToken ct)
  {
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    var context = await PropertyOperatorAsync(propertyId, ct);
    return await _store.GetDetailOwnedAsync(propertyId, contractId, context.OwnerLandlordUserId, ct)
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
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status is not (ContractStatus.Draft or ContractStatus.PendingTenantConfirmation or ContractStatus.PendingOperatorConfirmation))
      throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_CANNOT_CANCEL", "Chỉ có thể hủy hợp đồng trước khi kích hoạt.");
    var expectedStatus = contract.Status;
    await _store.CloseSafelyAsync(contract, expectedStatus, ContractStatus.Cancelled,
        VietnamDate.Today, null, ct);
    await _store.RecordClosedActorAsync(contractId, context.ActorUserId, ct);
    if (expectedStatus != ContractStatus.Draft)
    {
      await _store.NotifyContractTenantsAsync(contractId, "ContractCancelled",
          "Đề nghị hợp đồng đã bị hủy",
          $"Đề nghị hợp đồng {contract.ContractCode} đã bị phía chủ trọ hủy.",
          context.ActorUserId, ct);
    }
    return contract;
  }

  public async Task<Contract> TerminateAsync(Guid propertyId, Guid contractId, string reason, CancellationToken ct)
  {
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status != ContractStatus.Active || contract.StartDate > VietnamDate.Today ||
        string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
      throw new BusinessException(BusinessErrorType.InvalidOperation, "CONTRACT_CANNOT_TERMINATE", "Không thể kết thúc hợp đồng tại thời điểm này.");
    await _store.StartEarlyExitAsync(contract, reason.Trim(), ct);
    await _store.RecordClosedActorAsync(contractId, context.ActorUserId, ct);
    await _store.NotifyContractTenantsAsync(contractId, "ContractTerminated",
        "Thông báo kết thúc hợp đồng",
        $"Hợp đồng {contract.ContractCode} đã được ghi nhận kết thúc/trả phòng. Lý do: {reason.Trim()}.",
        context.ActorUserId, ct);
    return contract;
  }

  public async Task<Contract> HandoverAsync(Guid propertyId, Guid contractId, DateOnly handoverOn, CancellationToken ct)
  {
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status != ContractStatus.PendingMoveOut || handoverOn < contract.StartDate ||
        handoverOn > VietnamDate.Today || contract.EndDate is { } end && handoverOn > end)
      throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_HANDOVER_INVALID", "Ngày bàn giao không hợp lệ.");
    await _store.HandoverAsync(contract, handoverOn, ct);
    await _store.RecordClosedActorAsync(contractId, context.ActorUserId, ct);
    await _store.NotifyContractTenantsAsync(contractId, "ContractMoveOut",
        "Xác nhận bàn giao trả phòng",
        $"Hợp đồng {contract.ContractCode} đã hoàn tất bàn giao phòng ngày {handoverOn:yyyy-MM-dd}.",
        context.ActorUserId, ct);
    return contract;
  }

  public async Task<MeterReading> RecordContractReadingAsync(Guid propertyId, Guid contractId,
      UtilityType type, short year, byte month, decimal previousValue, decimal currentValue, CancellationToken ct)
  {
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status is not (ContractStatus.Active or ContractStatus.PendingMoveOut or ContractStatus.PendingSettlement) ||
        type is not (UtilityType.Electricity or UtilityType.Water) || year is < 2000 or > 2100 ||
        month is < 1 or > 12 || previousValue < 0 || currentValue < previousValue ||
        (contract.Status == ContractStatus.PendingSettlement &&
         (contract.HandoverOn?.Year != year || contract.HandoverOn?.Month != month)))
      throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_READING_INVALID", "Chỉ số điện nước không hợp lệ.");
    return await _store.RecordContractReadingAsync(contract, type, year, month, previousValue, currentValue, ct);
  }

  public async Task<IReadOnlyList<MeterReading>> ListContractReadingsAsync(Guid propertyId,
      Guid contractId, CancellationToken ct)
  {
    await GetAsync(propertyId, contractId, ct);
    return await _store.ListContractReadingsAsync(contractId, ct);
  }

  public async Task<Contract> CompleteSettlementAsync(Guid propertyId, Guid contractId,
      string disposition, decimal refundAmount, string? note, CancellationToken ct)
  {
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status != ContractStatus.PendingSettlement ||
        disposition is not ("Refunded" or "Offset" or "NoDeposit") || refundAmount < 0 ||
        note is { Length: > 1000 })
      throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_SETTLEMENT_INVALID", "Thông tin tất toán không hợp lệ.");
    await _store.CompleteSettlementAsync(contract, disposition, refundAmount, note, ct);
    await _store.RecordClosedActorAsync(contractId, context.ActorUserId, ct);
    await _store.NotifyContractTenantsAsync(contractId, "ContractSettled",
        "Hoàn tất tất toán hợp đồng",
        $"Hợp đồng {contract.ContractCode} đã hoàn tất tất toán tiền cọc ({disposition}).",
        context.ActorUserId, ct);
    return contract;
  }

  public async Task<DepositApplication> ApplyDepositAsync(Guid propertyId, Guid contractId, Guid invoiceId,
      decimal amount, string reason, string idempotencyKey, CancellationToken ct)
  {
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status != ContractStatus.PendingSettlement || invoiceId == Guid.Empty || amount <= 0 ||
        amount != decimal.Round(amount, 2) || string.IsNullOrWhiteSpace(reason) || reason.Length > 500 ||
        string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
      throw new BusinessException(BusinessErrorType.Validation, "DEPOSIT_APPLICATION_INVALID", "Thông tin cấn trừ cọc không hợp lệ.");
    return await _store.ApplyDepositAsync(contract, invoiceId, amount, reason.Trim(), idempotencyKey.Trim(), context.ActorUserId, ct);
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
    var context = await PropertyOperatorAsync(propertyId, ct);
    if (filter.Page < 1 || filter.PageSize is < 1 or > 100)
      throw new BusinessException(BusinessErrorType.Validation,
          "PAGE_INVALID", "Trang hoặc số bản ghi không hợp lệ.");
    var (items, total) = await _store.ListOwnedAsync(propertyId, context.OwnerLandlordUserId,
        filter.RoomId, filter.Status, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
    return new PageResult<Contract>(items, total, filter.Page, filter.PageSize);
  }

  public async Task<PageResult<ContractDetail>> ListDetailsAsync(Guid propertyId, ContractFilter filter, CancellationToken ct)
  {
    var page = await ListAsync(propertyId, filter, ct);
    var context = await PropertyOperatorAsync(propertyId, ct);
    var details = new List<ContractDetail>();
    foreach (var contract in page.Items)
    {
      var detail = await _store.GetDetailOwnedAsync(propertyId, contract.Id, context.OwnerLandlordUserId, ct);
      if (detail is not null) details.Add(detail);
    }
    return new PageResult<ContractDetail>(details, page.TotalItems, page.Page, page.PageSize);
  }

  public async Task<ContractTenant> AddTenantAsync(
      Guid propertyId, Guid contractId, AddContractTenantInput input, CancellationToken ct)
  {
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status is not (ContractStatus.Draft or ContractStatus.Active) ||
        input.JoinDate < contract.StartDate ||
        (contract.EndDate is { } end && input.JoinDate > end) ||
        !Enum.IsDefined(input.Role) ||
        (contract.TenancyMode == ContractTenancyMode.CoTenancy && input.Role != ContractTenantRole.ContractParty))
      throw new BusinessException(BusinessErrorType.Validation,
          "CONTRACT_TENANT_INVALID", "Người thuê hoặc ngày tham gia không hợp lệ.");
    return await _store.AddProfileAsync(contract, input.TenantId, context.OwnerLandlordUserId,
        input.JoinDate, input.IsContactPerson, input.Role, ct);
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

  public async Task<ContractTenant> AddTenantByUserAsync(
      Guid propertyId, Guid contractId, AddContractTenantByUserInput input, CancellationToken ct)
  {
    await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (contract.Status is not (ContractStatus.Draft or ContractStatus.Active) ||
        string.IsNullOrWhiteSpace(input.UserId) ||
        input.JoinDate < contract.StartDate ||
        (contract.EndDate is { } end && input.JoinDate > end) ||
        !Enum.IsDefined(input.Role) ||
        (contract.TenancyMode == ContractTenancyMode.CoTenancy && input.Role != ContractTenantRole.ContractParty))
      throw new BusinessException(BusinessErrorType.Validation,
          "CONTRACT_TENANT_INVALID", "Người thuê hoặc ngày tham gia không hợp lệ.");
    return await _store.AddTenantByUserAsync(contract, input.UserId.Trim(), input.JoinDate,
        input.IsContactPerson, input.Role, ct);
  }

  public async Task<Contract> SendOfferAsync(Guid propertyId, Guid contractId, CancellationToken ct)
  {
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    return await _store.SendOfferSafelyAsync(contract, context.ActorUserId, ct);
  }

  public async Task<Contract> UpdateOfferTermsAsync(Guid propertyId, Guid contractId,
      UpdateContractOfferInput input, CancellationToken ct)
  {
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (input.EndDate is { } end && end < input.StartDate ||
        input.MonthlyRent < 0 || input.DepositAmount < 0 ||
        input.PaymentDueDay is < 1 or > 31 ||
        !Enum.IsDefined(input.TenancyMode))
      throw new BusinessException(BusinessErrorType.Validation, "CONTRACT_INVALID", "Thông tin hợp đồng không hợp lệ.");
    return await _store.UpdateOfferTermsSafelyAsync(contract, input, context.ActorUserId, ct);
  }

  public Task<Contract> ConfirmOfferByTenantAsync(Guid contractId, string tenantUserId,
      ConfirmContractOfferInput input, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(tenantUserId) || input.OfferVersion < 1 ||
        string.IsNullOrWhiteSpace(input.TermsHash) || input.RulesVersionId == Guid.Empty)
      throw new BusinessException(BusinessErrorType.Validation, "CONFIRMATION_INVALID", "Thông tin xác nhận đề nghị không hợp lệ.");
    return _store.ConfirmOfferByTenantAsync(contractId, tenantUserId, input, ct);
  }

  public Task<Contract> RejectOfferByTenantAsync(Guid contractId, string tenantUserId,
      string reason, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(tenantUserId) || reason is { Length: > 1000 })
      throw new BusinessException(BusinessErrorType.Validation, "REJECTION_INVALID", "Lý do từ chối không hợp lệ.");
    return _store.RejectOfferByTenantAsync(contractId, tenantUserId, reason, ct);
  }

  public async Task<Contract> ActivateAsync(Guid propertyId, Guid contractId, CancellationToken ct)
  {
    await _store.ExpireDueAsync(VietnamDate.Today, ct);
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    var room = await _store.GetRoomAsync(contract.RoomId, ct);
    if (contract.Status != ContractStatus.PendingOperatorConfirmation ||
        contract.EndDate is { } end && end < VietnamDate.Today ||
        !await _store.HasCurrentTenantAsync(contractId, ct) ||
        room is null || room.Status != RoomStatus.Available ||
        await _store.HasOverlappingContractAsync(contract, ct))
      throw new BusinessException(BusinessErrorType.InvalidOperation,
          "CONTRACT_CANNOT_ACTIVATE", "Hợp đồng chưa được người thuê đại diện xác nhận hoặc phòng không khả dụng.");
    await _store.ActivateSafelyAsync(contract, context.ActorUserId, ct);
    return contract;
  }

  public async Task<MemberCommitment> ConfirmCommitmentAsync(Guid propertyId, Guid contractId,
      Guid linkId, ConfirmMemberCommitmentInput input, CancellationToken ct)
  {
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (input.CommitmentType != CommitmentType.RoomRulesAgreement ||
        input.SignedOn > VietnamDate.Today ||
        string.IsNullOrWhiteSpace(input.SignerName) || input.SignerName.Length > 200 ||
        string.IsNullOrWhiteSpace(input.TermsSnapshot) || input.TermsSnapshot.Length > 10_000 ||
        input.SignerRelation is { Length: > 100 } ||
        (input.SignerTenantProfileId.HasValue && string.IsNullOrWhiteSpace(input.SignerRelation)))
      throw new BusinessException(BusinessErrorType.Validation, "COMMITMENT_INVALID", "Thông tin cam kết không hợp lệ.");
    return await _store.ConfirmCommitmentAsync(contract, linkId, input, context.ActorUserId, ct);
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
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200 ||
        string.IsNullOrWhiteSpace(input.Description) || input.Description.Length > 2000)
      throw new BusinessException(BusinessErrorType.Validation, "MOVE_OUT_NOTICE_INVALID", "Nội dung thông báo không hợp lệ.");
    return await _store.CreateMoveOutNoticeAsync(contract, input, context.OwnerLandlordUserId, ct);
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
    var context = await PropertyOperatorAsync(propertyId, ct);
    var contract = await GetAsync(propertyId, contractId, ct);
    if (string.IsNullOrWhiteSpace(resolutionNote) || resolutionNote.Length > 1000)
      throw new BusinessException(BusinessErrorType.Validation, "RESOLUTION_NOTE_INVALID", "Ghi chú xử lý không hợp lệ.");
    return await _store.ResolveMoveOutNoticeAsync(contract, requestId, resolutionNote, context.ActorUserId, ct);
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
