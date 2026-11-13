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
