using Microsoft.Extensions.Logging.Abstractions;
using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

/// <summary>
/// Unit Tests kiểm tra logic nghiệp vụ của TenantProfileService theo kịch bản trong kế hoạch.
/// </summary>
public sealed class TenantProfileServiceTests
{
    private readonly TestTenantProfileStore _store;
    private readonly TenantProfileService _service;

    public TenantProfileServiceTests()
    {
        _store = new TestTenantProfileStore();
        _service = new TenantProfileService(_store, NullLogger<TenantProfileService>.Instance);
    }

    [Fact]
    public async Task CreateProfile_Succeeds_With_UserIdNull_And_CreatedByUserId_Set()
    {
        var landlordId = Guid.NewGuid().ToString();
        var dto = new CreateTenantProfileDto
        {
            FullName = "Nguyễn Văn A",
            PhoneNumber = "0901234567",
            Email = "vana@example.com",
            CitizenId = "079201012345",
            DateOfBirth = new DateOnly(1995, 5, 20),
            Gender = "Male",
            PermanentAddress = "Hà Nội"
        };

        var result = await _service.CreateProfileAsync(landlordId, dto);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Null(result.UserId); // Chưa có tài khoản người dùng
        Assert.Equal(landlordId, result.CreatedByUserId);
        Assert.Equal("Nguyễn Văn A", result.FullName);
        Assert.Equal("0901234567", result.PhoneNumber);
        Assert.Equal("079201012345", result.CitizenId);
        Assert.NotEqual(default, result.CreatedAt);
        Assert.Null(result.UpdatedAt);

        // Đảm bảo dữ liệu đã được lưu vào store
        Assert.True(_store.Profiles.ContainsKey(result.Id));
    }

    [Fact]
    public async Task CreateProfile_ThrowsConflictException_When_CitizenIdAlreadyExists()
    {
        var landlordId = Guid.NewGuid().ToString();
        var existingCitizenId = "079201012345";

        // Thêm một hồ sơ đã có sẵn CCCD
        var existingProfile = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordId,
            FullName = "Người Cũ",
            PhoneNumber = "0911111111",
            CitizenId = existingCitizenId,
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(existingProfile);

        // Tạo hồ sơ mới nhưng trùng CCCD
        var dto = new CreateTenantProfileDto
        {
            FullName = "Người Mới Trùng CCCD",
            PhoneNumber = "0922222222",
            CitizenId = existingCitizenId
        };

        var ex = await Assert.ThrowsAsync<AppConflictException>(
            () => _service.CreateProfileAsync(landlordId, dto));

        Assert.Equal("CITIZEN_ID_CONFLICT", ex.Code);
        Assert.Contains("Số CCCD/CMND đã được sử dụng", ex.Message);
    }

    [Fact]
    public async Task GetProfileById_ThrowsNotFoundException_When_LandlordHasNoAccess()
    {
        var landlordOwner = Guid.NewGuid().ToString();
        var landlordStranger = Guid.NewGuid().ToString();

        var profile = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordOwner,
            FullName = "Khách Thuê Của Chủ A",
            PhoneNumber = "0901234567",
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(profile);

        // Chủ trọ B cố gắng truy cập hồ sơ của Chủ trọ A
        var ex = await Assert.ThrowsAsync<AppNotFoundException>(
            () => _service.GetProfileByIdAsync(landlordStranger, profile.Id));

        Assert.Contains("không có quyền truy cập", ex.Message);
    }

    [Fact]
    public async Task GetProfileById_Succeeds_When_LandlordIsCreator()
    {
        var landlordId = Guid.NewGuid().ToString();
        var profile = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordId,
            FullName = "Khách Thuê",
            PhoneNumber = "0901234567",
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(profile);

        var result = await _service.GetProfileByIdAsync(landlordId, profile.Id);

        Assert.NotNull(result);
        Assert.Equal(profile.Id, result.Id);
        Assert.Equal("Khách Thuê", result.FullName);
    }

