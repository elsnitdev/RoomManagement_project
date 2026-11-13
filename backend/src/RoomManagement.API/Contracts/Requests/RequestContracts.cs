using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Requests;

public sealed record CreateTenantRequestRequest(
    Guid? PropertyId,
    string Title,
    string Content)
{
    public CreateTenantRequestInput ToInput() => new(PropertyId, Title, Content);
}

public sealed record UpdateLandlordRequestStatusRequest(RequestRecipientStatus Status);

public sealed record CreateLandlordAnnouncementRequest(
    RequestRecipientType RecipientType,
    IReadOnlyList<string>? ReceiverUserIds,
    IReadOnlyList<Guid>? RoomIds,
    string Title,
    string Content)
{
    public CreateAnnouncementInput ToInput() =>
        new(RecipientType, ReceiverUserIds, RoomIds, Title, Content);
}

