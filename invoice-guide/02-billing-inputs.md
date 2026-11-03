# Chương 2 — Viết file nhập giá, chỉ số và dịch vụ

> Đã triển khai. Xem [danh sách file và API thực tế](implementation-status-02-03.md). Một số phần được gom trong `BillingInputService` và `BillingInputStore` thay vì tách thành nhiều class như bài hướng dẫn ban đầu.

**Đích:** backend trả được dữ liệu đầu vào cho một phòng/kỳ: giá điện nước, chỉ số tiêu thụ, dịch vụ áp dụng. Chương 3 sẽ chỉ tính tiền từ dữ liệu đã được kiểm tra ở đây. Các entity/configuration đã tồn tại trong `Domain/Entities` và `Data/Configurations`; **chưa có service, store hoặc API** cho ba nhóm này.

Trong chương này, `Business/` là `backend/src/RoomManagement.Business/`, `Data/` là `backend/src/RoomManagement.Data/`, `API/` là `backend/src/RoomManagement.API/`. Mỗi bảng dưới đây chỉ rõ nội dung cần ghi, không chỉ là tên file.

## 1. Giá điện nước (`UtilityRate`)

| File cần tạo/sửa | Ghi gì trong file |
| --- | --- |
| `Business/DTOs/UtilityRateModels.cs` | `CreateUtilityRateInput(PropertyId, UtilityType, UnitPrice, Unit, EffectiveFrom, EffectiveTo)` và `UtilityRateResult`. Không đặt attribute HTTP trong Business. |
| `Business/Interfaces/IUtilityRateStore.cs` | `ListAsync(propertyId, ownerId, type, ct)`, `HasOverlapAsync(propertyId, type, from, to, ct)`, `AddAsync(rate, ct)`, `FindEffectiveAsync(propertyId, type, date, ct)`. |
| `Business/Services/UtilityRateService.cs` | Lấy landlord hiện tại; kiểm khu thuộc chủ trọ; `UnitPrice >= 0`, `Unit` không rỗng, `EffectiveTo >= EffectiveFrom`; chặn khoảng giá giao nhau; tạo entity và gọi store. |
| `Data/UtilityRateStore.cs` | EF query qua `Properties.OwnerUserId`; `FindEffectiveAsync` lấy một rate có `EffectiveFrom <= date && (EffectiveTo == null || date <= EffectiveTo)`; sort theo `EffectiveFrom` giảm rồi `Single`/báo xung đột nếu có nhiều rate. |
| `API/Contracts/Utilities/UtilityRateRequests.cs` | Request JSON có `UtilityType`, giá, đơn vị và ngày hiệu lực; `ToInput(propertyId)`. |
| `API/Controllers/UtilityRatesController.cs` | `POST/GET /api/properties/{propertyId}/utility-rates`; `[Authorize(Roles = Landlord)]`, gọi service và trả `ApiResponse`. |

**Điều kiện chồng khoảng phải viết rõ** trong `HasOverlapAsync`. Hai khoảng ngày `[aFrom,aTo]` và `[bFrom,bTo]` chồng nhau nếu `aFrom <= bTo && bFrom <= aTo`; `null` ở ngày kết thúc nghĩa là vô hạn. EF có thể biểu diễn:

```csharp
return await _db.UtilityRates.AnyAsync(r =>
    r.PropertyId == propertyId && r.UtilityType == type &&
    (to == null || r.EffectiveFrom <= to.Value) &&
    (r.EffectiveTo == null || from <= r.EffectiveTo.Value), ct);
```

`UtilityRateConfiguration.cs` chỉ chặn một số trường hợp trùng; service/store phải chặn chồng khoảng thật. Khi tạo hóa đơn, dùng **giá hiệu lực đầu kỳ** theo đề xuất của plan, rồi lưu `UnitPrice` vào `InvoiceItems`. Nếu chủ dự án chọn giá đổi ngay giữa kỳ, phải thiết kế lại phép tính trước khi dùng route này để phát hành hóa đơn.

## 2. Chỉ số điện nước (`MeterReading`)

| File cần tạo/sửa | Ghi gì trong file |
| --- | --- |
| `Business/DTOs/MeterReadingModels.cs` | `CreateMeterReadingInput(RoomId, MeterType, Year, Month, PreviousValue, CurrentValue, Note)`; result chứa `Used = CurrentValue - PreviousValue`. |
| `Business/Interfaces/IMeterReadingStore.cs` | `GetForPeriodAsync(roomId, type, year, month, ct)`, `GetPreviousAsync(roomId, type, year, month, ct)`, `AddAsync(reading, ct)` và phương thức kiểm phòng thuộc landlord. |
| `Business/Services/MeterReadingService.cs` | Kiểm năm/tháng, phòng thuộc chủ trọ, `CurrentValue >= PreviousValue`, kỳ chưa có bản ghi, `PreviousValue` bằng chỉ số cuối kỳ trước nếu kỳ trước tồn tại; trả lỗi khi chuỗi thiếu theo chính sách đã chốt. |
| `Data/MeterReadingStore.cs` | Query `MeterReadings` theo `RoomId`, `MeterType`, `Year`, `Month`; kỳ trước dùng `new DateOnly(year,month,1).AddMonths(-1)` để xử lý chuyển năm. Bắt unique SQL khi hai request cùng ghi một kỳ. |
| `API/Contracts/Utilities/MeterReadingRequests.cs` | Request JSON chứa loại đồng hồ, kỳ và hai chỉ số; `ToInput(roomId)`. |
| `API/Controllers/MeterReadingsController.cs` | `POST/GET /api/properties/{propertyId}/rooms/{roomId}/meter-readings`; gọi service, không tính tiền tại controller. |

