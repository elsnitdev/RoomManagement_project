using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record PageResult<T>(IReadOnlyList<T> Items, long TotalItems, int Page, int PageSize);
public sealed record PropertyFilter(int Page, int PageSize, string? Search, bool? IsActive);
public sealed record UpdatePropertyInput(string Name, string Address, string? Description, bool IsActive);
public sealed record CreateRoomInput(string RoomNumber, string? RoomName, int? Floor, decimal? Area, decimal BasePrice, int MaxOccupants, string? Description);
public sealed record UpdateRoomInput(string RoomNumber, string? RoomName, int? Floor, decimal? Area, decimal BasePrice, int MaxOccupants, string? Description, RoomStatus Status);
public sealed record RoomFilter(int Page, int PageSize, string? Search, RoomStatus? Status);
public sealed record RoomResult(Room Room, RoomStatus Status);