    [Fact]
    public async Task GetProfileById_Succeeds_When_TenantHasContractInLandlordProperty()
    {
        var creatorLandlord = Guid.NewGuid().ToString();
        var currentPropertyLandlord = Guid.NewGuid().ToString();

        var profile = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = creatorLandlord,
            FullName = "Khách Thuê Đã Ký Hợp Đồng Mới",
            PhoneNumber = "0901234567",
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(profile);

        // Giả lập người thuê này đã có hợp đồng thuê tại khu trọ của currentPropertyLandlord
        _store.ContractAccessList.Add((currentPropertyLandlord, profile.Id));

        var result = await _service.GetProfileByIdAsync(currentPropertyLandlord, profile.Id);

        Assert.NotNull(result);
        Assert.Equal(profile.Id, result.Id);
    }

    [Fact]
    public async Task UpdateProfile_Succeeds_When_Authorized()
    {
        var landlordId = Guid.NewGuid().ToString();
        var profile = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordId,
            FullName = "Họ Tên Cũ",
            PhoneNumber = "0901111111",
            Email = "old@example.com",
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(profile);

        var updateDto = new UpdateTenantProfileDto
        {
            FullName = "Họ Tên Mới Đã Sửa",
            PhoneNumber = "0909999999",
            Email = "new@example.com",
            CitizenId = "079201999999",
            Gender = "Female",
            PermanentAddress = "TP Đà Nẵng"
        };

        var result = await _service.UpdateProfileAsync(landlordId, profile.Id, updateDto);

        Assert.NotNull(result);
        Assert.Equal("Họ Tên Mới Đã Sửa", result.FullName);
        Assert.Equal("0909999999", result.PhoneNumber);
        Assert.Equal("new@example.com", result.Email);
        Assert.Equal("079201999999", result.CitizenId);
        Assert.NotNull(result.UpdatedAt);
    }

    [Fact]
    public async Task UpdateProfile_ThrowsConflict_When_CitizenIdCollidesWithOtherProfile()
    {
        var landlordId = Guid.NewGuid().ToString();

        var profile1 = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordId,
            FullName = "Người 1",
            PhoneNumber = "0901111111",
            CitizenId = "079201000001",
            CreatedAt = DateTime.UtcNow
        };
        var profile2 = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordId,
            FullName = "Người 2",
            PhoneNumber = "0902222222",
            CitizenId = "079201000002",
            CreatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(profile1);
        await _store.AddAsync(profile2);

        // Cố gắng cập nhật Profile 2 lấy số CCCD của Profile 1
        var updateDto = new UpdateTenantProfileDto
        {
            FullName = "Người 2 Sửa",
            PhoneNumber = "0902222222",
            CitizenId = "079201000001"
        };

        var ex = await Assert.ThrowsAsync<AppConflictException>(
            () => _service.UpdateProfileAsync(landlordId, profile2.Id, updateDto));

        Assert.Equal("CITIZEN_ID_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task GetProfiles_ReturnsPaginatedAndFilteredList()
    {
        var landlordA = Guid.NewGuid().ToString();
        var landlordB = Guid.NewGuid().ToString();

        await _store.AddAsync(new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordA,
            FullName = "Nguyễn Văn An",
            PhoneNumber = "0901111111",
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        });
        await _store.AddAsync(new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordA,
            FullName = "Trần Thị Bích",
            PhoneNumber = "0902222222",
            CreatedAt = DateTime.UtcNow.AddMinutes(2)
        });
        await _store.AddAsync(new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlordB,
            FullName = "Người Của Chủ B",
            PhoneNumber = "0903333333",
            CreatedAt = DateTime.UtcNow.AddMinutes(3)
        });

        // 1. Chủ trọ A lấy toàn bộ danh sách của mình
        var (allItems, totalAll) = await _service.GetProfilesAsync(
            landlordA, new TenantProfileQueryDto { PageIndex = 1, PageSize = 10 });
        Assert.Equal(2, totalAll);
        Assert.Equal(2, allItems.Count);

        // 2. Chủ trọ A tìm kiếm theo từ khóa "An"
        var (filteredItems, totalFiltered) = await _service.GetProfilesAsync(
            landlordA, new TenantProfileQueryDto { Keyword = "An", PageIndex = 1, PageSize = 10 });
        Assert.Equal(1, totalFiltered);
        Assert.Equal("Nguyễn Văn An", filteredItems[0].FullName);
    }
}