Ví dụ phần kiểm tra chính của service:

```csharp
if (input.Year is < 2000 or > 2100 || input.Month is < 1 or > 12 ||
    input.PreviousValue < 0 || input.CurrentValue < input.PreviousValue)
    throw new BusinessException(BusinessErrorType.Validation,
        "METER_READING_INVALID", "Chỉ số hoặc kỳ không hợp lệ.");

var previousPeriod = new DateOnly(input.Year, input.Month, 1).AddMonths(-1);
var previous = await _store.GetForPeriodAsync(input.RoomId, input.MeterType,
    previousPeriod.Year, previousPeriod.Month, ct);
if (previous is not null && input.PreviousValue != previous.CurrentValue)
    throw new BusinessException(BusinessErrorType.Conflict,
        "METER_READING_GAP", "Chỉ số đầu kỳ không khớp cuối kỳ trước.");
```

Ở kỳ đầu tiên chưa có bản ghi trước, cần cho nhập số đầu kỳ có kiểm tra; từ kỳ thứ hai không cho đứt chuỗi. **Không tạo dòng hóa đơn có `Quantity = 0`**, vì `InvoiceItemConfiguration.cs` yêu cầu `Quantity > 0`.

## 3. Danh mục dịch vụ và dịch vụ của phòng

`Service` là danh mục dùng chung: tên, đơn vị, `DefaultPrice`, `IsActive`. `RoomService` gắn một dịch vụ với một phòng, có `CustomPrice` và khoảng áp dụng. Đừng tạo riêng `RoomService` mà không có `Service` tương ứng.

| File cần tạo/sửa | Ghi gì trong file |
| --- | --- |
| `Business/DTOs/ServiceModels.cs` | `CreateServiceInput(Name, Description, Unit, DefaultPrice)` và `AssignRoomServiceInput(ServiceId, CustomPrice, StartDate, EndDate)`. |
| `Business/Interfaces/IServiceStore.cs` | `GetActiveAsync(serviceId, ct)`, `AddAsync(service, ct)`, `ListAsync(ct)`. Danh mục hiện chưa có `PropertyId`, nên quyền tạo/sửa dịch vụ toàn hệ thống phải được quyết định; mặc định chỉ Admin quản lý. |
| `Business/Interfaces/IRoomServiceStore.cs` | `ListEffectiveAsync(roomId, periodStart, periodEnd, ct)`, `HasOverlapAsync(roomId, serviceId, from, to, ct)`, `AddAsync(roomService, ct)`. |
| `Business/Services/ServiceCatalogService.cs` | Kiểm tên/giá mặc định; tạo danh mục dưới quyền Admin theo chính sách được chọn. |
| `Business/Services/RoomServiceService.cs` | Kiểm landlord sở hữu phòng, dịch vụ còn hoạt động, ngày/giá riêng hợp lệ và khoảng gán không chồng nhau. |
| `Data/ServiceStore.cs`, `Data/RoomServiceStore.cs` | EF CRUD theo interface; `RoomServiceStore` nối `Room → Property.OwnerUserId` khi gán. |
| `API/Contracts/Services/ServiceRequests.cs`, `RoomServiceRequests.cs` | Request/response tách cho danh mục và gán phòng; không trả entity trực tiếp. |
| `API/Controllers/ServicesController.cs`, `RoomServicesController.cs` | Route danh mục dịch vụ và route gán phòng; attribute role đúng với chính sách trên. |

Khi Chương 3 chọn giá dịch vụ, dùng:

```csharp
decimal price = roomService.CustomPrice
    ?? service.DefaultPrice
    ?? throw new BusinessException(BusinessErrorType.Validation,
        "SERVICE_PRICE_MISSING", "Dịch vụ chưa có đơn giá.");
```

`RoomServices` có ngày bắt đầu/kết thúc, nhưng kế hoạch chưa chốt dịch vụ áp dụng một phần tháng có tính theo ngày hay tính trọn kỳ. **Ghi test cho quy tắc được chọn trước khi phát hành hóa đơn có dịch vụ đó.**

## 4. DI và checkpoint

Trong `backend/src/RoomManagement.Data/ServiceCollectionExtensions.cs`, thêm mỗi cặp `services.AddScoped<IUtilityRateStore, UtilityRateStore>()`, `IMeterReadingStore`, `IServiceStore`, `IRoomServiceStore`. Trong `backend/src/RoomManagement.API/Program.cs`, đăng ký các service cụ thể như `PropertyService`. Kiểm tra namespace thực tế của mỗi store trước khi thêm `using`.

Build solution; qua API tạo một giá điện và nước, hai kỳ chỉ số nối tiếp, một dịch vụ được gán cho phòng có hợp đồng. Test lỗi: giá chồng thời gian, chỉ số giảm, chỉ số đứt chuỗi, dịch vụ không có giá, landlord khác truy cập. Khi các dữ liệu này đọc được bằng `propertyId + roomId + year + month`, chuyển sang [Chương 3](03-invoice.md).
