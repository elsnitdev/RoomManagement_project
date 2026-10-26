namespace RoomManagement.Business.DTOs;

public sealed record CreatePropertyInput(string Name, string Address, string? Description);