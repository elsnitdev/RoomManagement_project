# Hướng dẫn chuẩn hóa API response và enum cho RoomManagement

**Phạm vi:** Backend .NET 10 (`Domain`, `Business`, `Data`, `API`). Tài liệu này là kế hoạch và mẫu code; các đoạn code chưa được áp dụng vào ứng dụng. Phần tài khoản/xác thực không nằm trong đợt triển khai này.

## 1. Hiện trạng và quyết định chung

- `RoomManagement.API/Program.cs` mới đăng ký DbContext và controllers. `AuthController` đang trả `501` bằng object ẩn danh. Chưa có response contract, xử lý lỗi tập trung hoặc API nghiệp vụ.
- Entity đang dùng `string` cho trạng thái/loại; SQL Server dùng `nvarchar`, `CHECK` và một số default. `RoomManagementDbContext` áp dụng các `IEntityTypeConfiguration<T>` trong `Data/Configurations`. Hai view số dư là projection chỉ đọc.
- **Quy ước đề xuất:** response thành công dùng `{ "data": ... }` (danh sách có thêm `meta`); lỗi dùng `ProblemDetails`/`ValidationProblemDetails` (`application/problem+json`). Không trả HTTP 200 cho lỗi. Enum trao đổi qua JSON bằng tên chuỗi; trong database tiếp tục lưu đúng chuỗi hiện có. Giữ tên thành viên enum khớp chính xác với giá trị `CHECK` để không cần đổi dữ liệu.
- Mọi mã lỗi nghiệp vụ (`ROOM_NOT_FOUND`, `ROOM_NUMBER_EXISTS`...) là chuỗi ổn định trong `extensions.code`, độc lập với câu thông báo tiếng Việt. `traceId` giúp dò log, không đưa stack trace, câu SQL hay thông tin nhạy cảm ra client.

## 2. Đặt code ở đâu

| Layer | Nơi thêm/sửa | Trách nhiệm |
| --- | --- | --- |
| Domain | `backend/src/RoomManagement.Domain/Enums/` và `Entities/` | Định nghĩa enum nghiệp vụ và đổi thuộc tính entity từ `string` sang enum theo từng nhóm. Không chứa HTTP hay JSON. |
| Business | `backend/src/RoomManagement.Business/Errors/`, service/use case | Quy tắc chuyển trạng thái, validate liên bảng, mã lỗi nghiệp vụ. Không quyết định HTTP status hoặc định dạng JSON. |
| Data | `backend/src/RoomManagement.Data/Configurations/`, `RoomManagementDbContext.cs`, migrations | `HasConversion<string>()`, giữ kiểu cột/default/`CHECK` tương thích; kiểm tra projection từ view. |
| API | `backend/src/RoomManagement.API/Contracts/Common/`, `Contracts/<Feature>/`, `Infrastructure/Errors/`, `Program.cs`, controllers | DTO request/response, serialization, validate input, ánh xạ lỗi sang HTTP, response thành công và phân trang. Không trả entity EF trực tiếp. |
| Frontend | API client và type/interface theo tính năng | Đọc `data`/`meta`; đọc `ProblemDetails.code` để hiển thị lỗi; dùng đúng chuỗi enum. |
| Tests | Test project tạo khi triển khai API | Kiểm tra JSON, HTTP status, enum, EF mapping, chuyển trạng thái và số dư. |

Trong đợt đầu, nên làm response/error foundation và một enum `RoomStatus` cùng endpoint phòng để xác nhận toàn bộ đường đi. Sau đó mở rộng enum cho các module khác; tránh đổi tất cả entity cùng lúc khi chưa có API dùng chúng.

## 3. Hợp đồng response

### 3.1 Thành công

Đặt tại `API/Contracts/Common/ApiResponse.cs`:

```csharp
namespace RoomManagement.API.Contracts.Common;

public sealed record ApiResponse<T>(T Data);

public sealed record PageMeta(int Page, int PageSize, long TotalItems, int TotalPages);

public sealed record PagedResponse<T>(IReadOnlyList<T> Data, PageMeta Meta);
```

Ví dụ một phòng (`GET /api/properties/{propertyId}/rooms/{id}` → `200`):

```json
{
  "data": {
    "id": "c5097626-1e6f-4a89-8ed8-e34d97becdf7",
    "roomNumber": "A101",
    "basePrice": 2500000.00,
    "status": "Available"
  }
}
```

