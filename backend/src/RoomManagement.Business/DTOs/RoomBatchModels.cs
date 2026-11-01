using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.DTOs;

public sealed record RoomValues(string? RoomName, decimal? Area, decimal BasePrice, int MaxOccupants, string? Description);
public sealed record RoomBatchGroup(string ClientKey, Guid? RowId, int? Floor, int Count, RoomValues Defaults);
public sealed record RoomBatchOverride(string GroupKey, int Ordinal, RoomValues Values);
public sealed record RoomBatchPlan(IReadOnlyList<RoomBatchGroup> Groups, IReadOnlyList<RoomBatchOverride> Overrides);
public sealed record RoomBatchPreviewItem(string ClientKey, int Ordinal, string RoomNumber, Guid? RowId, int? Floor, int SequenceNumber, RoomValues Values);
public sealed record RoomBatchPreviewResult(IReadOnlyList<RoomBatchPreviewItem> Rooms, string PreviewFingerprint);
public sealed record RoomBatchCreateResult(IReadOnlyList<RoomResult> Rooms);
public sealed record RoomRowInput(string Code, string Name);
public sealed record RoomRowUpdateInput(string Name, bool IsActive);
