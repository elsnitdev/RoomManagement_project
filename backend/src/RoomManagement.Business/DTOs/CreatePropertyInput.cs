namespace RoomManagement.Business.DTOs;

public sealed record CreatePropertyInput(
    string Name,
    string City,
    string Ward,
    string StreetAddress,
    string? Description)
{
    public CreatePropertyInput(string name, string address, string? description)
        : this(name, string.Empty, string.Empty, address, description)
    {
    }
}