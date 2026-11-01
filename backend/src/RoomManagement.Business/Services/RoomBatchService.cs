using System.Text.RegularExpressions;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class RoomBatchService
{
    private readonly ICurrentLandlord _current;
    private readonly ILandlordStore _landlords;
    private readonly IPropertyStore _properties;
    private readonly IRoomBatchStore _store;

    public RoomBatchService(ICurrentLandlord current, ILandlordStore landlords, IPropertyStore properties, IRoomBatchStore store)
    {
        _current = current;
        _landlords = landlords;
        _properties = properties;
        _store = store;
    }

    private async Task<string> OwnerAsync(Guid propertyId, bool requireActive, CancellationToken ct)
    {
        var owner = _current.GetUserId();
        if (string.IsNullOrWhiteSpace(owner) || !await _landlords.IsActiveLandlordAsync(owner, ct))
            throw new BusinessException(BusinessErrorType.InvalidOperation, "LANDLORD_UNAVAILABLE", "Chủ trọ hiện tại không khả dụng.");
        var property = await _properties.GetAsync(propertyId, owner, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");
        if (requireActive && !property.IsActive)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "PROPERTY_INACTIVE", "Không thể thêm phòng vào khu trọ ngừng hoạt động.");
        return owner;
    }

    public async Task<IReadOnlyList<RoomRow>> ListRowsAsync(Guid propertyId, CancellationToken ct)
    {
        await OwnerAsync(propertyId, false, ct);
        return await _store.ListRowsAsync(propertyId, ct);
    }

    public async Task<RoomRow> AddRowAsync(Guid propertyId, RoomRowInput input, CancellationToken ct)
    {
        await OwnerAsync(propertyId, true, ct);
        var code = input.Code?.Trim().ToUpperInvariant() ?? "";
        PropertyRoomValidation.Required(input.Name, 100, "Tên dãy");
        if (!Regex.IsMatch(code, "^[A-Z][A-Z0-9]{0,19}$"))
            throw new BusinessException(BusinessErrorType.Validation, "ROW_CODE_INVALID", "Mã dãy phải bắt đầu bằng chữ cái không dấu, tối đa 20 ký tự.");
        return await _store.AddRowAsync(propertyId, code, input.Name.Trim(), ct);
    }

    public async Task<RoomRow> UpdateRowAsync(Guid propertyId, Guid rowId, RoomRowUpdateInput input, CancellationToken ct)
    {
        await OwnerAsync(propertyId, true, ct);
        PropertyRoomValidation.Required(input.Name, 100, "Tên dãy");
        return await _store.UpdateRowAsync(propertyId, rowId, input.Name.Trim(), input.IsActive, ct);
    }

    private static void Validate(RoomBatchPlan plan)
    {
        if (plan.Groups is null || plan.Overrides is null || plan.Groups.Count == 0 || plan.Groups.Sum(g => (long)g.Count) > 200)
            throw new BusinessException(BusinessErrorType.Validation, "ROOM_BATCH_SIZE_INVALID", "Mỗi lượt tạo cần từ 1 đến 200 phòng.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in plan.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.ClientKey) || group.ClientKey.Length > 80 || !keys.Add(group.ClientKey)
                || group.Count is < 1 or > 99 || group.Floor is <= 0 || (group.RowId is null && group.Floor is null)
                || !scopes.Add(RoomNumbering.Scope(group.RowId, group.Floor)) || group.Defaults is null)
                throw new BusinessException(BusinessErrorType.Validation, "ROOM_BATCH_GROUP_INVALID", "Nhóm dãy/tầng hoặc số lượng phòng không hợp lệ.");
            ValidateValues(group.Defaults);
        }
        var positions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in plan.Overrides)
        {
            var group = plan.Groups.FirstOrDefault(g => g.ClientKey == item.GroupKey);
            if (group is null || item.Ordinal < 1 || item.Ordinal > group.Count || !positions.Add($"{item.GroupKey}:{item.Ordinal}") || item.Values is null)
                throw new BusinessException(BusinessErrorType.Validation, "ROOM_BATCH_OVERRIDE_INVALID", "Thông tin phòng đặc biệt không hợp lệ.");
            ValidateValues(item.Values);
        }
    }

    private static void ValidateValues(RoomValues value) =>
        PropertyRoomValidation.Room("TEMP", value.RoomName, value.Area, value.BasePrice, value.MaxOccupants, value.Description);

    public async Task<RoomBatchPreviewResult> PreviewAsync(Guid propertyId, RoomBatchPlan plan, CancellationToken ct)
    {
        await OwnerAsync(propertyId, true, ct);
        Validate(plan);
        return await _store.PreviewAsync(propertyId, plan, ct);
    }

    public async Task<RoomBatchCreateResult> CreateAsync(Guid propertyId, RoomBatchPlan plan, string fingerprint, Guid idempotencyKey, CancellationToken ct)
    {
        var owner = await OwnerAsync(propertyId, false, ct);
        Validate(plan);
        if (string.IsNullOrWhiteSpace(fingerprint) || idempotencyKey == Guid.Empty)
            throw new BusinessException(BusinessErrorType.Validation, "ROOM_BATCH_CONFIRMATION_REQUIRED", "Hãy xem trước danh sách phòng trước khi lưu.");
        return await _store.CreateAsync(propertyId, owner, plan, fingerprint, idempotencyKey, ct);
    }
}
