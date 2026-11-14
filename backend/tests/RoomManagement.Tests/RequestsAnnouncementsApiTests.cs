using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Data.Stores;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Tests;

public sealed class RequestsAnnouncementsApiTests
{
    [Fact]
    public async Task Tenant_without_active_contract_gets_422_when_creating_request()
    {
        await using var factory = new ApiFactory();
        var tenant = factory.Store.AddUser(UserRoles.Tenant);
        using var client = factory.CreateClient();
        await SignInAsync(client, tenant);

        var response = await client.PostAsJsonAsync("/api/me/requests", new
        {
            title = "Hỏng vòi nước",
            content = "Vòi nước trong phòng vệ sinh bị rỉ nước liên tục."
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NO_ACTIVE_CONTRACT", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task End_to_end_tenant_sends_request_landlord_views_and_updates_status()
    {
        await using var factory = new ApiFactory();
        var landlord = factory.Store.AddUser(UserRoles.Landlord);
        var tenant = factory.Store.AddUser(UserRoles.Tenant);

        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        factory.RequestStore.Properties[propertyId] = new Property
        {
            Id = propertyId,
            OwnerUserId = landlord.Id,
            Name = "Khu trọ Ánh Dương",
            Address = "123 Nguyễn Văn Cừ",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        factory.RequestStore.UsersInfo[tenant.Id] = ("Nguyễn Văn Thuê", "0901234567");
        factory.RequestStore.ActiveStaysByTenant[tenant.Id] =
        [
            new TenantActiveStayInfo(propertyId, "Khu trọ Ánh Dương", landlord.Id, roomId, "P.201", contractId)
        ];

        using var tenantClient = factory.CreateClient();
        await SignInAsync(tenantClient, tenant);

        // 1. Tenant gửi yêu cầu
        var createRes = await tenantClient.PostAsJsonAsync("/api/me/requests", new
        {
            title = "  Máy lạnh không mát  ",
            content = "  Máy lạnh bật 18 độ nhưng chỉ thổi gió nóng.  "
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdJson = (await createRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var requestId = createdJson.GetProperty("id").GetGuid();
        Assert.Equal("Máy lạnh không mát", createdJson.GetProperty("title").GetString());
        Assert.False(createdJson.GetProperty("landlordIsRead").GetBoolean());
        Assert.Equal(JsonValueKind.Null, createdJson.GetProperty("landlordReadAt").ValueKind);
        Assert.Equal("Open", createdJson.GetProperty("status").GetString());

        // 2. Landlord xem danh sách yêu cầu (IsRead vẫn là false trước khi bấm chi tiết)
        using var landlordClient = factory.CreateClient();
        await SignInAsync(landlordClient, landlord);

        var listRes = await landlordClient.GetAsync($"/api/landlord/requests?propertyId={propertyId}&isRead=false");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var listData = (await listRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Single(listData.EnumerateArray());
        Assert.False(listData[0].GetProperty("isRead").GetBoolean());

        // 3. Landlord mở chi tiết yêu cầu -> tự động bật IsRead = true, ReadAt != null
        var detailRes = await landlordClient.GetAsync($"/api/landlord/requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detailData = (await detailRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(detailData.GetProperty("isRead").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, detailData.GetProperty("readAt").ValueKind);
        Assert.Equal("Open", detailData.GetProperty("status").GetString());

        // 4. Tenant kiểm tra lại yêu cầu đã gửi -> thấy chủ trọ đã xem
        var myDetailRes = await tenantClient.GetAsync($"/api/me/requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, myDetailRes.StatusCode);
        var myDetailData = (await myDetailRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(myDetailData.GetProperty("landlordIsRead").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, myDetailData.GetProperty("landlordReadAt").ValueKind);

        // 5. Landlord đổi trạng thái xử lý sang InProgress rồi Resolved
        var patchRes = await landlordClient.PatchAsJsonAsync($"/api/landlord/requests/{requestId}/status", new
        {
            status = "InProgress"
        });
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);
        var patchData = (await patchRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("InProgress", patchData.GetProperty("status").GetString());

        var resolveRes = await landlordClient.PatchAsJsonAsync($"/api/landlord/requests/{requestId}/status", new
        {
            status = "Resolved"
        });
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);

        // 6. Tenant xem danh sách lọc theo status=Resolved
        var tenantListRes = await tenantClient.GetAsync("/api/me/requests?status=Resolved");
        Assert.Equal(HttpStatusCode.OK, tenantListRes.StatusCode);
        var tenantListData = (await tenantListRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Single(tenantListData.EnumerateArray());
        Assert.Equal("Resolved", tenantListData[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Landlord_can_send_announcements_single_multiple_all_and_tenant_marks_read()
    {
        await using var factory = new ApiFactory();
        var landlord = factory.Store.AddUser(UserRoles.Landlord);
        var tenant1 = factory.Store.AddUser(UserRoles.Tenant);
        var tenant2 = factory.Store.AddUser(UserRoles.Tenant);
        var tenant3 = factory.Store.AddUser(UserRoles.Tenant);

        var propertyId = Guid.NewGuid();
        var room1 = Guid.NewGuid();
        var room2 = Guid.NewGuid();
        var room3 = Guid.NewGuid();

        factory.RequestStore.Properties[propertyId] = new Property
        {
            Id = propertyId,
            OwnerUserId = landlord.Id,
            Name = "Khu trọ Bình Minh",
            Address = "456 Lê Lợi",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        factory.RequestStore.UsersInfo[landlord.Id] = ("Chủ trọ An", "0988888888");
        factory.RequestStore.EligibleRecipientsByProperty[propertyId] =
        [
            new AnnouncementRecipientOptionDto(tenant1.Id, Guid.NewGuid(), "Khách 1", "0901111111", room1, "101", Guid.NewGuid()),
            new AnnouncementRecipientOptionDto(tenant2.Id, Guid.NewGuid(), "Khách 2", "0902222222", room2, "102", Guid.NewGuid()),
            new AnnouncementRecipientOptionDto(tenant3.Id, Guid.NewGuid(), "Khách 3", "0903333333", room3, "103", Guid.NewGuid()),
        ];

        using var landlordClient = factory.CreateClient();
        await SignInAsync(landlordClient, landlord);

        // 1. Gửi SINGLE cho tenant1
        var singleRes = await landlordClient.PostAsJsonAsync($"/api/properties/{propertyId}/announcements", new
        {
            recipientType = "Single",
            receiverUserIds = new[] { tenant1.Id },
            title = "Nhắc nhở đóng tiền phòng 101",
            content = "Vui lòng kiểm tra hóa đơn tháng này."
        });
        Assert.Equal(HttpStatusCode.Created, singleRes.StatusCode);
        var singleData = (await singleRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1, singleData.GetProperty("totalRecipients").GetInt32());

        // 2. Gửi MULTIPLE cho tenant1 và tenant2 (kèm trùng lặp để kiểm tra Distinct)
        var multiRes = await landlordClient.PostAsJsonAsync($"/api/properties/{propertyId}/announcements", new
        {
            recipientType = "Multiple",
            receiverUserIds = new[] { tenant1.Id, tenant2.Id, tenant1.Id },
            title = "Thông báo dọn vệ sinh tầng 1",
            content = "Chiều mai khu trọ tổng vệ sinh hành lang."
        });
        Assert.Equal(HttpStatusCode.Created, multiRes.StatusCode);
        var multiData = (await multiRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(2, multiData.GetProperty("totalRecipients").GetInt32());

        // 3. Gửi ALL cho toàn bộ khu trọ (3 người nhận)
        var allRes = await landlordClient.PostAsJsonAsync($"/api/properties/{propertyId}/announcements", new
        {
            recipientType = "All",
            title = "Thông báo cúp nước định kỳ",
            content = "Từ 9h đến 11h sáng thứ Bảy."
        });
        Assert.Equal(HttpStatusCode.Created, allRes.StatusCode);
        var allData = (await allRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var allAnnouncementId = allData.GetProperty("id").GetGuid();
        Assert.Equal(3, allData.GetProperty("totalRecipients").GetInt32());
        Assert.Equal(0, allData.GetProperty("readCount").GetInt32());

        // 4. Tenant 3 chỉ nhận được đúng 1 thông báo (ALL) và bấm đọc
        using var tenant3Client = factory.CreateClient();
        await SignInAsync(tenant3Client, tenant3);

        var t3ListRes = await tenant3Client.GetAsync("/api/me/announcements");
        Assert.Equal(HttpStatusCode.OK, t3ListRes.StatusCode);
        var t3ListData = (await t3ListRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Single(t3ListData.EnumerateArray());
        Assert.False(t3ListData[0].GetProperty("isRead").GetBoolean());

        var readRes = await tenant3Client.PostAsync($"/api/me/announcements/{allAnnouncementId}/read", null);
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
        var readData = (await readRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(readData.GetProperty("isRead").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, readData.GetProperty("readAt").ValueKind);

        // 5. Landlord xem lịch sử thông báo thấy ReadCount của thông báo ALL tăng lên 1/3
        var historyRes = await landlordClient.GetAsync($"/api/properties/{propertyId}/announcements");
        Assert.Equal(HttpStatusCode.OK, historyRes.StatusCode);
        var historyData = (await historyRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var allItem = historyData.EnumerateArray().First(x => x.GetProperty("id").GetGuid() == allAnnouncementId);
        Assert.Equal(3, allItem.GetProperty("totalRecipients").GetInt32());
        Assert.Equal(1, allItem.GetProperty("readCount").GetInt32());
    }

    [SqlServerFact]
    public async Task SqlServer_RequestStore_persists_requests_and_recipients_with_unique_constraint()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var landlordId = "req-landlord-1";
        var tenantUserId = "req-tenant-1";
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        context.Users.AddRange(
            new AppUser
            {
                Id = landlordId,
                UserName = "landlord1",
                NormalizedUserName = "LANDLORD1",
                PasswordHash = "hash",
                Role = UserRoles.Landlord,
                IsActive = true,
                FullName = "Chủ Trọ A",
                CreatedAt = DateTime.UtcNow
            },
            new AppUser
            {
                Id = tenantUserId,
                UserName = "tenant1",
                NormalizedUserName = "TENANT1",
                PasswordHash = "hash",
                Role = UserRoles.Tenant,
                IsActive = true,
                FullName = "Người Thuê B",
                PhoneNumber = "0909123456",
                CreatedAt = DateTime.UtcNow
            });

        context.Properties.Add(new Property
        {
            Id = propertyId,
            OwnerUserId = landlordId,
            Name = "Khu Trọ Thực Nghiệm",
            Address = "100 Đường Số 1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        context.Rooms.Add(new Room
        {
            Id = roomId,
            PropertyId = propertyId,
            RoomNumber = "A101",
            MaxOccupants = 2,
            BasePrice = 3_500_000,
            Status = RoomStatus.Occupied,
            CreatedAt = DateTime.UtcNow
        });

        context.Contracts.Add(new Contract
        {
            Id = contractId,
            RoomId = roomId,
            ContractCode = "HD-REQ-001",
            StartDate = today.AddDays(-5),
            MonthlyRent = 3_500_000,
            Status = ContractStatus.Active,
            CreatedAt = DateTime.UtcNow
        });

        context.TenantProfiles.Add(new TenantProfile
        {
            Id = profileId,
            UserId = tenantUserId,
            CreatedByUserId = landlordId,
            FullName = "Người Thuê B",
            PhoneNumber = "0909123456",
            CreatedAt = DateTime.UtcNow
        });

        context.ContractTenants.Add(new ContractTenant
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            TenantId = profileId,
            JoinDate = today.AddDays(-5),
            IsContactPerson = true,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var store = new RequestStore(context);
        var stays = await store.GetTenantActiveStaysAsync(tenantUserId);
        Assert.Single(stays);
        Assert.Equal("A101", stays[0].RoomNumber);

        var req = new Request
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            SenderUserId = tenantUserId,
            RecipientType = RequestRecipientType.Owner,
            Title = "Sửa khóa cửa",
            Content = "Ổ khóa cửa chính bị kẹt.",
            Type = RequestType.Request,
            CreatedAt = DateTime.UtcNow
        };
        var rec = new RequestRecipient
        {
            Id = Guid.NewGuid(),
            RequestId = req.Id,
            ReceiverUserId = landlordId,
            IsRead = false,
            Status = RequestRecipientStatus.Open
        };

        await store.AddRequestWithRecipientsAsync(req, [rec]);

        var readDetail = await store.GetLandlordRequestAndMarkReadAsync(req.Id, landlordId);
        Assert.NotNull(readDetail);
        Assert.True(readDetail!.IsRead);
        Assert.NotNull(readDetail.ReadAt);
        Assert.Equal("A101", readDetail.RoomName);

        var updated = await store.UpdateLandlordRequestStatusAsync(req.Id, landlordId, RequestRecipientStatus.Resolved);
        Assert.Equal(RequestRecipientStatus.Resolved, updated!.Status);
    }

    [Fact]
    public async Task Multi_property_tenant_can_list_request_properties_and_select_target_property()
    {
        await using var factory = new ApiFactory();
        var landlord1 = factory.Store.AddUser(UserRoles.Landlord);
        var landlord2 = factory.Store.AddUser(UserRoles.Landlord);
        var tenant = factory.Store.AddUser(UserRoles.Tenant);

        var prop1 = Guid.NewGuid();
        var prop2 = Guid.NewGuid();
        factory.RequestStore.Properties[prop1] = new Property
        {
            Id = prop1,
            OwnerUserId = landlord1.Id,
            Name = "Khu trọ 1",
            Address = "1 Đường A",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        factory.RequestStore.Properties[prop2] = new Property
        {
            Id = prop2,
            OwnerUserId = landlord2.Id,
            Name = "Khu trọ 2",
            Address = "2 Đường B",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        factory.RequestStore.ActiveStaysByTenant[tenant.Id] =
        [
            new TenantActiveStayInfo(prop1, "Khu trọ 1", landlord1.Id, Guid.NewGuid(), "101", Guid.NewGuid()),
            new TenantActiveStayInfo(prop2, "Khu trọ 2", landlord2.Id, Guid.NewGuid(), "202", Guid.NewGuid()),
        ];

        using var client = factory.CreateClient();
        await SignInAsync(client, tenant);

        var propsRes = await client.GetAsync("/api/me/requests/properties");
        Assert.Equal(HttpStatusCode.OK, propsRes.StatusCode);
        var propsData = (await propsRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(2, propsData.GetArrayLength());

        var noPropRes = await client.PostAsJsonAsync("/api/me/requests", new
        {
            title = "Cần hỗ trợ",
            content = "Thiếu propertyId"
        });
        Assert.Equal(HttpStatusCode.BadRequest, noPropRes.StatusCode);

        var chosenRes = await client.PostAsJsonAsync("/api/me/requests", new
        {
            propertyId = prop2,
            title = "Sửa khóa phòng 202",
            content = "Khóa cửa hơi kẹt."
        });
        Assert.Equal(HttpStatusCode.Created, chosenRes.StatusCode);
        var createdData = (await chosenRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(prop2, createdData.GetProperty("propertyId").GetGuid());
        Assert.Equal("Khu trọ 2", createdData.GetProperty("propertyName").GetString());
    }

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