Ví dụ danh sách: `{ "data": [ ... ], "meta": { "page": 1, "pageSize": 20, "totalItems": 43, "totalPages": 3 } }`. `totalPages = (int)Math.Ceiling((double)totalItems / pageSize)` sau khi bảo đảm `pageSize > 0`. Không thêm `success: true` vì HTTP status đã nói kết quả. Với tạo mới dùng `201 Created` và `Location`; xóa/thao tác không cần nội dung dùng `204 No Content`, không cố bọc response rỗng. Giới hạn `pageSize` (ví dụ 1–100) và mặc định thứ tự ổn định (`CreatedAt`, `Id`) để phân trang không lặp/bỏ bản ghi.

DTO cụ thể ở `API/Contracts/Rooms/RoomResponse.cs`:

```csharp
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Rooms;

public sealed record RoomResponse(
    Guid Id,
    Guid PropertyId,
    string RoomNumber,
    decimal BasePrice,
    int MaxOccupants,
    RoomStatus Status);
```

Controller map **chỉ** trường muốn công khai, không serialize `Room` entity. Với `CreatedAtAction`, trỏ đúng action lấy chi tiết và truyền đủ route values. Không đưa thông tin người duyệt, khóa lưu trữ tệp hoặc thuộc tính nội bộ vào DTO nếu endpoint không cần.

### 3.2 Lỗi và status code

| Tình huống | HTTP | `code` ví dụ | Nguồn xử lý |
| --- | --- | --- | --- |
| JSON/field/enum không hợp lệ | 400 | `VALIDATION_ERROR` | Model binding/validation ở API |
| Không tìm thấy tài nguyên trong phạm vi truy cập | 404 | `ROOM_NOT_FOUND` | Business → API |
| Trùng số phòng, hợp đồng Active, hóa đơn kỳ | 409 | `ROOM_NUMBER_EXISTS` | Business; DB unique là lớp chặn cuối |
| Vi phạm quy tắc nghiệp vụ (chỉ số giảm, chuyển trạng thái sai) | 422 | `METER_READING_DECREASED` | Business → API |
| Lỗi hệ thống ngoài dự kiến | 500 | `INTERNAL_ERROR` | Exception handler; log nội bộ |

Quyền truy cập (401/403) sẽ bổ sung khi làm xác thực; không suy đoán quyền từ một lỗi database. Với dữ liệu thuộc khu trọ khác, thiết kế endpoint có thể trả 404 để tránh lộ sự tồn tại của tài nguyên. Chọn một quy ước nhất quán khi triển khai phân quyền.

Mẫu lỗi:

```json
{
  "type": "about:blank",
  "title": "Không thể tạo phòng",
  "status": 409,
  "detail": "Số phòng A101 đã tồn tại trong khu trọ.",
  "instance": "/api/properties/c5097626-1e6f-4a89-8ed8-e34d97becdf7/rooms",
  "code": "ROOM_NUMBER_EXISTS",
  "traceId": "00-..."
}
```

`code` và `traceId` là extension của `ProblemDetails`; lỗi validation dùng thêm `errors` theo tên field. Không cố định thông báo làm khóa xử lý phía frontend vì nội dung có thể dịch/chỉnh sửa.

### 3.3 Cấu hình trong `Program.cs`

Thêm các phần sau quanh cấu hình hiện tại:

```csharp
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // "Available" trên wire; không chấp nhận 0, 1... làm enum.
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));
    });

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu yêu cầu không hợp lệ",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = "VALIDATION_ERROR";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

// Sau builder.Build(), trước app.MapControllers():
app.UseExceptionHandler();
```

`[ApiController]` tự xử lý lỗi model binding. Đăng ký `AddProblemDetails` và `UseExceptionHandler` cho lỗi chưa bắt, sau đó thêm `IExceptionHandler` riêng tại `API/Infrastructure/Errors/ApiExceptionHandler.cs` để ánh xạ lỗi có kiểu từ Business sang 404/409/422. Đăng ký handler **trước** `AddProblemDetails`:

```csharp
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
```

