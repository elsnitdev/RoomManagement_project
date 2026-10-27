using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class RoomService
{
    private readonly ICurrentLandlord _currentLandlord;
    private readonly ILandlordStore _landlordStore;
    private readonly IPropertyStore _properties;
    private readonly IRoomStore _rooms;

    public RoomService(ICurrentLandlord currentLandlord, ILandlordStore landlordStore, IPropertyStore properties, IRoomStore rooms)
    {
        _currentLandlord = currentLandlord;
        _landlordStore = landlordStore;
        _properties = properties;
        _rooms = rooms;
    }

    private async Task<string> OwnerAsync(CancellationToken ct)
    {
        var ownerId = _currentLandlord.GetUserId();
        if (string.IsNullOrWhiteSpace(ownerId) || !await _landlordStore.IsActiveLandlordAsync(ownerId, ct))
            throw new BusinessException(BusinessErrorType.InvalidOperation, "LANDLORD_UNAVAILABLE", "Chủ trọ hiện tại không tồn tại hoặc đã ngừng hoạt động.");
        return ownerId;
    }
    private async Task<Property> PropertyAsync(Guid propertyId, string ownerId, CancellationToken ct) =>
        await _properties.GetAsync(propertyId, ownerId, ct)
        ?? throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Không tìm thấy khu trọ.");

    public async Task<RoomResult> CreateAsync(Guid propertyId, CreateRoomInput input, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        var property = await PropertyAsync(propertyId, ownerId, ct);
        if (!property.IsActive)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "PROPERTY_INACTIVE", "Không thể thêm phòng vào khu trọ ngừng hoạt động.");
        PropertyRoomValidation.Room(input.RoomNumber, input.RoomName, input.Area, input.BasePrice, input.MaxOccupants, input.Description);
        var number = input.RoomNumber.Trim();
        if (await _rooms.NumberExistsAsync(propertyId, number, null, ct))
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_NUMBER_ALREADY_EXISTS", "Mã phòng đã tồn tại trong khu trọ.");
        var room = new Room
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, RoomNumber = number,
            RoomName = input.RoomName?.Trim(), Floor = input.Floor, Area = input.Area,
            BasePrice = input.BasePrice, MaxOccupants = input.MaxOccupants,
            Description = input.Description?.Trim(), Status = RoomStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        await _rooms.AddAsync(room, ct);
        return new RoomResult(room, RoomStatus.Available);
    }

    public async Task<PageResult<RoomResult>> ListAsync(Guid propertyId, RoomFilter filter, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        await PropertyAsync(propertyId, ownerId, ct);
        PropertyRoomValidation.Page(filter.Page, filter.PageSize);
        PropertyRoomValidation.Search(filter.Search);
        if (filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value))
            throw new BusinessException(BusinessErrorType.Validation, "VALIDATION_ERROR", "Trạng thái phòng không hợp lệ.");
        var (items, total) = await _rooms.ListAsync(propertyId, ownerId, filter.Search?.Trim(), filter.Status, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
        return new PageResult<RoomResult>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<RoomResult> GetAsync(Guid propertyId, Guid roomId, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        return await _rooms.GetResultAsync(propertyId, roomId, ownerId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "ROOM_NOT_FOUND", "Không tìm thấy phòng.");
    }

    public async Task<RoomResult> UpdateAsync(Guid propertyId, Guid roomId, UpdateRoomInput input, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        var room = await _rooms.GetAsync(propertyId, roomId, ownerId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "ROOM_NOT_FOUND", "Không tìm thấy phòng.");
        RowVersionGuard.Verify(input.RowVersion, room.RowVersion);
        PropertyRoomValidation.Room(input.RoomNumber, input.RoomName, input.Area, input.BasePrice, input.MaxOccupants, input.Description);
        if (input.Status is not (RoomStatus.Available or RoomStatus.Maintenance or RoomStatus.Inactive))
            throw new BusinessException(BusinessErrorType.Validation, "ROOM_STATUS_INVALID", "Chỉ được chọn Available, Maintenance hoặc Inactive.");
        var number = input.RoomNumber.Trim();
        if (await _rooms.NumberExistsAsync(propertyId, number, roomId, ct))
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_NUMBER_ALREADY_EXISTS", "Mã phòng đã tồn tại trong khu trọ.");
        var hasActiveContract = await _rooms.HasActiveContractAsync(roomId, ct);
        if (hasActiveContract && input.Status != RoomStatus.Available)
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_HAS_ACTIVE_CONTRACT", "Phòng có hợp đồng đang hiệu lực; không thể chuyển trạng thái vận hành.");
        var property = await PropertyAsync(propertyId, ownerId, ct);
        if (!property.IsActive && input.Status == RoomStatus.Available && room.Status != RoomStatus.Available)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "PROPERTY_INACTIVE", "Không thể mở lại phòng khi khu trọ ngừng hoạt động.");
        room.RoomNumber = number;
        room.RoomName = input.RoomName?.Trim();
        room.Floor = input.Floor;
        room.Area = input.Area;
        room.BasePrice = input.BasePrice;
        room.MaxOccupants = input.MaxOccupants;
        room.Description = input.Description?.Trim();
        room.Status = input.Status;
        room.UpdatedAt = DateTime.UtcNow;
        await _rooms.SaveAsync(ct);
        return new RoomResult(room, hasActiveContract ? RoomStatus.Occupied : room.Status);
    }

    public async Task DeleteAsync(Guid propertyId, Guid roomId, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        var room = await _rooms.GetAsync(propertyId, roomId, ownerId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "ROOM_NOT_FOUND", "Không tìm thấy phòng.");
        var blocker = await _rooms.GetDeleteBlockerAsync(roomId, ct);
        if (blocker is not null)
        {
            var message = blocker switch
            {
                "ROOM_HAS_CONTRACTS" => "Phòng còn hợp đồng.",
                "ROOM_HAS_SERVICES" => "Phòng còn dịch vụ.",
                "ROOM_HAS_METER_READINGS" => "Phòng còn chỉ số điện nước.",
                _ => "Phòng còn dữ liệu liên quan."
            };
            throw new BusinessException(BusinessErrorType.Conflict, blocker, message);
        }
        await _rooms.DeleteAsync(room, ct);
    }
}
