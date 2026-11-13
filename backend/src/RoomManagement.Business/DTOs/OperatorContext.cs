namespace RoomManagement.Business.DTOs;

/// <summary>
/// Ngữ cảnh người thao tác hiện tại, tách biệt người thực hiện (ActorUserId)
/// với Chủ trọ sở hữu tài nguyên (OwnerLandlordUserId).
/// </summary>
public sealed record OperatorContext(
    string ActorUserId,
    string OwnerLandlordUserId,
    string Role,
    bool IsManager);
