namespace RoomManagement.API.Contracts.Common;

public sealed record ApiResponse<T>(T Data);
public sealed record PageMeta(int Page, int PageSize, long TotalItems, int TotalPages);
public sealed record PageResponse<T>(IReadOnlyList<T> Data, PageMeta Meta);