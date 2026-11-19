using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;
using Xunit;

namespace RoomManagement.Business.Tests;

public sealed class NotificationServiceTests
{
    private const string AdminId = "admin-001";
    private const string LandlordId = "landlord-001";
    private const string ManagerId = "manager-001";
    private const string OtherLandlordId = "landlord-002";
    private const string Tenant1Id = "tenant-001";
    private const string Tenant2Id = "tenant-002";
    private const string OutsideTenantId = "tenant-outside";

    [Fact]
    public async Task Admin_can_send_to_AllSystem_AllLandlords_and_SpecificLandlord()
    {
        var (service, store, currentLandlord, _, _) = CreateSut();
        currentLandlord.UserId = AdminId;

        store.Users.AddRange([
            MakeUser(AdminId, UserRoles.Admin, "Quản trị"),
            MakeUser(LandlordId, UserRoles.Landlord, "Chủ trọ A"),
            MakeUser(OtherLandlordId, UserRoles.Landlord, "Chủ trọ B"),
            MakeUser("landlord-pending", UserRoles.Landlord, "Chủ trọ chờ duyệt", approvalStatus: ApprovalStatuses.Pending),
            MakeUser("landlord-locked", UserRoles.Landlord, "Chủ trọ bị khóa", isActive: false),
            MakeUser(Tenant1Id, UserRoles.Tenant, "Người thuê 1"),
        ]);

        // 1. AllSystem excludes actorAdminId and inactive/pending users
        var allSys = await service.SendAdminNotificationAsync(
            AdminId,
            new SendAdminNotificationInput(AdminNotificationTargetScope.AllSystem, null, "Bảo trì hệ thống", "Tối nay bảo trì lúc 23h", Guid.NewGuid()),
            default);
        Assert.Equal(NotificationTypes.SystemBroadcast, allSys.Type);
        Assert.Equal(3, allSys.RecipientCount);
        Assert.False(allSys.Duplicate);
        Assert.DoesNotContain(store.Notifications, n => n.UserId == AdminId && n.EventKey == allSys.EventKey);

        // 2. AllLandlords targets only active approved landlords
        var allLandlords = await service.SendAdminNotificationAsync(
            AdminId,
            new SendAdminNotificationInput(AdminNotificationTargetScope.AllLandlords, null, "Cập nhật chính sách", "Vui lòng đọc thông báo mới", Guid.NewGuid()),
            default);
        Assert.Equal(NotificationTypes.AdminLandlordBroadcast, allLandlords.Type);
        Assert.Equal(2, allLandlords.RecipientCount);

        // 3. SpecificLandlord
        var key = Guid.NewGuid();
        var direct = await service.SendAdminNotificationAsync(
            AdminId,
            new SendAdminNotificationInput(AdminNotificationTargetScope.SpecificLandlord, LandlordId, "Nhắc nhở riêng", "Kiểm tra thông tin khu trọ", key),
            default);
        Assert.Equal(NotificationTypes.AdminDirectLandlord, direct.Type);
        Assert.Equal(1, direct.RecipientCount);
        Assert.Equal(LandlordId, store.Notifications.Single(n => n.EventKey == direct.EventKey).UserId);

        // 4. Duplicate idempotencyKey returns Duplicate = true without adding more rows
        var duplicate = await service.SendAdminNotificationAsync(
            AdminId,
            new SendAdminNotificationInput(AdminNotificationTargetScope.SpecificLandlord, LandlordId, "Nhắc nhở riêng", "Kiểm tra thông tin khu trọ", key),
            default);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(0, duplicate.RecipientCount);
    }