Trong handler: nhận exception nghiệp vụ gồm `Code`, `Message`, `StatusCategory`; đặt `Response.StatusCode`, tạo `ProblemDetails` với `Status`, `Title`, `Detail`, `Instance`, thêm `code`; gọi `IProblemDetailsService.TryWriteAsync(...)` để giữ định dạng thống nhất. Với exception khác, log đầy đủ ở server và trả thông báo 500 chung. Không catch `Exception` trong từng controller. Đặt `StatusCategory` ở Business (ví dụ `NotFound`, `Conflict`, `InvalidOperation`) rồi ánh xạ sang số HTTP ở API để Business không phụ thuộc ASP.NET.

Nếu muốn trả `ProblemDetails` cho 404 do route không tồn tại hoặc 405, thêm `app.UseStatusCodePages()` sau `UseExceptionHandler()` và kiểm tra kết quả thực tế bằng integration test. Response của `AuthController` hiện là object ẩn danh `501`; chỉ đổi nó khi xử lý module auth, tránh làm thay đổi một API chưa triển khai trong đợt này.

### 3.4 Validation request

Đặt DTO ở `API/Contracts/<Feature>/`, dùng `DataAnnotations` cho ràng buộc từng field, sau đó kiểm tra quy tắc cần truy vấn DB trong Business. Ví dụ:

```csharp
using System.ComponentModel.DataAnnotations;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Rooms;

public sealed record CreateRoomRequest(
    [Required, StringLength(50)] string RoomNumber,
    [Range(0, double.MaxValue)] decimal BasePrice,
    [Range(1, int.MaxValue)] int MaxOccupants,
    RoomStatus Status);
```

`[Required]` trên `string` không thay việc trim và kiểm tra chuỗi toàn khoảng trắng. `Range` là ví dụ kiểm tra đầu vào; Business vẫn xác minh khu trọ tồn tại, số phòng chưa trùng, trạng thái hợp lệ và thay đổi trạng thái có được phép hay không. Với enum không nullable, giá trị mặc định `0` vẫn có thể xuất hiện khi client bỏ field: thêm `[Required]` trên `RoomStatus?` rồi kiểm tra `Enum.IsDefined(value)` trong validator/service, hoặc dùng request riêng không cho client đặt `Status` và mặc định `Available` ở server. Cách thứ hai phù hợp `CreateRoom`.

## 4. Danh mục enum theo schema hiện tại

Tên enum là đề xuất; **giá trị thành viên phải giữ đúng chính tả tiếng Anh trong `CHECK`**. Có thể thêm enum theo module, không cần tạo tất cả ngay.

| Nhóm | Enum đề xuất | Giá trị hiện có | Cột liên quan |
| --- | --- | --- | --- |
| Phòng | `RoomStatus` | `Available`, `Occupied`, `Maintenance`, `Inactive` | `Rooms.Status` |
| Hợp đồng | `ContractStatus` | `Draft`, `Active`, `Expired`, `Terminated`, `Cancelled` | `Contracts.Status` |
| Tiện ích | `UtilityType` | `Electricity`, `Water` | `UtilityRates.UtilityType`, `MeterReadings.MeterType` |
| Hóa đơn | `InvoiceType` | `Monthly`, `Deposit`, `Damage`, `Settlement`, `Other` | `Invoices.InvoiceType` |
| Hóa đơn | `InvoiceStatus` | `Draft`, `Issued`, `Cancelled` | `Invoices.Status` |
| Dòng hóa đơn | `InvoiceItemType` | `Rent`, `Deposit`, `Electricity`, `Water`, `Service`, `Damage`, `Penalty`, `Discount`, `Other` | `InvoiceItems.ItemType` |
| Điều chỉnh | `InvoiceAdjustmentType` | `Discount`, `Correction`, `WriteOff` | `InvoiceAdjustments.AdjustmentType` |
| Thanh toán | `PaymentMethod` | `Cash`, `BankTransfer`, `OnlinePayment`, `Other` | `Payments.PaymentMethod` |
| Thanh toán | `PaymentStatus` | `Pending`, `Success`, `Failed`, `Cancelled` | `Payments.Status`, `PaymentReviewEvents.PreviousStatus/NewStatus` |
| Hoàn tiền | `RefundStatus` | `Pending`, `Success`, `Failed`, `Cancelled` | `Refunds.Status` |
| Yêu cầu | `TenantRequestCategory` | `Repair`, `Billing`, `Question`, `Complaint`, `Other` | `TenantRequests.Category` |
| Yêu cầu | `TenantRequestPriority` | `Low`, `Medium`, `High` | `TenantRequests.Priority` |
| Yêu cầu | `TenantRequestStatus` | `Pending`, `InProgress`, `Resolved` | `TenantRequests.Status` |
| Hồ sơ | `Gender` | `Male`, `Female`, `Other` | `TenantProfiles.Gender` (nullable) |
| Tài liệu | `ContractDocumentType` | `ContractPage`, `CitizenId`, `Handover`, `Appendix`, `Other` | `ContractDocuments.DocumentType` |

