using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record CreateTenantRequestInput(Guid? PropertyId, string Title, string Content);

public sealed record TenantRequestFilter(int Page, int PageSize, RequestRecipientStatus? Status);

public sealed record TenantRequestItemDto(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    string RoomNumber,
    string Title,
    string Content,
    DateTime CreatedAt,
    bool LandlordIsRead,
    DateTime? LandlordReadAt,
    RequestRecipientStatus Status);

public sealed record TenantAnnouncementFilter(int Page, int PageSize, bool? IsRead);

public sealed record TenantAnnouncementItemDto(
    Guid Id,
    Guid RecipientRowId,
    Guid PropertyId,
    string PropertyName,
    string RoomNumber,
    string SenderUserId,
    string SenderFullName,
    RequestRecipientType RecipientType,
    string Title,
    string Content,
    DateTime CreatedAt,
    bool IsRead,
    DateTime? ReadAt,
    RequestRecipientStatus Status);

public sealed record LandlordRequestFilter(
    int Page,
    int PageSize,
    Guid? PropertyId,
    RequestRecipientStatus? Status,
    bool? IsRead,
    string? Search);

public sealed record LandlordRequestItemDto(
    Guid Id,
    Guid RecipientRowId,
    Guid PropertyId,
    string PropertyName,
    string SenderUserId,
    string SenderFullName,
    string? SenderPhoneNumber,
    string RoomName,
    string Title,
    string Content,
    DateTime CreatedAt,
    bool IsRead,
    DateTime? ReadAt,
    RequestRecipientStatus Status);

public sealed record AnnouncementRecipientOptionDto(
    string UserId,
    Guid TenantProfileId,
    string FullName,
    string? PhoneNumber,
    Guid RoomId,
    string RoomNumber,
    Guid ContractId);

public sealed record CreateAnnouncementInput(
    RequestRecipientType RecipientType,
    IReadOnlyList<string>? ReceiverUserIds,
    IReadOnlyList<Guid>? RoomIds,
    string Title,
    string Content);

public sealed record LandlordAnnouncementItemDto(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    RequestRecipientType RecipientType,
    string Title,
    string Content,
    DateTime CreatedAt,
    int TotalRecipients,
    int ReadCount);

public sealed record TenantActiveStayInfo(
    Guid PropertyId,
    string PropertyName,
    string LandlordUserId,
    Guid RoomId,
    string RoomNumber,
    Guid ContractId);

public sealed record TenantRequestPropertyOptionDto(
    Guid PropertyId,
    string PropertyName,
    string RoomNumber);

