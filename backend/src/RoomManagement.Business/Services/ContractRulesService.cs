using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class ContractRulesService(ICurrentLandlord current, ILandlordStore landlords, IContractRulesStore store)
{
    public const string DefaultTemplate = "NỘI QUY PHÒNG TRỌ\n\n1. Người thuê sử dụng phòng và tài sản chung đúng mục đích, giữ gìn vệ sinh và an toàn.\n2. Người thuê thực hiện các thỏa thuận về tiền thuê, dịch vụ và thời hạn thanh toán trong hợp đồng.\n3. Các bên tôn trọng sinh hoạt chung và thông báo cho nhau khi có sự cố hoặc thay đổi liên quan đến việc thuê.\n\nChủ trọ cần rà soát, bổ sung nội dung phù hợp với khu trọ và pháp luật trước khi lưu.";

    private async Task<OperatorContext> RequirePropertyAsync(Guid propertyId, CancellationToken ct)
    {
        var context = await OperatorAccessGuard.RequireOperatorAsync(current, landlords, ct);
        await OperatorAccessGuard.EnsurePropertyAccessAsync(context, propertyId, landlords, ct, "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
        return context;
    }

    public async Task<ContractRulesVersion?> GetOwnedAsync(Guid propertyId, CancellationToken ct)
    {
        var context = await RequirePropertyAsync(propertyId, ct);
        return await store.GetOwnedAsync(propertyId, context.OwnerLandlordUserId, ct);
    }

    public async Task<ContractRulesVersion> SaveAsync(Guid propertyId, string? content, Guid? expectedVersionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 10_000)
            throw new BusinessException(BusinessErrorType.Validation, "RULES_CONTENT_INVALID", "Nội dung nội quy phải có từ 1 đến 10.000 ký tự.");
        var context = await RequirePropertyAsync(propertyId, ct);
        return await store.SaveAsync(propertyId, context.OwnerLandlordUserId, content.Trim(), expectedVersionId, ct);
    }

    public Task<IReadOnlyList<ContractRulesTenantView>> ListMineAsync(string userId, CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(userId) ? store.ListMineAsync(userId, ct)
            : throw new BusinessException(BusinessErrorType.NotFound, "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");

    public Task<ContractRulesAcceptance> AcceptAsync(Guid contractId, Guid linkId, Guid versionId, string userId, CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(userId) && contractId != Guid.Empty && linkId != Guid.Empty && versionId != Guid.Empty
            ? store.AcceptAsync(contractId, linkId, versionId, userId, ct)
            : throw new BusinessException(BusinessErrorType.Validation, "RULES_ACCEPTANCE_INVALID", "Thông tin chấp thuận không hợp lệ.");
}
