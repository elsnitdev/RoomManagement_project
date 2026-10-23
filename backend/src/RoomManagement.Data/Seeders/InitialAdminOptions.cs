namespace RoomManagement.Data.Seeders;

public sealed class InitialAdminOptions
{
    public const string SectionName = "InitialAdmin";

    public string UserName { get; set; } = "admin";
    public string Email { get; set; } = "admin@example.com";
    public string Password { get; set; } = string.Empty;
}