`Users.Role` hiện đã có `UserRoles` constant, thuộc phần auth nên chưa đưa vào lượt này. `DepositApplications.Reason`, `InvoiceAdjustment.Reason`, `VoidReason`, `Services.Unit`... là văn bản tự do, **không** biến thành enum. `Unpaid`, `Partial`, `Paid`, `Overdue` là trạng thái **suy ra** từ số dư hóa đơn và `DueDate`, không thêm vào `InvoiceStatus` hay cột `Invoices.Status`. Trạng thái cọc cũng suy ra từ `vwContractDepositBalances`.

## 5. Cách thêm một enum từ đầu đến cuối: `RoomStatus`

### Bước 1 — Domain

Tạo `Domain/Enums/RoomStatus.cs`:

```csharp
namespace RoomManagement.Domain.Enums;

public enum RoomStatus
{
    Available = 1,
    Occupied = 2,
    Maintenance = 3,
    Inactive = 4
}
```

Đổi `Domain/Entities/Room.cs`: `public RoomStatus Status { get; set; } = RoomStatus.Available;` và thêm `using RoomManagement.Domain.Enums;`. Không sử dụng `0` làm trạng thái hợp lệ; các số 1–4 chỉ là giá trị nội bộ .NET, không lưu vào DB/JSON. Khi tạo room, gán trạng thái tại Business, không tin giá trị mặc định của C# thay cho quy tắc nghiệp vụ.

### Bước 2 — EF Core/SQL Server

Trong `Data/Configurations/RoomConfiguration.cs`, thay **chỉ mapping `Status`**:

```csharp
builder.Property(e => e.Status)
    .HasConversion<string>()
    .HasColumnType("nvarchar(20)")
    .IsRequired()
    .HasDefaultValueSql("N'Available'", "DF_Rooms_Status");
```

Giữ `CK_Rooms_Status` hiện có. `HasConversion<string>()` lưu `RoomStatus.Available` thành `Available`; không dùng ordinal/int vì SQL đã có dữ liệu và `CHECK` dạng chuỗi. Trước khi đổi các cột khác, kiểm tra cả index/filter và SQL/view có so sánh giá trị chuỗi (ví dụ `Status = N'Active'`). Với view projection (`InvoiceBalanceRow.InvoiceType/Status`), có thể tiếp tục để string nếu chỉ đọc; nếu đổi sang enum, phải map conversion riêng cho view và đối chiếu dữ liệu trả về.

### Bước 3 — API

Thêm `JsonStringEnumConverter(allowIntegerValues: false)` như mục 3.3. Request chứa `"status": "Available"`; `"status": 1` hoặc `"status": "Unknown"` phải nhận 400. Tránh `JsonStringEnumConverter` cho phép số theo mặc định. Nếu request cho chọn enum, validate `Enum.IsDefined(...)`; enum có thể nhận giá trị `0` trong code dù JSON không chấp nhận số. Không cho API `CreateRoom` tự đặt `Occupied` nếu chưa có hợp đồng Active; trạng thái phòng cần được cập nhật theo luồng hợp đồng và quy tắc chuyển trạng thái.

### Bước 4 — Business

Tạo phương thức chuyển trạng thái có kiểm tra trạng thái hiện tại và dữ liệu liên quan. Ví dụ `Available → Maintenance` chỉ khi không có hợp đồng Active; `Occupied` phải gắn với hợp đồng Active. Trả lỗi nghiệp vụ có `code` ổn định. `RoomStatus` không thay thế quy tắc liên bảng; check constraint chỉ chặn tên trạng thái sai.

### Bước 5 — migration và dữ liệu

