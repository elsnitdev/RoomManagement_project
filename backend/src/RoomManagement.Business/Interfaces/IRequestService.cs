using RoomManagement.Business.DTOs;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Interfaces;

public interface IRequestService
{
    Task<IReadOnlyList<TenantRequestPropertyOptionDto>> GetTenantActivePropertiesAsync(string tenantUserId, CancellationToken ct = default);
    Task<TenantRequestItemDto> CreateTenantRequestAsync(string tenantUserId, CreateTenantRequestInput input, CancellationToken ct = default);
    Task<PageResult<TenantRequestItemDto>> ListTenantRequestsAsync(string tenantUserId, TenantRequestFilter filter, CancellationToken ct = default);
    Task<TenantRequestItemDto> GetTenantRequestByIdAsync(string tenantUserId, Guid requestId, CancellationToken ct = default);
    Task<PageResult<TenantAnnouncementItemDto>> ListTenantAnnouncementsAsync(string tenantUserId, TenantAnnouncementFilter filter, CancellationToken ct = default);
    Task<TenantAnnouncementItemDto> MarkAnnouncementAsReadAsync(string tenantUserId, Guid requestId, CancellationToken ct = default);

    Task<PageResult<LandlordRequestItemDto>> ListLandlordRequestsAsync(LandlordRequestFilter filter, CancellationToken ct = default);
    Task<LandlordRequestItemDto> GetLandlordRequestByIdAsync(Guid requestId, CancellationToken ct = default);
    Task<LandlordRequestItemDto> UpdateLandlordRequestStatusAsync(Guid requestId, RequestRecipientStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<AnnouncementRecipientOptionDto>> GetAnnouncementRecipientsAsync(Guid propertyId, CancellationToken ct = default);
    Task<LandlordAnnouncementItemDto> CreateLandlordAnnouncementAsync(Guid propertyId, CreateAnnouncementInput input, CancellationToken ct = default);
    Task<PageResult<LandlordAnnouncementItemDto>> ListLandlordAnnouncementsAsync(Guid propertyId, int page, int pageSize, CancellationToken ct = default);
}

