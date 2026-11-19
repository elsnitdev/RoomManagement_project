using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RoomManagement.Business.DTOs;
using RoomManagement.Data.Stores;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class NotificationsApiTests
{
    [Fact]
    public async Task Endpoints_enforce_role_authorization()
    {
        await using var factory = new ApiFactory();
        var admin = factory.Store.AddUser(UserRoles.Admin);
        var landlord = factory.Store.AddUser(UserRoles.Landlord);
        var tenant = factory.Store.AddUser(UserRoles.Tenant);

        using var adminClient = factory.CreateClient();
        using var landlordClient = factory.CreateClient();
        using var tenantClient = factory.CreateClient();
        await SignInAsync(adminClient, admin);
        await SignInAsync(landlordClient, landlord);
        await SignInAsync(tenantClient, tenant);

        // Tenant and Landlord cannot call Admin notification endpoints
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantClient.GetAsync("/api/admin/notifications/targets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await landlordClient.GetAsync("/api/admin/notifications/targets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await landlordClient.PostAsJsonAsync("/api/admin/notifications/send", new
        {
            scope = "AllSystem",
            title = "Test",
            message = "Test",
            idempotencyKey = Guid.NewGuid()
        })).StatusCode);

        // Tenant and Admin cannot call Operator notification endpoints
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantClient.GetAsync("/api/operator/notifications/targets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.GetAsync("/api/operator/notifications/targets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.PostAsJsonAsync("/api/operator/notifications/send", new
        {
            scope = "AllActiveTenants",
            title = "Test",
            message = "Test",
            idempotencyKey = Guid.NewGuid()
        })).StatusCode);

        // Tenant cannot access Sent History
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantClient.GetAsync("/api/me/notifications/sent")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_query_targets_send_all_scopes_and_view_sent_history()
    {
        await using var factory = new ApiFactory();
        var admin = factory.Store.AddUser(UserRoles.Admin);
        var landlord1 = factory.Store.AddUser(UserRoles.Landlord);
        landlord1.FullName = "Chủ Trọ An";
        landlord1.LandlordCode = "CT-001";
        var landlord2 = factory.Store.AddUser(UserRoles.Landlord);
        landlord2.FullName = "Chủ Trọ Bình";
        var pendingLandlord = factory.Store.AddUser(UserRoles.Landlord, ApprovalStatuses.Pending);
        var tenant = factory.Store.AddUser(UserRoles.Tenant);

        var propId = Guid.NewGuid();
        factory.NotificationStore.Properties[propId] = new Property
        {
            Id = propId,
            OwnerUserId = landlord1.Id,
            Name = "Khu Trọ An",
            Address = "123 Lê Lợi",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, admin);

        // 1. Targets exclude actor admin and pending/locked accounts
        var targetsRes = await adminClient.GetAsync("/api/admin/notifications/targets");
        Assert.Equal(HttpStatusCode.OK, targetsRes.StatusCode);
        var targetsData = (await targetsRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(3, targetsData.GetProperty("totalActiveSystemUsers").GetInt32());
        Assert.Equal(2, targetsData.GetProperty("totalActiveLandlords").GetInt32());
        Assert.Equal(2, targetsData.GetProperty("landlords").GetArrayLength());

        // 2. Send AllSystem
        var allSysRes = await adminClient.PostAsJsonAsync("/api/admin/notifications/send", new
        {
            scope = "AllSystem",
            title = "Thông báo bảo trì toàn hệ thống",
            message = "Hệ thống cập nhật lúc 00:00.",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, allSysRes.StatusCode);
        var allSysData = (await allSysRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(3, allSysData.GetProperty("recipientCount").GetInt32());
        Assert.Equal("SystemBroadcast", allSysData.GetProperty("type").GetString());

        // 3. Send AllLandlords
        var allLandlordsRes = await adminClient.PostAsJsonAsync("/api/admin/notifications/send", new
        {
            scope = "AllLandlords",
            title = "Thông báo dành cho chủ trọ",
            message = "Cập nhật quy định quản lý phòng.",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, allLandlordsRes.StatusCode);
        var allLandlordsData = (await allLandlordsRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(2, allLandlordsData.GetProperty("recipientCount").GetInt32());
        Assert.Equal("AdminLandlordBroadcast", allLandlordsData.GetProperty("type").GetString());

        // 4. Send SpecificLandlord
        var directKey = Guid.NewGuid();
        var directRes = await adminClient.PostAsJsonAsync("/api/admin/notifications/send", new
        {
            scope = "SpecificLandlord",
            targetUserId = landlord1.Id,
            title = "Nhắc nhở hồ sơ",
            message = "Vui lòng cập nhật thông tin ngân hàng.",
            idempotencyKey = directKey
        });
        Assert.Equal(HttpStatusCode.OK, directRes.StatusCode);
        var directData = (await directRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1, directData.GetProperty("recipientCount").GetInt32());
        Assert.Equal("AdminDirectLandlord", directData.GetProperty("type").GetString());

        // 5. Reject pending landlord
        var invalidRes = await adminClient.PostAsJsonAsync("/api/admin/notifications/send", new
        {
            scope = "SpecificLandlord",
            targetUserId = pendingLandlord.Id,
            title = "Test",
            message = "Test",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidRes.StatusCode);

        // 6. Legacy broadcast route still works
        var legacyRes = await adminClient.PostAsJsonAsync("/api/admin/notifications/broadcast", new
        {
            title = "Legacy broadcast",
            message = "Nội dung cũ vẫn hoạt động",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, legacyRes.StatusCode);
        var legacyData = (await legacyRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(3, legacyData.GetProperty("recipients").GetInt32());

        // 7. Admin Sent History lists all 4 batches
        var sentRes = await adminClient.GetAsync("/api/me/notifications/sent?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, sentRes.StatusCode);
        var sentJson = await sentRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, sentJson.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task Landlord_and_Manager_targets_send_isolation_and_sent_history_work()
    {
        await using var factory = new ApiFactory();
        var landlord = factory.Store.AddUser(UserRoles.Landlord);
        landlord.FullName = "Chủ Trọ Minh";

        var manager = factory.Store.AddUser(UserRoles.Manager);
        manager.LandlordUserId = landlord.Id;
        manager.FullName = "Quản Lý Lan";

        var tenant1 = factory.Store.AddUser(UserRoles.Tenant);
        var tenant2 = factory.Store.AddUser(UserRoles.Tenant);

        var prop1 = Guid.NewGuid();
        var prop2 = Guid.NewGuid();

        factory.NotificationStore.Properties[prop1] = new Property
        {
            Id = prop1,
            OwnerUserId = landlord.Id,
            Name = "Khu Trọ Xanh 1",
            Address = "10 Nguyễn Huệ",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        factory.NotificationStore.Properties[prop2] = new Property
        {
            Id = prop2,
            OwnerUserId = landlord.Id,
            Name = "Khu Trọ Xanh 2",
            Address = "20 Hai Bà Trưng",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        factory.NotificationStore.ManagerAssignments[manager.Id] = [prop1];

        factory.NotificationStore.ActiveStays.AddRange([
            new TestActiveStay(tenant1.Id, "Nguyễn Văn Một", "0901111111", tenant1.Email, prop1, "Khu Trọ Xanh 1", Guid.NewGuid(), "P101", Guid.NewGuid()),
            new TestActiveStay(tenant1.Id, "Nguyễn Văn Một", "0901111111", tenant1.Email, prop2, "Khu Trọ Xanh 2", Guid.NewGuid(), "P201", Guid.NewGuid()),
            new TestActiveStay(tenant2.Id, "Trần Thị Hai", "0902222222", tenant2.Email, prop2, "Khu Trọ Xanh 2", Guid.NewGuid(), "P202", Guid.NewGuid())
        ]);

        using var landlordClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        await SignInAsync(landlordClient, landlord);
        await SignInAsync(managerClient, manager);

        // Landlord sees 2 properties & 2 unique active tenants
        var landlordTargetsRes = await landlordClient.GetAsync("/api/operator/notifications/targets");
        Assert.Equal(HttpStatusCode.OK, landlordTargetsRes.StatusCode);
        var landlordTargets = (await landlordTargetsRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(2, landlordTargets.GetProperty("totalActiveTenants").GetInt32());
        Assert.Equal(2, landlordTargets.GetProperty("properties").GetArrayLength());

        // Landlord sends to AllActiveTenants -> 2 recipients (tenant1 deduplicated)
        var landlordAllRes = await landlordClient.PostAsJsonAsync("/api/operator/notifications/send", new
        {
            scope = "AllActiveTenants",
            title = "Thông báo toàn bộ khu trọ",
            message = "Kiểm tra hệ thống PCCC cuối tuần.",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, landlordAllRes.StatusCode);
        Assert.Equal(2, (await landlordAllRes.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("recipientCount").GetInt32());

        // Landlord sends to SpecificProperty (prop2) -> 2 recipients (tenant1 + tenant2)
        var landlordPropRes = await landlordClient.PostAsJsonAsync("/api/operator/notifications/send", new
        {
            scope = "SpecificProperty",
            propertyId = prop2,
            title = "Lịch vệ sinh Khu 2",
            message = "Chủ nhật 8h sáng.",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, landlordPropRes.StatusCode);
        Assert.Equal(2, (await landlordPropRes.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("recipientCount").GetInt32());

        // Manager sees ONLY prop1 & tenant1
        var managerTargetsRes = await managerClient.GetAsync("/api/operator/notifications/targets");
        Assert.Equal(HttpStatusCode.OK, managerTargetsRes.StatusCode);
        var managerTargets = (await managerTargetsRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1, managerTargets.GetProperty("totalActiveTenants").GetInt32());
        Assert.Single(managerTargets.GetProperty("properties").EnumerateArray());
        Assert.Equal(prop1, managerTargets.GetProperty("properties")[0].GetProperty("propertyId").GetGuid());

        // Manager sends SpecificTenant (tenant1)
        var managerDirectRes = await managerClient.PostAsJsonAsync("/api/operator/notifications/send", new
        {
            scope = "SpecificTenant",
            targetUserId = tenant1.Id,
            title = "Nhắc riêng P101",
            message = "Vui lòng đóng cửa cổng sau 23h.",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, managerDirectRes.StatusCode);
        Assert.Equal(1, (await managerDirectRes.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("recipientCount").GetInt32());

        // Manager CANNOT send to unassigned prop2 -> 404
        var managerForbiddenProp = await managerClient.PostAsJsonAsync("/api/operator/notifications/send", new
        {
            scope = "SpecificProperty",
            propertyId = prop2,
            title = "Không có quyền",
            message = "Thử gửi sang khu 2",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.NotFound, managerForbiddenProp.StatusCode);

        // Manager CANNOT send to tenant2 (only in prop2) -> 422
        var managerForbiddenTenant = await managerClient.PostAsJsonAsync("/api/operator/notifications/send", new
        {
            scope = "SpecificTenant",
            targetUserId = tenant2.Id,
            title = "Không có quyền",
            message = "Thử gửi cho tenant2",
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, managerForbiddenTenant.StatusCode);

        // Sent history shows 2 items for landlord and 1 item for manager
        var landlordSent = await (await landlordClient.GetAsync("/api/me/notifications/sent"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, landlordSent.GetProperty("data").GetArrayLength());

        var managerSent = await (await managerClient.GetAsync("/api/me/notifications/sent"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(managerSent.GetProperty("data").EnumerateArray());
    }

    [SqlServerFact]
    public async Task SqlServer_NotificationStore_translates_targets_batch_and_sent_history_accurately()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var adminId = "notif-admin-1";
        var landlordId = "notif-landlord-1";
        var pendingLandlordId = "notif-landlord-pending";
        var managerId = "notif-manager-1";
        var activeTenantId = "notif-tenant-active";
        var expiredTenantId = "notif-tenant-expired";

        var prop1 = Guid.NewGuid();
        var prop2 = Guid.NewGuid();
        var room1 = Guid.NewGuid();
        var room2 = Guid.NewGuid();
        var roomExpired = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        var cExpired = Guid.NewGuid();
        var profActive = Guid.NewGuid();
        var profExpired = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        context.Users.AddRange(
            MakeSqlUser(adminId, UserRoles.Admin, "Admin Chính"),
            MakeSqlUser(landlordId, UserRoles.Landlord, "Chủ Trọ A"),
            MakeSqlUser(pendingLandlordId, UserRoles.Landlord, "Chủ Trọ Chờ", approvalStatus: ApprovalStatuses.Pending),
            MakeSqlUser(managerId, UserRoles.Manager, "Quản Lý B", landlordUserId: landlordId),
            MakeSqlUser(activeTenantId, UserRoles.Tenant, "Thuê Active"),
            MakeSqlUser(expiredTenantId, UserRoles.Tenant, "Thuê Expired"));

        context.Properties.AddRange(
            new Property { Id = prop1, OwnerUserId = landlordId, Name = "Khu A", Address = "1 Đường A", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Property { Id = prop2, OwnerUserId = landlordId, Name = "Khu B", Address = "2 Đường B", IsActive = true, CreatedAt = DateTime.UtcNow });

        context.PropertyManagerAssignments.Add(new PropertyManagerAssignment
        {
            Id = Guid.NewGuid(),
            PropertyId = prop1,
            ManagerUserId = managerId,
            AssignedByUserId = landlordId,
            AssignedAt = DateTime.UtcNow
        });

        context.Rooms.AddRange(
            new Room { Id = room1, PropertyId = prop1, RoomNumber = "101", MaxOccupants = 2, BasePrice = 3_000_000, Status = RoomStatus.Occupied, CreatedAt = DateTime.UtcNow },
            new Room { Id = room2, PropertyId = prop2, RoomNumber = "201", MaxOccupants = 2, BasePrice = 3_000_000, Status = RoomStatus.Occupied, CreatedAt = DateTime.UtcNow },
            new Room { Id = roomExpired, PropertyId = prop1, RoomNumber = "102", MaxOccupants = 2, BasePrice = 3_000_000, Status = RoomStatus.Available, CreatedAt = DateTime.UtcNow });

        context.Contracts.AddRange(
            new Contract { Id = c1, RoomId = room1, ContractCode = "HD-N1", StartDate = today.AddDays(-10), MonthlyRent = 3_000_000, Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow },
            new Contract { Id = c2, RoomId = room2, ContractCode = "HD-N2", StartDate = today.AddDays(-5), MonthlyRent = 3_000_000, Status = ContractStatus.Active, CreatedAt = DateTime.UtcNow },
            new Contract { Id = cExpired, RoomId = roomExpired, ContractCode = "HD-N3", StartDate = today.AddDays(-40), EndDate = today.AddDays(-1), MonthlyRent = 3_000_000, Status = ContractStatus.Expired, CreatedAt = DateTime.UtcNow });

        context.TenantProfiles.AddRange(
            new TenantProfile { Id = profActive, UserId = activeTenantId, CreatedByUserId = landlordId, FullName = "Thuê Active", PhoneNumber = "0901111111", CreatedAt = DateTime.UtcNow },
            new TenantProfile { Id = profExpired, UserId = expiredTenantId, CreatedByUserId = landlordId, FullName = "Thuê Expired", PhoneNumber = "0902222222", CreatedAt = DateTime.UtcNow });

        context.ContractTenants.AddRange(
            new ContractTenant { Id = Guid.NewGuid(), ContractId = c1, TenantId = profActive, JoinDate = today.AddDays(-10), IsContactPerson = true, CreatedAt = DateTime.UtcNow },
            new ContractTenant { Id = Guid.NewGuid(), ContractId = c2, TenantId = profActive, JoinDate = today.AddDays(-5), IsContactPerson = true, CreatedAt = DateTime.UtcNow },
            new ContractTenant { Id = Guid.NewGuid(), ContractId = cExpired, TenantId = profExpired, JoinDate = today.AddDays(-40), IsContactPerson = true, CreatedAt = DateTime.UtcNow });

        await context.SaveChangesAsync();

        var store = new NotificationStore(context);

        var adminTargets = await store.GetAdminTargetsAsync(adminId, null);
        Assert.Equal(4, adminTargets.TotalActiveSystemUsers); // landlord, manager, 2 tenants (excludes admin & pending landlord)
        Assert.Equal(1, adminTargets.TotalActiveLandlords);
        Assert.Single(adminTargets.Landlords);
        Assert.Equal(2, adminTargets.Landlords[0].ActivePropertyCount);

        var landlordContext = new OperatorContext(landlordId, landlordId, UserRoles.Landlord, false);
        var landlordTargets = await store.GetOperatorTargetsAsync(landlordContext, null);
        Assert.Equal(1, landlordTargets.TotalActiveTenants); // expiredTenantId excluded; activeTenantId deduplicated across 2 rooms
        Assert.Equal(2, landlordTargets.Tenants[0].ActiveStays.Count);

        var managerContext = new OperatorContext(managerId, landlordId, UserRoles.Manager, true);
        var managerTargets = await store.GetOperatorTargetsAsync(managerContext, null);
        Assert.Single(managerTargets.Properties);
        Assert.Equal(prop1, managerTargets.Properties[0].PropertyId);
        Assert.Null(await store.ResolveActiveTenantPropertyIdForOperatorAsync(managerContext, expiredTenantId, null));

        var key = $"operator-notif:{Guid.NewGuid():N}";
        var notification = new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = activeTenantId,
            ActorUserId = landlordId,
            Type = NotificationTypes.PropertyAnnouncement,
            Title = "Thông báo SQL",
            Message = "Kiểm tra truy vấn SQL Server",
            PropertyId = prop1,
            EventKey = key,
            IsRead = true,
            ReadAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        Assert.True(await store.AddNotificationsBatchAsync([notification]));
        Assert.False(await store.AddNotificationsBatchAsync([new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = activeTenantId,
            ActorUserId = landlordId,
            Type = NotificationTypes.PropertyAnnouncement,
            Title = "Trùng",
            Message = "Trùng key",
            PropertyId = prop1,
            EventKey = key,
            CreatedAt = DateTime.UtcNow
        }]));

        var (sent, totalSent) = await store.ListSentNotificationsAsync(landlordId, 0, 20);
        Assert.Equal(1, totalSent);
        Assert.Equal(1, sent[0].TotalRecipients);
        Assert.Equal(1, sent[0].ReadCount);
        Assert.Equal("Khu A", sent[0].PropertyName);
        Assert.Equal("Khu trọ: Khu A", sent[0].TargetLabel);
    }

    private static AppUser MakeSqlUser(
        string id,
        string role,
        string fullName,
        string approvalStatus = ApprovalStatuses.Approved,
        string? landlordUserId = null) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        FullName = fullName,
        Email = $"{id}@example.com",
        NormalizedEmail = $"{id}@example.com".ToUpperInvariant(),
        PasswordHash = "hash",
        Role = role,
        LandlordUserId = landlordUserId,
        IsActive = true,
        ApprovalStatus = approvalStatus,
        CreatedAt = DateTime.UtcNow
    };

    private static async Task SignInAsync(HttpClient client, AppUser user)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userNameOrEmail = user.Email,
            password = TestUserStore.Password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", json.GetProperty("accessToken").GetString());
    }
}
