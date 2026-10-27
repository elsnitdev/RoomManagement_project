namespace RoomManagement.Domain.Constants;

public static class TenantGenders
{
    public const string Male = "Male";
    public const string Female = "Female";
    public const string Other = "Other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Male, Female, Other
    };
}