    [Fact]
    public async Task Admin_SpecificLandlord_rejects_empty_unapproved_locked_or_wrong_role_and_empty_targets()
    {
        var (service, store, _, _, _) = CreateSut();
        store.Users.AddRange([
            MakeUser(AdminId, UserRoles.Admin, "Quản trị"),
            MakeUser("landlord-pending", UserRoles.Landlord, "Chờ duyệt", approvalStatus: ApprovalStatuses.Pending),
            MakeUser(Tenant1Id, UserRoles.Tenant, "Tenant")
        ]);

        var missingTarget = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendAdminNotificationAsync(
                AdminId,
                new SendAdminNotificationInput(AdminNotificationTargetScope.SpecificLandlord, " ", "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("RECIPIENT_REQUIRED", missingTarget.Code);
        Assert.Equal(BusinessErrorType.Validation, missingTarget.Type);

        var pendingTarget = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendAdminNotificationAsync(
                AdminId,
                new SendAdminNotificationInput(AdminNotificationTargetScope.SpecificLandlord, "landlord-pending", "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("INVALID_RECIPIENT", pendingTarget.Code);
        Assert.Equal(BusinessErrorType.InvalidOperation, pendingTarget.Type);

        var wrongRole = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendAdminNotificationAsync(
                AdminId,
                new SendAdminNotificationInput(AdminNotificationTargetScope.SpecificLandlord, Tenant1Id, "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("INVALID_RECIPIENT", wrongRole.Code);

        var noLandlords = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendAdminNotificationAsync(
                AdminId,
                new SendAdminNotificationInput(AdminNotificationTargetScope.AllLandlords, null, "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("NO_ELIGIBLE_RECIPIENTS", noLandlords.Code);
        Assert.Equal(BusinessErrorType.InvalidOperation, noLandlords.Type);
    }

    [Fact]
    public async Task Landlord_can_send_AllActiveTenants_SpecificProperty_SpecificTenant_with_dedup_and_validation()
    {
        var (service, store, currentLandlord, prop1, prop2) = CreateSut();
        currentLandlord.UserId = LandlordId;

        var emptyPropId = Guid.NewGuid();
        store.Properties.AddRange([
            new Property { Id = prop1, OwnerUserId = LandlordId, Name = "Khu A", Address = "1 Đường A", IsActive = true },
            new Property { Id = prop2, OwnerUserId = LandlordId, Name = "Khu B", Address = "2 Đường B", IsActive = true },
            new Property { Id = emptyPropId, OwnerUserId = LandlordId, Name = "Khu Trống", Address = "3 Đường C", IsActive = true },
            new Property { Id = Guid.NewGuid(), OwnerUserId = OtherLandlordId, Name = "Khu Khác", Address = "4 Đường D", IsActive = true }
        ]);
        // Tenant1 has active stays in BOTH prop1 and prop2 (must deduplicate to 1 notification in AllActiveTenants)
        store.ActiveStays.AddRange([
            new FakeStay(Tenant1Id, "Thuê Một", "0901111111", null, prop1, "Khu A", Guid.NewGuid(), "101", Guid.NewGuid()),
            new FakeStay(Tenant1Id, "Thuê Một", "0901111111", null, prop2, "Khu B", Guid.NewGuid(), "202", Guid.NewGuid()),
            new FakeStay(Tenant2Id, "Thuê Hai", "0902222222", null, prop2, "Khu B", Guid.NewGuid(), "203", Guid.NewGuid()),
            new FakeStay(OutsideTenantId, "Thuê Ngoài", "0909999999", null, store.Properties[3].Id, "Khu Khác", Guid.NewGuid(), "901", Guid.NewGuid()),
        ]);

        // 1. AllActiveTenants
        var all = await service.SendOperatorNotificationAsync(
            new SendOperatorNotificationInput(OperatorNotificationTargetScope.AllActiveTenants, null, null, "Thông báo chung", "Nội quy phòng trọ mới", Guid.NewGuid()),
            default);
        Assert.Equal(NotificationTypes.OperatorAllTenants, all.Type);
        Assert.Equal(2, all.RecipientCount);

        // 2. SpecificProperty
        var byProp = await service.SendOperatorNotificationAsync(
            new SendOperatorNotificationInput(OperatorNotificationTargetScope.SpecificProperty, prop1, null, "Cúp điện Khu A", "Sáng mai bảo trì điện", Guid.NewGuid()),
            default);
        Assert.Equal(NotificationTypes.PropertyAnnouncement, byProp.Type);
        Assert.Equal(1, byProp.RecipientCount);

        // 3. SpecificTenant
        var byTenant = await service.SendOperatorNotificationAsync(
            new SendOperatorNotificationInput(OperatorNotificationTargetScope.SpecificTenant, null, Tenant2Id, "Nhắc riêng", "Kiểm tra đồng hồ nước", Guid.NewGuid()),
            default);
        Assert.Equal(NotificationTypes.OperatorDirectTenant, byTenant.Type);
        Assert.Equal(1, byTenant.RecipientCount);

        // 4. Empty property -> 422 NO_ELIGIBLE_RECIPIENTS
        var emptyEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendOperatorNotificationAsync(
                new SendOperatorNotificationInput(OperatorNotificationTargetScope.SpecificProperty, emptyPropId, null, "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("NO_ELIGIBLE_RECIPIENTS", emptyEx.Code);

        // 5. Unowned property -> 404 PROPERTY_NOT_FOUND
        var unownedEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendOperatorNotificationAsync(
                new SendOperatorNotificationInput(OperatorNotificationTargetScope.SpecificProperty, store.Properties[3].Id, null, "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("PROPERTY_NOT_FOUND", unownedEx.Code);
        Assert.Equal(BusinessErrorType.NotFound, unownedEx.Type);

        // 6. Outside tenant -> 422 INVALID_RECIPIENT
        var outsideEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendOperatorNotificationAsync(
                new SendOperatorNotificationInput(OperatorNotificationTargetScope.SpecificTenant, null, OutsideTenantId, "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("INVALID_RECIPIENT", outsideEx.Code);
        Assert.Equal(BusinessErrorType.InvalidOperation, outsideEx.Type);
    }

    [Fact]
    public async Task Manager_is_strictly_scoped_to_assigned_properties()
    {
        var (service, store, currentLandlord, prop1, prop2) = CreateSut();
        currentLandlord.UserId = ManagerId;

        store.Properties.AddRange([
            new Property { Id = prop1, OwnerUserId = LandlordId, Name = "Khu Được Giao", Address = "1 Đường A", IsActive = true },
            new Property { Id = prop2, OwnerUserId = LandlordId, Name = "Khu Chưa Giao", Address = "2 Đường B", IsActive = true },
        ]);
        store.AssignedProperties[ManagerId] = [prop1];
        store.ActiveStays.AddRange([
            new FakeStay(Tenant1Id, "Thuê Được Giao", "0901", null, prop1, "Khu Được Giao", Guid.NewGuid(), "101", Guid.NewGuid()),
            new FakeStay(Tenant2Id, "Thuê Chưa Giao", "0902", null, prop2, "Khu Chưa Giao", Guid.NewGuid(), "201", Guid.NewGuid()),
        ]);

        var targets = await service.GetOperatorTargetsAsync(null, default);
        Assert.Equal(1, targets.TotalActiveTenants);
        Assert.Single(targets.Properties);
        Assert.Equal(prop1, targets.Properties[0].PropertyId);
        Assert.Single(targets.Tenants);
        Assert.Equal(Tenant1Id, targets.Tenants[0].UserId);

        var allInScope = await service.SendOperatorNotificationAsync(
            new SendOperatorNotificationInput(OperatorNotificationTargetScope.AllActiveTenants, null, null, "Từ Quản lý", "Thông báo khu được giao", Guid.NewGuid()),
            default);
        Assert.Equal(1, allInScope.RecipientCount);
        Assert.Equal(Tenant1Id, store.Notifications.Single(n => n.EventKey == allInScope.EventKey).UserId);

        var unassignedPropEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendOperatorNotificationAsync(
                new SendOperatorNotificationInput(OperatorNotificationTargetScope.SpecificProperty, prop2, null, "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("PROPERTY_NOT_FOUND", unassignedPropEx.Code);

        var outsideTenantEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendOperatorNotificationAsync(
                new SendOperatorNotificationInput(OperatorNotificationTargetScope.SpecificTenant, null, Tenant2Id, "Tiêu đề", "Nội dung", Guid.NewGuid()),
                default));
        Assert.Equal("INVALID_RECIPIENT", outsideTenantEx.Code);
    }

    private static (
        NotificationService Service,
        FakeNotificationStore Store,
        FakeCurrentLandlord CurrentLandlord,
        Guid Prop1,
        Guid Prop2) CreateSut()
    {
        var current = new FakeCurrentLandlord();
        var landlords = new FakeLandlordStore();
        var store = new FakeNotificationStore();
        return (new NotificationService(current, landlords, store), store, current, Guid.NewGuid(), Guid.NewGuid());
    }

    private static AppUser MakeUser(
        string id,
        string role,
        string fullName,
        bool isActive = true,
        string approvalStatus = ApprovalStatuses.Approved) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        FullName = fullName,
        Email = $"{id}@example.com",
        Role = role,
        IsActive = isActive,
        ApprovalStatus = approvalStatus,
        CreatedAt = DateTime.UtcNow
    };

    private sealed class FakeCurrentLandlord : ICurrentLandlord
    {
        public string UserId { get; set; } = "";
        public string GetUserId() => UserId;
    }

    private sealed class FakeLandlordStore : ILandlordStore
    {
        public Task<bool> IsActiveLandlordAsync(string userId, CancellationToken ct) =>
            Task.FromResult(userId == LandlordId || userId == OtherLandlordId);

        public Task<OperatorContext?> GetOperatorContextAsync(string userId, CancellationToken ct) => userId switch
        {
            LandlordId => Task.FromResult<OperatorContext?>(new OperatorContext(LandlordId, LandlordId, UserRoles.Landlord, false)),
            OtherLandlordId => Task.FromResult<OperatorContext?>(new OperatorContext(OtherLandlordId, OtherLandlordId, UserRoles.Landlord, false)),
            ManagerId => Task.FromResult<OperatorContext?>(new OperatorContext(ManagerId, LandlordId, UserRoles.Manager, true)),
            _ => Task.FromResult<OperatorContext?>(null)
        };
    }

    private sealed record FakeStay(
        string UserId,
        string FullName,
        string? PhoneNumber,
        string? Email,
        Guid PropertyId,
        string PropertyName,
        Guid RoomId,
        string RoomNumber,
        Guid ContractId);

    private sealed class FakeNotificationStore : INotificationStore
    {
        public List<AppUser> Users { get; } = [];
        public List<Property> Properties { get; } = [];
        public Dictionary<string, HashSet<Guid>> AssignedProperties { get; } = new(StringComparer.Ordinal);
        public List<FakeStay> ActiveStays { get; } = [];
        public List<UserNotification> Notifications { get; } = [];

        private IEnumerable<Property> Accessible(OperatorContext context)
        {
            var baseProps = Properties.Where(p => p.OwnerUserId == context.OwnerLandlordUserId && p.IsActive);
            if (!context.IsManager) return baseProps;
            var assigned = AssignedProperties.GetValueOrDefault(context.ActorUserId) ?? [];
            return baseProps.Where(p => assigned.Contains(p.Id));
        }

        public Task<AdminNotificationTargetsDto> GetAdminTargetsAsync(string actorAdminId, string? search, CancellationToken ct = default)
        {
            var activeUsers = Users.Where(u => u.Id != actorAdminId && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved).ToList();
            var landlords = Users.Where(u => u.Role == UserRoles.Landlord && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved)
                .Select(u => new AdminLandlordOptionDto(u.Id, u.FullName ?? u.UserName, u.UserName, u.Email, u.PhoneNumber, u.LandlordCode, Properties.Count(p => p.OwnerUserId == u.Id && p.IsActive)))
                .ToList();
            return Task.FromResult(new AdminNotificationTargetsDto(activeUsers.Count, landlords.Count, landlords));
        }

        public Task<IReadOnlyList<string>> GetActiveSystemUserIdsAsync(string excludeUserId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Users
                .Where(u => u.Id != excludeUserId && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved)
                .Select(u => u.Id)
                .ToList());

        public Task<IReadOnlyList<string>> GetActiveLandlordUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Users
                .Where(u => u.Role == UserRoles.Landlord && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved)
                .Select(u => u.Id)
                .ToList());

        public Task<bool> IsActiveApprovedLandlordAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(Users.Any(u => u.Id == userId && u.Role == UserRoles.Landlord && u.IsActive && u.DeletedAt == null && u.ApprovalStatus == ApprovalStatuses.Approved));

        public Task<OperatorNotificationTargetsDto> GetOperatorTargetsAsync(OperatorContext context, string? search, CancellationToken ct = default)
        {
            var props = Accessible(context).ToList();
            var propIds = props.Select(p => p.Id).ToHashSet();
            var stays = ActiveStays.Where(s => propIds.Contains(s.PropertyId)).ToList();
            var propOptions = props.Select(p => new OperatorPropertyOptionDto(
                p.Id, p.Name, p.Address,
                stays.Where(s => s.PropertyId == p.Id).Select(s => s.UserId).Distinct().Count())).ToList();
            var tenants = stays.GroupBy(s => s.UserId)
                .Select(g =>
                {
                    var first = g.First();
                    return new OperatorTenantOptionDto(
                        g.Key,
                        first.FullName,
                        first.PhoneNumber,
                        first.Email,
                        g.Select(x => new OperatorTenantStayDto(x.PropertyId, x.PropertyName, x.RoomId, x.RoomNumber, x.ContractId)).ToList());
                }).ToList();
            return Task.FromResult(new OperatorNotificationTargetsDto(tenants.Count, propOptions, tenants));
        }

        public Task<bool> IsActivePropertyAccessibleAsync(OperatorContext context, Guid propertyId, CancellationToken ct = default) =>
            Task.FromResult(Accessible(context).Any(p => p.Id == propertyId));

        public Task<IReadOnlyList<string>> GetActiveTenantUserIdsForOperatorAsync(OperatorContext context, Guid? propertyId, CancellationToken ct = default)
        {
            var propIds = Accessible(context)
                .Where(p => !propertyId.HasValue || p.Id == propertyId.Value)
                .Select(p => p.Id)
                .ToHashSet();
            return Task.FromResult<IReadOnlyList<string>>(ActiveStays
                .Where(s => propIds.Contains(s.PropertyId))
                .Select(s => s.UserId)
                .Distinct()
                .ToList());
        }

        public Task<Guid?> ResolveActiveTenantPropertyIdForOperatorAsync(OperatorContext context, string tenantUserId, Guid? preferredPropertyId, CancellationToken ct = default)
        {
            var propIds = Accessible(context)
                .Where(p => !preferredPropertyId.HasValue || p.Id == preferredPropertyId.Value)
                .Select(p => p.Id)
                .ToHashSet();
            var stay = ActiveStays.FirstOrDefault(s => s.UserId == tenantUserId && propIds.Contains(s.PropertyId));
            return Task.FromResult<Guid?>(stay?.PropertyId);
        }

        public Task<bool> EventKeyExistsAsync(string eventKey, CancellationToken ct = default) =>
            Task.FromResult(Notifications.Any(n => n.EventKey == eventKey));

        public Task<bool> AddNotificationsBatchAsync(IReadOnlyList<UserNotification> notifications, CancellationToken ct = default)
        {
            if (notifications.Any(n => Notifications.Any(x => x.UserId == n.UserId && x.EventKey == n.EventKey)))
                return Task.FromResult(false);
            Notifications.AddRange(notifications);
            return Task.FromResult(true);
        }

        public Task<(IReadOnlyList<SentNotificationItemDto> Items, long Total)> ListSentNotificationsAsync(string actorUserId, int skip, int take, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<SentNotificationItemDto>, long)>(([], 0));
    }
}