1. Kiểm tra các giá trị hiện có: `SELECT Status, COUNT(*) FROM dbo.Rooms GROUP BY Status;` (chạy trên DB đúng môi trường). Nếu có chuỗi ngoài tập enum, sửa dữ liệu bằng migration/script có review trước khi deploy.
2. Nếu có thay đổi schema, đặt `ConnectionStrings__RoomManagement` cho design-time factory và chạy từ root repo:

   ```powershell
   dotnet ef migrations add AddRoomStatusMapping --project backend/src/RoomManagement.Data --startup-project backend/src/RoomManagement.API
   dotnet ef migrations has-pending-model-changes --project backend/src/RoomManagement.Data --startup-project backend/src/RoomManagement.API
   ```

   **Đọc migration được sinh ra**: chỉ đổi CLR type mà SQL vẫn là `nvarchar(20)` thường không cần đổi cột; không áp dụng migration tạo/drop cột ngoài ý muốn. Nếu không đổi schema, kiểm tra model trước và không tạo migration rỗng chỉ để ghi nhận refactor C#.
3. Build và integration test. Nếu đổi tập giá trị `CHECK`/default/độ dài cột thì bắt buộc migration có chủ đích và phải thử trên DB test trước khi cập nhật DB thật.
4. Khi đổi tên enum member hoặc thêm trạng thái sau này, xem đó là thay đổi **API + dữ liệu + CHECK + mọi SQL/view/index/filter**. Dùng kế hoạch tương thích dữ liệu, không đổi tên lặng lẽ.

## 6. Quy tắc nghiệp vụ đi cùng enum

- `InvoiceStatus`: chỉ `Draft → Issued` sau khi đã có dòng tiền, tổng bằng tổng dòng, kỳ hợp lệ; không sửa `InvoiceItems` sau phát hành. Nợ/đã trả/quá hạn là kết quả tính toán, không là status lưu.
- `PaymentStatus`: `Pending` không giảm nợ. Chỉ `Success` với `VerifiedAmount > 0`, `PaidAt`, `VerifiedAt`, `VerifiedByUserId` hợp lệ mới giảm nợ. Ghi `PaymentReviewEvent` cùng transaction với thay đổi Payment. Không gán `Failed` khi ngân hàng đã nhận một phần.
- `TenantRequestStatus`: tiến trình dự kiến `Pending → InProgress → Resolved`; khi `Resolved`, phải đặt `ResolvedAt`, khi trạng thái khác phải để null theo `CHECK`.
- `ContractStatus`: DB chỉ giới hạn tên và tối đa một `Active` trên mỗi phòng. Business còn phải kiểm tra khoảng ngày trùng, người ở và sức chứa trước khi Active.
- `UtilityType` dùng chung cho giá và chỉ số. Không trộn điện/nước vào enum dịch vụ thường rồi tính hai lần.

## 7. Thứ tự triển khai và điểm kiểm chứng

1. **API nền tảng:** thêm `ApiResponse<T>`, `PagedResponse<T>`, `ProblemDetails`, validation response, exception handler và mapping lỗi Business. Tạo một endpoint mẫu của module phòng. Kiểm tra `200/201/204/400/404/409/422/500`, `Content-Type`, `Location`, `code`, `traceId`.
2. **Enum phòng:** `RoomStatus` → entity → EF conversion → request/response → test round-trip SQL/JSON. Kiểm tra DB vẫn lưu `Available`, không lưu `1`; numeric JSON bị từ chối; chuỗi sai nhận 400.
3. **Enum theo module:** hợp đồng, giá/chỉ số, hóa đơn, thanh toán, yêu cầu. Mỗi module kiểm tra cùng bốn điểm: giá trị `CHECK`, entity, JSON, luồng trạng thái. Ưu tiên theo kế hoạch chức năng, không thêm enum cho văn bản tự do.
4. **Frontend:** cập nhật client một lần theo contract; `2xx` đọc `data`, lỗi đọc `code`/`errors`, các status enum hiện chuỗi từ API. Không so sánh với số enum TypeScript.
5. **Hồi quy:** build `dotnet build backend/RoomManagement.sln --no-restore -m:1 -p:UseSharedCompilation=false`; kiểm tra model changes và thử trên SQL Server test trước khi cập nhật DB thật. Unit test quy tắc chuyển trạng thái và integration test cho lỗi validation/trùng dữ liệu; riêng payment cần test transaction và số dư từ view.

**Definition of done cho mỗi module:** response và error có schema nhất quán; enum khớp DB `CHECK`; request sai bị chặn trước khi ghi; mọi thay đổi trạng thái có test luồng hợp lệ/bị từ chối; migration được review; frontend đọc đúng contract.
