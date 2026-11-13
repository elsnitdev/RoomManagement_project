namespace RoomManagement.Domain;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Landlord = "Landlord";
    public const string Manager = "Manager";
    public const string Tenant = "Tenant";
    public const string LandlordOrManager = Landlord + "," + Manager;
}
