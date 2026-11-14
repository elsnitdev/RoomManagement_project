using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using Xunit;

namespace RoomManagement.Business.Tests;

public sealed class RequestServiceTests
{
    private const string LandlordId = "landlord-001";
    private const string Tenant1Id = "tenant-001";
    private const string Tenant2Id = "tenant-002";

    [Fact]
    public async Task CreateTenantRequest_throws_when_title_or_content_invalid()
    {
        var (service, _, _) = CreateSut();

        var titleEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateTenantRequestAsync(Tenant1Id, new CreateTenantRequestInput(null, "   ", "Nội dung hợp lệ"), default));
        Assert.Equal("INVALID_TITLE", titleEx.Code);
        Assert.Equal(BusinessErrorType.Validation, titleEx.Type);

        var contentEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateTenantRequestAsync(Tenant1Id, new CreateTenantRequestInput(null, "Tiêu đề hợp lệ", ""), default));
        Assert.Equal("INVALID_CONTENT", contentEx.Code);
        Assert.Equal(BusinessErrorType.Validation, contentEx.Type);
    }

    [Fact]
    public async Task CreateTenantRequest_throws_when_no_active_contract()
    {
        var (service, store, _) = CreateSut();
        store.ActiveStaysByTenant[Tenant1Id] = [];

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateTenantRequestAsync(Tenant1Id, new CreateTenantRequestInput(null, "Sửa đèn", "Đèn phòng tắm bị cháy"), default));
        Assert.Equal("NO_ACTIVE_CONTRACT", ex.Code);
        Assert.Equal(BusinessErrorType.InvalidOperation, ex.Type);
    }

    [Fact]
    public async Task CreateTenantRequest_and_Landlord_View_and_UpdateStatus_works_end_to_end()
    {
        var (service, store, propertyId) = CreateSut();

        // 1. Tenant gửi yêu cầu
        var created = await service.CreateTenantRequestAsync(
            Tenant1Id,
            new CreateTenantRequestInput(null, "  Hỏng ổ cắm  ", "  Ổ cắm cạnh bàn làm việc bị lỏng.  "),
            default);

        Assert.Equal("Hỏng ổ cắm", created.Title);
        Assert.Equal("Ổ cắm cạnh bàn làm việc bị lỏng.", created.Content);
        Assert.False(created.LandlordIsRead);
        Assert.Null(created.LandlordReadAt);
        Assert.Equal(RequestRecipientStatus.Open, created.Status);
        Assert.Single(store.Requests);
        Assert.Single(store.Recipients);

        // 2. Landlord bấm xem chi tiết -> tự động bật IsRead = true, ReadAt != null
        var detail = await service.GetLandlordRequestByIdAsync(created.Id, default);
        Assert.True(detail.IsRead);
        Assert.NotNull(detail.ReadAt);
        Assert.Equal(RequestRecipientStatus.Open, detail.Status);

        // 3. Tenant xem lại thấy LandlordIsRead = true
        var tenantView = await service.GetTenantRequestByIdAsync(Tenant1Id, created.Id, default);
        Assert.True(tenantView.LandlordIsRead);
        Assert.NotNull(tenantView.LandlordReadAt);

        // 4. Landlord cập nhật trạng thái xử lý sang InProgress -> Resolved
        var updated = await service.UpdateLandlordRequestStatusAsync(created.Id, RequestRecipientStatus.Resolved, default);
        Assert.Equal(RequestRecipientStatus.Resolved, updated.Status);
        Assert.True(updated.IsRead);
    }

    [Fact]
    public async Task Landlord_CreateAnnouncement_supports_Single_Multiple_All_and_deduplicates_recipients()
    {
        var (service, _, propertyId) = CreateSut();

        // Single
        var single = await service.CreateLandlordAnnouncementAsync(
            propertyId,
            new CreateAnnouncementInput(RequestRecipientType.Single, [Tenant1Id], null, "Thông báo riêng", "Nội dung riêng"),
            default);
        Assert.Equal(1, single.TotalRecipients);
        Assert.Equal(0, single.ReadCount);

        // Multiple (kèm trùng lặp)
        var multiple = await service.CreateLandlordAnnouncementAsync(
            propertyId,
            new CreateAnnouncementInput(RequestRecipientType.Multiple, [Tenant1Id, Tenant2Id, Tenant1Id], null, "Thông báo nhóm", "Nội dung nhóm"),
            default);
        Assert.Equal(2, multiple.TotalRecipients);

        // All
        var all = await service.CreateLandlordAnnouncementAsync(
            propertyId,
            new CreateAnnouncementInput(RequestRecipientType.All, null, null, "Thông báo chung", "Toàn khu trọ chú ý"),
            default);
        Assert.Equal(2, all.TotalRecipients);

        // Tenant 1 đọc thông báo All
        var readResult = await service.MarkAnnouncementAsReadAsync(Tenant1Id, all.Id, default);
        Assert.True(readResult.IsRead);
        Assert.NotNull(readResult.ReadAt);
    }

    private static (RequestService Service, FakeRequestStore Store, Guid PropertyId) CreateSut()
    {
        var propertyId = Guid.NewGuid();
        var room1Id = Guid.NewGuid();
        var room2Id = Guid.NewGuid();
        var contract1Id = Guid.NewGuid();
        var contract2Id = Guid.NewGuid();

        var store = new FakeRequestStore();
        store.Properties[propertyId] = new Property
        {
            Id = propertyId,
            OwnerUserId = LandlordId,
            Name = "Khu Trọ Xanh",
            Address = "Số 1 Đại Cồ Việt",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        store.ActiveStaysByTenant[Tenant1Id] =
        [
            new TenantActiveStayInfo(propertyId, "Khu Trọ Xanh", LandlordId, room1Id, "101", contract1Id)
        ];
        store.ActiveStaysByTenant[Tenant2Id] =
        [
            new TenantActiveStayInfo(propertyId, "Khu Trọ Xanh", LandlordId, room2Id, "102", contract2Id)
        ];
        store.EligibleRecipientsByProperty[propertyId] =
        [
            new AnnouncementRecipientOptionDto(Tenant1Id, Guid.NewGuid(), "Khách 1", "0901111111", room1Id, "101", contract1Id),
            new AnnouncementRecipientOptionDto(Tenant2Id, Guid.NewGuid(), "Khách 2", "0902222222", room2Id, "102", contract2Id),
        ];

        var service = new RequestService(new FakeCurrentLandlord(LandlordId), new FakeLandlordStore(), store);
        return (service, store, propertyId);
    }

    private sealed class FakeCurrentLandlord(string userId) : ICurrentLandlord
    {
        public string GetUserId() => userId;
    }

    private sealed class FakeLandlordStore : ILandlordStore
    {
        public Task<bool> IsActiveLandlordAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(userId == LandlordId);
    }

    private sealed class FakeRequestStore : IRequestStore
    {
        public Dictionary<Guid, Property> Properties { get; } = new();
        public Dictionary<string, List<TenantActiveStayInfo>> ActiveStaysByTenant { get; } = new();
        public Dictionary<Guid, List<AnnouncementRecipientOptionDto>> EligibleRecipientsByProperty { get; } = new();
        public Dictionary<Guid, Request> Requests { get; } = new();
        public Dictionary<Guid, RequestRecipient> Recipients { get; } = new();

        public Task<IReadOnlyList<TenantActiveStayInfo>> GetTenantActiveStaysAsync(string tenantUserId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TenantActiveStayInfo>>(ActiveStaysByTenant.GetValueOrDefault(tenantUserId) ?? []);

        public Task<Property?> GetOwnedPropertyAsync(Guid propertyId, string landlordUserId, CancellationToken ct = default) =>
            Task.FromResult(Properties.TryGetValue(propertyId, out var p) && p.OwnerUserId == landlordUserId ? p : null);

        public Task<Request> AddRequestWithRecipientsAsync(Request request, IReadOnlyList<RequestRecipient> recipients, CancellationToken ct = default)
        {
            Requests[request.Id] = request;
            foreach (var r in recipients) Recipients[r.Id] = r;
            return Task.FromResult(request);
        }

        public Task<(IReadOnlyList<TenantRequestItemDto> Items, long Total)> ListTenantRequestsAsync(
            string tenantUserId, RequestRecipientStatus? status, int skip, int take, CancellationToken ct = default)
        {
            var items = Requests.Values
                .Where(r => r.SenderUserId == tenantUserId && r.Type == RequestType.Request)
                .Select(r =>
                {
                    var rec = Recipients.Values.First(x => x.RequestId == r.Id);
                    return new TenantRequestItemDto(r.Id, r.PropertyId, "Khu Trọ Xanh", "101", r.Title, r.Content, r.CreatedAt, rec.IsRead, rec.ReadAt, rec.Status);
                })
                .ToList();
            return Task.FromResult<(IReadOnlyList<TenantRequestItemDto>, long)>((items, items.Count));
        }

        public async Task<TenantRequestItemDto?> GetTenantRequestByIdAsync(Guid requestId, string tenantUserId, CancellationToken ct = default)
        {
            var (items, _) = await ListTenantRequestsAsync(tenantUserId, null, 0, 100, ct);
            return items.FirstOrDefault(x => x.Id == requestId);
        }

        public Task<(IReadOnlyList<TenantAnnouncementItemDto> Items, long Total)> ListTenantAnnouncementsAsync(
            string tenantUserId, bool? isRead, int skip, int take, CancellationToken ct = default)
        {
            var items = Recipients.Values
                .Where(rec => rec.ReceiverUserId == tenantUserId)
                .Select(rec => (rec, req: Requests[rec.RequestId]))
                .Where(x => x.req.Type == RequestType.Announcement)
                .Select(x => new TenantAnnouncementItemDto(x.req.Id, x.rec.Id, x.req.PropertyId, "Khu Trọ Xanh", "101", x.req.SenderUserId, "Chủ trọ", x.req.RecipientType, x.req.Title, x.req.Content, x.req.CreatedAt, x.rec.IsRead, x.rec.ReadAt, x.rec.Status))
                .ToList();
            return Task.FromResult<(IReadOnlyList<TenantAnnouncementItemDto>, long)>((items, items.Count));
        }

        public async Task<TenantAnnouncementItemDto?> MarkAnnouncementAsReadAsync(Guid requestId, string tenantUserId, CancellationToken ct = default)
        {
            var rec = Recipients.Values.FirstOrDefault(r => r.RequestId == requestId && r.ReceiverUserId == tenantUserId);
            if (rec is null) return null;
            rec.IsRead = true;
            rec.ReadAt ??= DateTime.UtcNow;
            var (items, _) = await ListTenantAnnouncementsAsync(tenantUserId, null, 0, 100, ct);
            return items.FirstOrDefault(x => x.Id == requestId);
        }

        public Task<(IReadOnlyList<LandlordRequestItemDto> Items, long Total)> ListLandlordRequestsAsync(
            string landlordUserId, LandlordRequestFilter filter, int skip, int take, CancellationToken ct = default)
        {
            var items = Recipients.Values
                .Where(rec => rec.ReceiverUserId == landlordUserId)
                .Select(rec => (rec, req: Requests[rec.RequestId]))
                .Where(x => x.req.Type == RequestType.Request)
                .Select(x => new LandlordRequestItemDto(x.req.Id, x.rec.Id, x.req.PropertyId, "Khu Trọ Xanh", x.req.SenderUserId, "Khách 1", "0901111111", "101", x.req.Title, x.req.Content, x.req.CreatedAt, x.rec.IsRead, x.rec.ReadAt, x.rec.Status))
                .ToList();
            return Task.FromResult<(IReadOnlyList<LandlordRequestItemDto>, long)>((items, items.Count));
        }

        public async Task<LandlordRequestItemDto?> GetLandlordRequestAndMarkReadAsync(Guid requestId, string landlordUserId, CancellationToken ct = default)
        {
            var rec = Recipients.Values.FirstOrDefault(r => r.RequestId == requestId && r.ReceiverUserId == landlordUserId);
            if (rec is null) return null;
            rec.IsRead = true;
            rec.ReadAt ??= DateTime.UtcNow;
            var (items, _) = await ListLandlordRequestsAsync(landlordUserId, new LandlordRequestFilter(1, 100, null, null, null, null), 0, 100, ct);
            return items.FirstOrDefault(x => x.Id == requestId);
        }

        public async Task<LandlordRequestItemDto?> UpdateLandlordRequestStatusAsync(Guid requestId, string landlordUserId, RequestRecipientStatus newStatus, CancellationToken ct = default)
        {
            var rec = Recipients.Values.FirstOrDefault(r => r.RequestId == requestId && r.ReceiverUserId == landlordUserId);
            if (rec is null) return null;
            rec.IsRead = true;
            rec.ReadAt ??= DateTime.UtcNow;
            rec.Status = newStatus;
            var (items, _) = await ListLandlordRequestsAsync(landlordUserId, new LandlordRequestFilter(1, 100, null, null, null, null), 0, 100, ct);
            return items.FirstOrDefault(x => x.Id == requestId);
        }

        public Task<IReadOnlyList<AnnouncementRecipientOptionDto>> ListEligibleAnnouncementRecipientsAsync(Guid propertyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AnnouncementRecipientOptionDto>>(EligibleRecipientsByProperty.GetValueOrDefault(propertyId) ?? []);

        public Task<(IReadOnlyList<LandlordAnnouncementItemDto> Items, long Total)> ListLandlordAnnouncementsAsync(
            Guid propertyId, string landlordUserId, int skip, int take, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<LandlordAnnouncementItemDto>, long)>(([], 0));
    }
}
