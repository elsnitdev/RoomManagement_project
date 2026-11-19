namespace RoomManagement.Domain.Enums;

public enum AdminNotificationTargetScope
{
    AllSystem = 1,
    AllLandlords = 2,
    SpecificLandlord = 3
}

public enum OperatorNotificationTargetScope
{
    AllActiveTenants = 1,
    SpecificProperty = 2,
    SpecificTenant = 3
}
