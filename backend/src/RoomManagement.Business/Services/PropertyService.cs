using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class PropertyService
{
    private readonly ICurrentLandlord _currentLandlord;
    private readonly ILandlordStore _landlordStore;
    private readonly IPropertyStore _propertyStore;

    public PropertyService(ICurrentLandlord currentLandlord, ILandlordStore landlordStore, IPropertyStore propertyStore)
    {
        _currentLandlord = currentLandlord;
        _landlordStore = landlordStore;
        _propertyStore = propertyStore;
    }

    private async Task<string> OwnerAsync(CancellationToken ct)
    {
        var ownerId = _currentLandlord.GetUserId();
        if (string.IsNullOrWhiteSpace(ownerId) || !await _landlordStore.IsActiveLandlordAsync(ownerId, ct))
            throw new BusinessException(BusinessErrorType.InvalidOperation, "LANDLORD_UNAVAILABLE", "Chủ trọ hiện tại không tồn tại hoặc đã ngừng hoạt động.");
        return ownerId;
    }

    public async Task<Property> CreateAsync(CreatePropertyInput input, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        PropertyRoomValidation.Required(input.Name, 150, "Tên khu trọ");
        PropertyRoomValidation.Required(input.Address, 300, "Địa chỉ");
        PropertyRoomValidation.Optional(input.Description, 1000, "Mô tả");
        var property = new Property
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            Name = input.Name.Trim(),
            Address = input.Address.Trim(),
            Description = input.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _propertyStore.AddAsync(property, ct);
        return property;
    }

    public async Task<PageResult<Property>> ListAsync(PropertyFilter filter, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        PropertyRoomValidation.Page(filter.Page, filter.PageSize);
        PropertyRoomValidation.Search(filter.Search);
        var (items, total) = await _propertyStore.ListAsync(ownerId, filter.Search?.Trim(), filter.IsActive, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
        return new PageResult<Property>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<Property> GetAsync(Guid propertyId, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        return await _propertyStore.GetAsync(propertyId, ownerId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");
    }

    public async Task<Property> UpdateAsync(Guid propertyId, UpdatePropertyInput input, CancellationToken ct)
    {
        var property = await GetAsync(propertyId, ct);
        RowVersionGuard.Verify(input.RowVersion, property.RowVersion);
        PropertyRoomValidation.Required(input.Name, 150, "Tên khu trọ");
        PropertyRoomValidation.Required(input.Address, 300, "Địa chỉ");
        PropertyRoomValidation.Optional(input.Description, 1000, "Mô tả");
        property.Name = input.Name.Trim();
        property.Address = input.Address.Trim();
        property.Description = input.Description?.Trim();
        property.IsActive = input.IsActive;
        property.UpdatedAt = DateTime.UtcNow;
        await _propertyStore.SaveAsync(ct);
        return property;
    }

    public async Task DeleteAsync(Guid propertyId, CancellationToken ct)
    {
        var property = await GetAsync(propertyId, ct);
        var blocker = await _propertyStore.GetDeleteBlockerAsync(propertyId, ct);
        if (blocker is not null)
        {
            var message = blocker switch
            {
                "PROPERTY_HAS_ROOMS" => "Khu trọ còn phòng.",
                "PROPERTY_HAS_UTILITY_RATES" => "Khu trọ còn bảng giá điện nước.",
                _ => "Khu trọ còn dữ liệu liên quan."
            };
            throw new BusinessException(BusinessErrorType.Conflict, blocker, message);
        }
        await _propertyStore.DeleteAsync(property, ct);
    }
}
