namespace RoomManagement.Domain.Constants;

public static class NotificationTypes
{
    public const string SystemBroadcast = "SystemBroadcast";
    public const string AdminLandlordBroadcast = "AdminLandlordBroadcast";
    public const string AdminDirectLandlord = "AdminDirectLandlord";

    public const string OperatorAllTenants = "OperatorAllTenants";
    public const string PropertyAnnouncement = "PropertyAnnouncement";
    public const string OperatorDirectTenant = "OperatorDirectTenant";

    // Legacy types preserved for compatibility with historical notifications
    public const string ManagerAnnouncement = "ManagerAnnouncement";
    public const string Announcement = "Announcement";

    public static readonly string[] ManualSentTypes =
    [
        SystemBroadcast,
        AdminLandlordBroadcast,
        AdminDirectLandlord,
        OperatorAllTenants,
        PropertyAnnouncement,
        OperatorDirectTenant,
        ManagerAnnouncement,
        Announcement
    ];
}
