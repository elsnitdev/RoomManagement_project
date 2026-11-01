using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using System.Text.Json.Serialization;

namespace RoomManagement.API.Contracts.Rooms;

public sealed record RoomValuesRequest(string? RoomName, decimal? Area,
    [property: JsonRequired] decimal BasePrice, [property: JsonRequired] int MaxOccupants, string? Description)
{
    public RoomValues ToInput() => new(RoomName, Area, BasePrice, MaxOccupants, Description);
}

public sealed record RoomBatchGroupRequest(string ClientKey, Guid? RowId, int? Floor, int Count, RoomValuesRequest Defaults)
{
    public RoomBatchGroup ToInput() => new(ClientKey, RowId, Floor, Count, Defaults?.ToInput()!);
}

public sealed record RoomBatchOverrideRequest(string GroupKey, int Ordinal, RoomValuesRequest Values)
{
    public RoomBatchOverride ToInput() => new(GroupKey, Ordinal, Values?.ToInput()!);
}

public sealed record RoomBatchPreviewRequest(IReadOnlyList<RoomBatchGroupRequest> Groups, IReadOnlyList<RoomBatchOverrideRequest> Overrides)
{
    public RoomBatchPlan ToInput() => RoomBatchRequestMapping.ToInput(Groups, Overrides);
}

public sealed record RoomBatchCreateRequest(
    IReadOnlyList<RoomBatchGroupRequest> Groups,
    IReadOnlyList<RoomBatchOverrideRequest> Overrides,
    string PreviewFingerprint,
    Guid IdempotencyKey)
{
    public RoomBatchPlan ToInput() => RoomBatchRequestMapping.ToInput(Groups, Overrides);
}

internal static class RoomBatchRequestMapping
{
    public static RoomBatchPlan ToInput(IReadOnlyList<RoomBatchGroupRequest>? groups, IReadOnlyList<RoomBatchOverrideRequest>? overrides)
    {
        if (groups is null || overrides is null || groups.Any(g => g is null || g.Defaults is null)
            || overrides.Any(o => o is null || o.Values is null))
            throw new BusinessException(BusinessErrorType.Validation, "ROOM_BATCH_INVALID", "Danh sách phòng cần tạo không hợp lệ.");
        return new(groups.Select(g => g.ToInput()).ToList(), overrides.Select(o => o.ToInput()).ToList());
    }
}

public sealed record RoomBatchCreateResponse(IReadOnlyList<RoomResponse> Rooms);
public sealed record RoomRowRequest(string Code, string Name)
{
    public RoomRowInput ToInput() => new(Code, Name);
}
public sealed record UpdateRoomRowRequest(string Name, bool IsActive)
{
    public RoomRowUpdateInput ToInput() => new(Name, IsActive);
}
public sealed record RoomRowResponse(Guid Id, Guid PropertyId, string Code, string Name, bool IsActive)
{
    public static RoomRowResponse From(RoomManagement.Domain.Entities.RoomRow row) =>
        new(row.Id, row.PropertyId, row.Code, row.Name, row.IsActive);
}
