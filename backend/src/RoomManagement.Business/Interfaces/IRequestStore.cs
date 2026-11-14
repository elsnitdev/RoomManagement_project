using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Interfaces;

public interface IRequestStore
{
    Task<IReadOnlyList<TenantActiveStayInfo>> GetTenantActiveStaysAsync(string tenantUserId, CancellationToken ct = default);
    Task<Property?> GetOwnedPropertyAsync(Guid propertyId, string landlordUserId, CancellationToken ct = default);
    Task<Request> AddRequestWithRecipientsAsync(Request request, IReadOnlyList<RequestRecipient> recipients, CancellationToken ct = default);
    Task<(IReadOnlyList<TenantRequestItemDto> Items, long Total)> ListTenantRequestsAsync(
        string tenantUserId, RequestRecipientStatus? status, int skip, int take, CancellationToken ct = default);
    Task<TenantRequestItemDto?> GetTenantRequestByIdAsync(Guid requestId, string tenantUserId, CancellationToken ct = default);
    Task<(IReadOnlyList<TenantAnnouncementItemDto> Items, long Total)> ListTenantAnnouncementsAsync(
        string tenantUserId, bool? isRead, int skip, int take, CancellationToken ct = default);
    Task<TenantAnnouncementItemDto?> MarkAnnouncementAsReadAsync(Guid requestId, string tenantUserId, CancellationToken ct = default);
    Task<(IReadOnlyList<LandlordRequestItemDto> Items, long Total)> ListLandlordRequestsAsync(
        string landlordUserId, LandlordRequestFilter filter, int skip, int take, CancellationToken ct = default);
    Task<LandlordRequestItemDto?> GetLandlordRequestAndMarkReadAsync(Guid requestId, string landlordUserId, CancellationToken ct = default);
    Task<LandlordRequestItemDto?> UpdateLandlordRequestStatusAsync(
        Guid requestId, string landlordUserId, RequestRecipientStatus newStatus, CancellationToken ct = default);
    Task<IReadOnlyList<AnnouncementRecipientOptionDto>> ListEligibleAnnouncementRecipientsAsync(
        Guid propertyId, CancellationToken ct = default);
    Task<(IReadOnlyList<LandlordAnnouncementItemDto> Items, long Total)> ListLandlordAnnouncementsAsync(
        Guid propertyId, string landlordUserId, int skip, int take, CancellationToken ct = default);
}

