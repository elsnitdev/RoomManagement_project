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

    public async Task<Property> CreateAsync(CreatePropertyInput input, CancellationToken ct)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_currentLandlord, _landlordStore, ct);
        var ownerId = context.OwnerLandlordUserId;
        PropertyRoomValidation.Required(input.Name, 150, "Tên khu trọ");
        PropertyRoomValidation.Optional(input.Description, 1000, "Mô tả");
        var (city, ward, streetAddress, fullAddress) = NormalizeAddress(input.City, input.Ward, input.StreetAddress);
        var property = new Property
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            Name = input.Name.Trim(),
            City = city,
            Ward = ward,
            StreetAddress = streetAddress,
            Address = fullAddress,
            Description = input.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _propertyStore.AddAsync(property, ct);
        return property;
    }

    public async Task<PageResult<Property>> ListAsync(PropertyFilter filter, CancellationToken ct)
    {
        var context = await OperatorAccessGuard.RequireOperatorAsync(_currentLandlord, _landlordStore, ct);
        PropertyRoomValidation.Page(filter.Page, filter.PageSize);
        PropertyRoomValidation.Search(filter.Search);
        var (items, total) = await _propertyStore.ListForOperatorAsync(
            context.ActorUserId, context.OwnerLandlordUserId, context.IsManager,
            filter.Search?.Trim(), filter.IsActive, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
        return new PageResult<Property>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<Property> GetAsync(Guid propertyId, CancellationToken ct)
    {
        var context = await OperatorAccessGuard.RequireOperatorAsync(_currentLandlord, _landlordStore, ct);
        await OperatorAccessGuard.EnsurePropertyAccessAsync(context, propertyId, _landlordStore, ct);
        return await _propertyStore.GetAsync(propertyId, context.OwnerLandlordUserId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");
    }

    public async Task<Property> UpdateAsync(Guid propertyId, UpdatePropertyInput input, CancellationToken ct)
    {
        var property = await GetAsync(propertyId, ct);
        RowVersionGuard.Verify(input.RowVersion, property.RowVersion);
        PropertyRoomValidation.Required(input.Name, 150, "Tên khu trọ");
        PropertyRoomValidation.Optional(input.Description, 1000, "Mô tả");
        var (city, ward, streetAddress, fullAddress) = NormalizeAddress(input.City, input.Ward, input.StreetAddress);
        property.Name = input.Name.Trim();
        property.City = city;
        property.Ward = ward;
        property.StreetAddress = streetAddress;
        property.Address = fullAddress;
        property.Description = input.Description?.Trim();
        property.IsActive = input.IsActive;
        property.UpdatedAt = DateTime.UtcNow;
        await _propertyStore.SaveAsync(ct);
        return property;
    }

    private static (string City, string Ward, string StreetAddress, string FullAddress) NormalizeAddress(
        string city, string ward, string streetAddress)
    {
        var c = city?.Trim() ?? string.Empty;
        var w = ward?.Trim() ?? string.Empty;
        var s = streetAddress?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(c) && string.IsNullOrWhiteSpace(w))
        {
            PropertyRoomValidation.Required(s, 300, "Địa chỉ");
            return (string.Empty, string.Empty, s, s);
        }

        PropertyRoomValidation.Required(c, 100, "Tỉnh/Thành phố");
        PropertyRoomValidation.Required(w, 100, "Phường/Xã");
        PropertyRoomValidation.Required(s, 200, "Địa chỉ cụ thể");
        var full = $"{s}, {w}, {c}";
        return (c, w, s, full);
    }

    public async Task<Property> UpdatePaymentAccountAsync(Guid propertyId, string? bankName,
        string? accountNumber, string? accountHolder, string rowVersion, CancellationToken ct)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_currentLandlord, _landlordStore, ct);
        var property = await _propertyStore.GetAsync(propertyId, context.OwnerLandlordUserId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");
        RowVersionGuard.Verify(rowVersion, property.RowVersion);
        var fields = new[] { bankName, accountNumber, accountHolder };
        if (fields.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            PropertyRoomValidation.Required(bankName, 150, "Ngân hàng");
            PropertyRoomValidation.Required(accountNumber, 50, "Số tài khoản");
            PropertyRoomValidation.Required(accountHolder, 150, "Tên chủ tài khoản");
        }
        property.BankName = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
        property.BankAccountNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
        property.BankAccountHolder = string.IsNullOrWhiteSpace(accountHolder) ? null : accountHolder.Trim();
        property.UpdatedAt = DateTime.UtcNow;
        await _propertyStore.SaveAsync(ct);
        return property;
    }

    public async Task DeleteAsync(Guid propertyId, CancellationToken ct)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_currentLandlord, _landlordStore, ct);
        var property = await _propertyStore.GetAsync(propertyId, context.OwnerLandlordUserId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");
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
