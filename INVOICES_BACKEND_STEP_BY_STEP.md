# Mentor guide: code backend Invoices từng bước

**Đối chiếu ngày 02/11/2026.** Tài liệu này hướng dẫn thực hiện [kế hoạch Invoice](plans/invoices-backend-implementation-plan.md) trên code hiện tại; đây là hướng dẫn, **chưa phải phần code đã triển khai**. Bắt đầu từ P0 rồi làm P1 → P1b → P2 → P3. Mỗi bước chỉ chuyển tiếp khi checkpoint và test của bước đó đạt.

## 0. Đọc kiến trúc trước khi viết

Luồng hiện tại để học theo: `RoomsController` → `RoomService` → `IRoomStore` → `RoomStore` → `RoomManagementDbContext`. Controller nhận HTTP/DTO; service giữ quy tắc nghiệp vụ và quyền; store đọc/ghi EF và transaction; Domain giữ entity/enum. `Program.cs` đăng ký các service cụ thể, còn `Data/ServiceCollectionExtensions.cs` đăng ký store. Lỗi nghiệp vụ dùng `BusinessException`, được `ApiExceptionHandler` chuyển thành ProblemDetails. Response thành công dùng `ApiResponse<T>` hoặc `PageResponse<T>`.

| Đọc file đang có | Học điều gì |
| --- | --- |
| `backend/src/RoomManagement.API/Controllers/RoomsController.cs` | Route, `[Authorize]`, HTTP status và response envelope. |
| `backend/src/RoomManagement.API/Contracts/Rooms/RoomRequests.cs` | Request DTO và cách đổi sang Business input. |
| `backend/src/RoomManagement.Business/Services/RoomService.cs` | Kiểm tra chủ trọ và quy tắc nghiệp vụ. |
| `backend/src/RoomManagement.Business/Interfaces/IRoomStore.cs` | Hợp đồng giữa Business và Data. |
| `backend/src/RoomManagement.Data/RoomStore.cs` | Truy vấn có phạm vi sở hữu khu trọ. |
| `backend/src/RoomManagement.Data/RoomBatchStore.cs` | Transaction, idempotency và khóa SQL Server. |
| `backend/src/RoomManagement.API/Infrastructure/Errors/ApiExceptionHandler.cs` | Mã lỗi nghiệp vụ trả về API. |

**Bài tập đầu tiên:** theo đường đi của `GET /api/properties/{propertyId}/rooms/{roomId}` qua các file trên. Sau đó dùng cùng mẫu cho `GET /api/invoices/{id}`. Đừng cho controller truy vấn `DbContext` trực tiếp, vì quyền sở hữu phải được áp dụng nhất quán ở service/store.

## P0. Chuẩn bị dữ liệu đầu vào của hóa đơn

### Bước 1 — Hợp đồng và người thuê

**File đã có:** `backend/src/RoomManagement.Domain/Entities/Contract.cs`, `ContractTenant.cs`, `TenantProfile.cs`; `backend/src/RoomManagement.Data/Configurations/ContractConfiguration.cs`, `ContractTenantConfiguration.cs`; các `DbSet` trong `RoomManagementDbContext.cs`.

**File cần tạo:**

| Tầng | File | Nội dung chính |
| --- | --- | --- |
| Business DTO | `backend/src/RoomManagement.Business/DTOs/ContractModels.cs` | Input tạo/kích hoạt hợp đồng, filter và kết quả đọc. |
| Business interface | `backend/src/RoomManagement.Business/Interfaces/IContractStore.cs` | `GetOwnedAsync`, `ListAsync`, `AddAsync`, `SaveAsync`, kiểm tra hợp đồng trùng thời gian. |
| Business service | `backend/src/RoomManagement.Business/Services/ContractService.cs` | Kiểm tra phòng thuộc chủ trọ, ngày thuê, giá/cọc, người thuê, chuyển trạng thái. |
| Data | `backend/src/RoomManagement.Data/ContractStore.cs` | EF query nối `Contracts → Rooms → Properties.OwnerUserId`; ghi transaction khi tạo hợp đồng và người thuê. |
| API DTO | `backend/src/RoomManagement.API/Contracts/Contracts/ContractRequests.cs`, `ContractResponse.cs` | Validate request và trả thông tin hợp đồng. |
| API controller | `backend/src/RoomManagement.API/Controllers/ContractsController.cs` | Create/get/list/activate và gắn người thuê. |

Đăng ký `IContractStore → ContractStore` trong `backend/src/RoomManagement.Data/ServiceCollectionExtensions.cs`; đăng ký `ContractService` trong `backend/src/RoomManagement.API/Program.cs`. **Checkpoint:** qua API tạo và kích hoạt được một hợp đồng hợp lệ; chủ trọ khác không đọc được hợp đồng đó; không có hai hợp đồng `Active` cùng phòng theo ràng buộc hiện có.

### Bước 2 — Chỉ số, đơn giá và dịch vụ

**File đã có:** entity/configuration cho `UtilityRate`, `MeterReading`, `Service`, `RoomService` trong `Domain/Entities` và `Data/Configurations`.

| Nghiệp vụ | File mới trong Business | File mới trong Data | File mới trong API |
| --- | --- | --- | --- |
| Giá điện nước | `DTOs/UtilityRateModels.cs`, `Interfaces/IUtilityRateStore.cs`, `Services/UtilityRateService.cs` | `UtilityRateStore.cs` | `Contracts/Utilities/UtilityRateRequests.cs`, `Controllers/UtilityRatesController.cs` |
| Chỉ số | `DTOs/MeterReadingModels.cs`, `Interfaces/IMeterReadingStore.cs`, `Services/MeterReadingService.cs` | `MeterReadingStore.cs` | `Contracts/Utilities/MeterReadingRequests.cs`, `Controllers/MeterReadingsController.cs` |
| Dịch vụ phòng | `DTOs/RoomServiceModels.cs`, `Interfaces/IRoomServiceStore.cs`, `Services/RoomServiceService.cs` | `RoomServiceStore.cs` | `Contracts/Services/RoomServiceRequests.cs`, `Controllers/RoomServicesController.cs` |

Mọi đường dẫn trong bảng tính từ `backend/src/RoomManagement.Business`, `backend/src/RoomManagement.Data` hoặc `backend/src/RoomManagement.API` tương ứng. Store chỉ lấy dữ liệu trong khu trọ thuộc chủ trọ hiện tại. Service kiểm tra khoảng giá không chồng lấn, `PreviousValue` nối với `CurrentValue` kỳ trước, và giá dịch vụ `CustomPrice ?? DefaultPrice`. Khi thiếu giá/chỉ số, trả lỗi dữ liệu đầu vào; không cho phép phép tính tự coi là 0. Đăng ký store/service như bước 1.

**Checkpoint:** một phòng có hợp đồng `Active`, đơn giá, chỉ số tháng và dịch vụ đủ để tạo hóa đơn tháng qua API. Viết test quyền và dữ liệu thiếu trước khi sang P1.

## P1. Tạo, phát hành và đọc hóa đơn

### Bước 3 — Tính tiền bằng hàm thuần

**File đã có:** `Domain/Entities/Invoice.cs`, `InvoiceItem.cs`, `Contract.cs`, `MeterReading.cs`, `UtilityRate.cs`, `RoomService.cs`, `Service.cs`; enum `InvoiceType`, `InvoiceItemType`, `InvoiceStatus`.

**File cần tạo:** `backend/src/RoomManagement.Business/DTOs/InvoiceModels.cs` (input/result của preview, filter, item và detail), `backend/src/RoomManagement.Business/Services/MonthlyInvoiceCalculator.cs`, `backend/tests/RoomManagement.Business.Tests/MonthlyInvoiceCalculatorTests.cs`.

Calculator nhận dữ liệu đã kiểm tra: hợp đồng, kỳ, chỉ số, đơn giá và dịch vụ. Nó trả danh sách item cùng tổng; không truy vấn database, không lấy `DateTime.Now` bên trong. Với tiền phòng tháng lẻ, làm theo chính sách đã chốt rồi lưu item `Quantity = 1`, `UnitPrice = số tiền thuê đã chia kỳ`. Điện nước dùng `Quantity = CurrentValue - PreviousValue`; đơn giá lưu vào item để hóa đơn không đổi khi bảng giá đổi. `TotalAmount = Sum(item.Amount)`. Dùng `decimal` và cách làm tròn tương thích với SQL `ROUND(..., 2)`; khi code C# có thể dùng `decimal.Round(value, 2, MidpointRounding.AwayFromZero)`.

**Chưa chốt trước khi code calculator:** tiền thuê/dịch vụ áp dụng một phần tháng, ngày đến hạn thuộc tháng nào, và giá mới giữa kỳ có áp dụng ngay hay kỳ sau. Plan đề xuất kỳ tháng dương lịch, điện nước dùng giá đầu kỳ, nhưng đây chưa phải quyết định cuối cùng. Viết test cho tháng 2 năm nhuận, chỉ số giảm, giá thiếu và tổng item.

### Bước 4 — Lưu draft và xem trước

**File đã có:** `Data/Configurations/InvoiceConfiguration.cs` có unique index theo hợp đồng/kỳ và cọc; `InvoiceItemConfiguration.cs` kiểm tra phép nhân thành tiền; `RoomManagementDbContext.cs` có `DbSet<Invoice>`/`DbSet<InvoiceItem>`.

**File cần tạo:** `backend/src/RoomManagement.Business/Interfaces/IInvoiceStore.cs`, `Services/InvoiceService.cs`; `backend/src/RoomManagement.Data/InvoiceStore.cs`; `backend/src/RoomManagement.API/Contracts/Invoices/InvoiceRequests.cs`, `InvoiceResponse.cs`; `backend/src/RoomManagement.API/Controllers/InvoicesController.cs`.

Trong `InvoiceService`, xác nhận chủ trọ/phòng/hợp đồng, tải dữ liệu đầu vào, gọi calculator và trả preview. Route `POST /api/properties/{propertyId}/invoices/monthly/preview` **chỉ tính**, không ghi DB. Route tạo `POST /api/properties/{propertyId}/invoices/monthly` phải tính lại trên server và gọi store ghi `Invoice(Draft)` + toàn bộ `InvoiceItems` trong **một transaction**. Không tin `TotalAmount` client gửi. Bắt unique violation của SQL Server `2601/2627` và đổi thành `INVOICE_ALREADY_EXISTS`/HTTP 409; không dựa vào kiểm tra `AnyAsync` để chống request đồng thời. Mã hóa đơn phải do server tạo và unique.

Tạo draft cọc qua `POST /api/contracts/{contractId}/invoices/deposit`: item cọc lấy `Contract.DepositAmount`; không đưa tiền cọc vào tiền thuê tháng. Đăng ký `IInvoiceStore → InvoiceStore` trong Data DI và `InvoiceService` trong `Program.cs`.

**Checkpoint:** preview không thêm dòng DB; tạo một kỳ hai lần chỉ có một draft chưa hủy; lỗi giữa chừng không để lại header mồ côi; số header bằng tổng item.

### Bước 5 — Phát hành, hủy, danh sách và chi tiết

Tiếp tục trong `InvoiceService`, `IInvoiceStore`, `InvoiceStore`, `InvoicesController` của bước 4. Thêm `POST /api/invoices/{id}/issue`, `POST /api/invoices/{id}/cancel`, `GET /api/properties/{propertyId}/invoices` (lọc kỳ/phòng/trạng thái, phân trang ở SQL) và `GET /api/invoices/{id}`. `Issue` chỉ nhận `Draft`, có ít nhất một item, tổng khớp, ngày hợp lệ và quyền sở hữu; `Cancel` chặn hóa đơn đã phát sinh giao dịch tài chính. Sau phát hành không sửa `InvoiceItems` hoặc `TotalAmount`.

Chi tiết hóa đơn đọc `DbContext.InvoiceBalances` ánh xạ tới `vwInvoiceBalances`, cộng với item và lịch sử. **File đã có:** `backend/src/RoomManagement.Data/InvoiceBalanceRow.cs`, `RoomManagementDbContext.cs`, `Migrations/InitialSchema.Views.cs`. `Unpaid`, `Partial`, `Paid`, `Overdue` là nhãn suy ra từ `RemainingAmount` và ngày hạn theo múi giờ nghiệp vụ Việt Nam; chúng không phải giá trị của `Invoices.Status`. Tenant chỉ được đọc hóa đơn `Issued` khi `TenantProfile.UserId` nối được tới `ContractTenant` của hợp đồng; thêm endpoint tenant riêng sau khi quyền này có test.

**Checkpoint:** tenant không thấy draft hoặc hóa đơn hợp đồng khác; chủ trọ khác không thấy hóa đơn; danh sách phân trang đúng; view trả số dư đúng.

## P1b. Cộng hoặc trừ tiền sau phát hành

### Bước 6 — Thay đổi schema và view

**File cần sửa:** `backend/src/RoomManagement.Domain/Enums/InvoiceAdjustmentType.cs` thêm `AdditionalCharge`; `backend/src/RoomManagement.Data/Configurations/InvoiceAdjustmentConfiguration.cs` cho check constraint nhận loại mới. **File cần tạo:** migration mới trong `backend/src/RoomManagement.Data/Migrations/`, ví dụ `AddInvoiceAdditionalCharge`. Xem định nghĩa view gốc ở `backend/src/RoomManagement.Data/Migrations/InitialSchema.Views.cs`, nhưng **không sửa migration gốc** đã có.

Trong migration mới, cập nhật check constraint rồi `ALTER VIEW dbo.vwInvoiceBalances` để `TotalAdjustments = tổng Discount/Correction/WriteOff − tổng AdditionalCharge` trên adjustment chưa void. Giữ tên cột view hiện tại để `InvoiceBalanceRow.cs` tiếp tục đọc được; `EffectiveAmount = TotalAmount − TotalAdjustments`. `Down` phục hồi view và check cũ. Các dòng adjustment cũ vẫn là khoản trừ nên không cần backfill. Review SQL migration và test nâng cấp trên SQL Server trước khi áp dụng vào DB phát triển.

### Bước 7 — API điều chỉnh và void

**File đã có:** `Domain/Entities/InvoiceAdjustment.cs` có `Amount`, `Reason`, `IdempotencyKey`, người tạo và các trường void.

**File cần tạo:** `Business/DTOs/InvoiceAdjustmentModels.cs`, `Business/Interfaces/IInvoiceAdjustmentStore.cs`, `Business/Services/InvoiceAdjustmentService.cs`, `Data/InvoiceAdjustmentStore.cs`, `API/Contracts/Invoices/InvoiceAdjustmentRequests.cs`. Thêm route vào `API/Controllers/InvoicesController.cs`; đăng ký DI như các bước trước.

Thêm `POST /api/invoices/{id}/adjustments`, `GET .../adjustments`, `POST .../adjustments/{adjustmentId}/void`. MVP chỉ thao tác `Monthly/Issued`; landlord sở hữu mới được tạo/void. `Amount > 0`, `Reason` bắt buộc, giảm trừ không làm `EffectiveAmount` âm. Không sửa/xóa bản ghi cũ: void ghi thời gian, người và lý do; view không tính dòng đã void. Cùng `IdempotencyKey` và cùng payload trả bản ghi cũ; cùng key nhưng payload khác trả 409. Dùng transaction và khóa theo invoice cho create/void để hai yêu cầu giảm đồng thời không vượt tổng.

Trả trong detail `baseTotal`, `totalIncreases`, `totalDecreases`, `effectiveTotal`, `remainingAmount`, `amountDue = max(0, remainingAmount)` và `creditToRefund = max(0, -remainingAmount)`. Cộng phí sau khi đã trả đủ tạo nợ mới; giảm sau khi trả đủ tạo tiền chờ hoàn, không tự hoàn tiền. **Checkpoint:** migration không làm đổi số dư cũ; cộng/trừ/void và retry cho số dư đúng; tenant chỉ được đọc lịch sử của mình.

## P2. Ghi nhận và xác minh thanh toán

### Bước 8 — Payment

**File đã có:** `Domain/Entities/Payment.cs`, `PaymentReviewEvent.cs`, `Data/Configurations/PaymentConfiguration.cs`, `PaymentReviewEventConfiguration.cs`, view số dư.

**File cần tạo:** `Business/DTOs/PaymentModels.cs`, `Business/Interfaces/IPaymentStore.cs`, `Business/Services/PaymentService.cs`, `Data/PaymentStore.cs`, `API/Contracts/Payments/PaymentRequests.cs`, `PaymentResponse.cs`, `API/Controllers/PaymentsController.cs` (hoặc route lồng trong `InvoicesController`, chọn một cách và giữ nhất quán).

Route tạo payment `Pending` lưu `ClaimedAmount`; Pending không giảm công nợ. Route review của landlord xác nhận số thực nhận `VerifiedAmount`, trạng thái, người và thời điểm; khi cần ghi `PaymentReviewEvent` trong cùng transaction. **Theo plan, nếu ngân hàng thực nhận nhiều hơn số dư thì vẫn ghi đúng số thực nhận** và hiển thị số dư âm để đối soát. Payment `Success` không được sửa/xóa trực tiếp. Dùng khóa cùng invoice với bước 7, kiểm tra idempotency và quyền.

**Checkpoint:** Pending/partial/full/overpayment, retry và hai review đồng thời có số dư đúng; lịch sử xác minh đọc được.

## P3. Hoàn tiền và sử dụng cọc

### Bước 9 — Refund và DepositApplication

**File đã có:** `Domain/Entities/Refund.cs`, `DepositApplication.cs`; EF configuration của hai bảng; `Data/ContractDepositBalanceRow.cs` và hai view trong `Migrations/InitialSchema.Views.cs`.

**File cần tạo:** `Business/DTOs/RefundModels.cs`, `DepositApplicationModels.cs`; `Business/Interfaces/IRefundStore.cs`, `IDepositApplicationStore.cs`; `Business/Services/RefundService.cs`, `DepositApplicationService.cs`; `Data/RefundStore.cs`, `DepositApplicationStore.cs`; `API/Contracts/Payments/RefundRequests.cs`, `API/Contracts/Invoices/DepositApplicationRequests.cs`; controller hoặc route tương ứng. Đăng ký DI.

Refund hóa đơn thường do giảm/trả dư chỉ xác nhận tới mức `creditToRefund`; hoàn cọc không được vượt `DepositHeld`. Cọc chỉ bù hóa đơn `Issued` khác cọc của **cùng hợp đồng**. Idempotency, quyền, lý do và khóa transaction cần áp dụng cho cả refund lẫn phân bổ cọc. Test chuỗi cọc thu 1,2 triệu + 0,8 triệu, bù 0,5 triệu và hoàn 1,5 triệu; `DepositHeld` cuối cùng bằng 0.

## Cách kiểm tra sau mỗi bước

1. Unit test phép tính trong `backend/tests/RoomManagement.Business.Tests/MonthlyInvoiceCalculatorTests.cs` (tạo mới).
2. Integration test store/view/migration/quyền trong `backend/tests/RoomManagement.Tests/InvoiceStoreTests.cs`, `InvoiceAdjustmentTests.cs`, `PaymentTests.cs` (tạo mới). Dùng fixture `backend/tests/RoomManagement.Tests/SqlServerDatabase.cs`; test SQL chỉ chạy khi `ROOMMANAGEMENT_TEST_SQLSERVER` trỏ tới **SQL Server dành riêng cho test**.
3. Chạy `dotnet test backend/RoomManagement.sln` sau mỗi lát cắt. Test có `[SqlServerFact]` sẽ bị skip khi thiếu biến môi trường; một kết quả xanh nhưng bị skip **không chứng minh** view hoặc migration đúng.
4. Khi đến bước 6, dùng `dotnet ef migrations add AddInvoiceAdditionalCharge --project backend/src/RoomManagement.Data --startup-project backend/src/RoomManagement.API`; review migration và script SQL tạo ra. Chỉ chạy `dotnet ef database update --project backend/src/RoomManagement.Data --startup-project backend/src/RoomManagement.API` trên DB phát triển sau khi đã xem script và cấu hình kết nối đúng.

**Thứ tự bắt đầu thực tế:** làm bước 1 và 2 trước, rồi mới viết calculator. Nếu dữ liệu đầu vào chưa có API, có thể unit test calculator bằng object trong bộ nhớ, nhưng không thể coi luồng Invoice qua API là hoàn thành.

## Mẫu code để thực hành theo từng bước

Các đoạn dưới đây là **phần lõi minh họa**, không phải file hoàn chỉnh để dán nguyên khối: các input/result type, validation, quyền, DI và xử lý lỗi phải được nối theo từng bước phía trên. Mỗi đoạn ghi rõ file đích và điều kiện cần kiểm tra.

### Mẫu A — Bước 1: truy vấn hợp đồng đúng chủ trọ

**File đích:** `backend/src/RoomManagement.Data/ContractStore.cs` (tạo mới). Truy vấn từ contract qua room tới property trước khi đọc hoặc sửa; đừng dùng `ContractId` từ request làm bằng chứng quyền.

```csharp
private IQueryable<Contract> OwnedContracts(Guid propertyId, string ownerId) =>
    from contract in _db.Contracts
    join room in _db.Rooms on contract.RoomId equals room.Id
    join property in _db.Properties on room.PropertyId equals property.Id
    where property.Id == propertyId && property.OwnerUserId == ownerId
    select contract;

public Task<Contract?> GetOwnedAsync(
    Guid propertyId, Guid contractId, string ownerId, CancellationToken ct) =>
    OwnedContracts(propertyId, ownerId)
        .AsNoTracking()
        .SingleOrDefaultAsync(c => c.Id == contractId, ct);
```

**Tự kiểm:** cùng `contractId`, landlord sở hữu nhận dữ liệu; landlord khác nhận `null` và service chuyển thành lỗi tài nguyên không tìm thấy. Thao tác ghi phải tải entity có tracking trong transaction, không dùng `AsNoTracking()` của ví dụ đọc.

### Mẫu B — Bước 3: tính tiền thuê và điện bằng `decimal`

**File đích:** `backend/src/RoomManagement.Business/Services/MonthlyInvoiceCalculator.cs` (tạo mới). Ví dụ này trả tiền thuê tháng lẻ và dòng điện; bổ sung nước/dịch vụ theo cùng mẫu sau khi có chính sách tính kỳ.

```csharp
private static decimal Money(decimal value) =>
    decimal.Round(value, 2, MidpointRounding.AwayFromZero);

private static decimal RentForMonth(Contract contract, int year, int month)
{
    var firstDay = new DateOnly(year, month, 1);
    var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
    var first = contract.StartDate > firstDay ? contract.StartDate : firstDay;
    var last = contract.EndDate is { } end && end < lastDay ? end : lastDay;
    var occupiedDays = last.DayNumber - first.DayNumber + 1;
    if (occupiedDays <= 0) throw new ArgumentException("Hợp đồng không thuộc kỳ hóa đơn.");
    return Money(contract.MonthlyRent * occupiedDays / DateTime.DaysInMonth(year, month));
}

private static InvoiceItem? ElectricityItem(
    Guid invoiceId, MeterReading reading, UtilityRate rate)
{
    var quantity = reading.CurrentValue - reading.PreviousValue;
    if (quantity < 0) throw new ArgumentException("Chỉ số điện giảm.");
    if (quantity == 0) return null; // CK_InvoiceItems_Amounts yêu cầu Quantity > 0.
    return new InvoiceItem
    {
        Id = Guid.NewGuid(), InvoiceId = invoiceId,
        ItemType = InvoiceItemType.Electricity,
        Description = $"Điện kỳ {reading.Month}/{reading.Year}",
        Quantity = quantity, UnitPrice = rate.UnitPrice,
        Amount = Money(quantity * rate.UnitPrice), ReferenceId = reading.Id
    };
}
```

Khi tạo item tiền thuê theo chính sách trên: `Quantity = 1`, `UnitPrice = RentForMonth(...)`, `Amount = RentForMonth(...)`. Trước khi gọi hàm, service phải chứng minh `reading` thuộc đúng phòng/kỳ, `rate` thuộc đúng khu và có hiệu lực đầu kỳ. Test ví dụ: hợp đồng bắt đầu **16/04/2026**, tiền thuê 3.000.000đ/tháng → 15/30 ngày, item tiền thuê 1.500.000đ. Nếu giá trị chỉ số không đổi, đừng insert item có `Quantity = 0` vì DB sẽ từ chối.

### Mẫu C — Bước 4–5: phạm vi sở hữu và transaction hóa đơn

**File đích:** `backend/src/RoomManagement.Data/InvoiceStore.cs` (tạo mới). Truy vấn chi tiết phải giới hạn theo chủ trọ; tạo draft phải lưu header và items cùng một transaction.

```csharp
private IQueryable<Invoice> OwnedInvoices(Guid propertyId, string ownerId) =>
    from invoice in _db.Invoices
    join room in _db.Rooms on invoice.RoomId equals room.Id
    join property in _db.Properties on room.PropertyId equals property.Id
    where property.Id == propertyId && property.OwnerUserId == ownerId
    select invoice;

// Bên trong CreateDraftAsync, sau khi service đã kiểm tra quyền, dữ liệu và tính tiền:
await using var tx = await _db.Database.BeginTransactionAsync(ct);
_db.Invoices.Add(invoice);               // invoice.Id đã được tạo ở server.
_db.InvoiceItems.AddRange(items);         // Mỗi item.InvoiceId == invoice.Id.
await _db.SaveChangesAsync(ct);           // Invoice.TotalAmount == items.Sum(x => x.Amount).
await tx.CommitAsync(ct);
```

Trong bản hoàn chỉnh, bắt `DbUpdateException` có `SqlException.Number` 2601/2627 để trả `INVOICE_ALREADY_EXISTS` hoặc xung đột mã hóa đơn; unique index của `InvoiceConfiguration.cs` mới là chốt cuối chống tạo trùng. Khi phát hành, mở transaction và khóa **cùng invoice**, tải trạng thái hiện tại, kiểm `Draft`, `items.Count > 0`, `items.Sum(x => x.Amount) == invoice.TotalAmount`, rồi đổi sang `Issued` và commit. Dùng một hàm khóa chung cho phát hành, điều chỉnh, review payment và refund; `RoomBatchStore.cs` có ví dụ `sp_getapplock`, nhưng resource nên theo `invoice:{id}` và phải kiểm tra kết quả khóa âm.

### Mẫu D — Bước 6: cộng phí vào view mà vẫn giữ dữ liệu cũ

**File đích:** sửa `backend/src/RoomManagement.Domain/Enums/InvoiceAdjustmentType.cs` bằng cách thêm `AdditionalCharge` **ở cuối enum**; sửa check constraint ở `Data/Configurations/InvoiceAdjustmentConfiguration.cs`. Tạo migration mới trong `backend/src/RoomManagement.Data/Migrations/`; không sửa `InitialSchema.Views.cs`.

```csharp
public enum InvoiceAdjustmentType
{
    Discount = 1,
    Correction,
    WriteOff,
    AdditionalCharge
}
```

Trong bản sao **đầy đủ** của SQL `vwInvoiceBalances` ở migration mới, thay riêng phép tổng adjustment bằng biểu thức sau; giữ nguyên các cột, phần Payment/Refund/Deposit và quy tắc riêng cho hóa đơn cọc:

```sql
SELECT SUM(CASE
    WHEN a.AdjustmentType = N'AdditionalCharge'
        THEN -CONVERT(decimal(38,2), a.Amount)
    ELSE CONVERT(decimal(38,2), a.Amount)
END) AS TotalAdjustments
FROM dbo.InvoiceAdjustments AS a
WHERE a.InvoiceId = i.Id AND a.VoidedAt IS NULL
```

Migration cần drop check cũ, add check mới và chạy `ALTER VIEW` qua `migrationBuilder.Sql("EXEC(N'...')")` vì `ALTER VIEW` phải là câu lệnh đầu batch. `Down` phục hồi **view cũ trước**, rồi mới check cũ. Nếu database đã có dòng `AdditionalCharge`, rollback về check cũ sẽ thất bại; cần đối soát dữ liệu trước khi rollback. Các adjustment cũ tiếp tục giảm tiền, nên không cần backfill.

### Mẫu E — Bước 7: kiểm tra giảm tiền và idempotency

**File đích:** `backend/src/RoomManagement.Data/InvoiceAdjustmentStore.cs` và `Business/Services/InvoiceAdjustmentService.cs` (tạo mới). Ví dụ dưới là đoạn bên trong transaction **sau khi đã khóa invoice và kiểm tra landlord sở hữu hóa đơn `Monthly/Issued`**.

```csharp
var previous = await _db.InvoiceAdjustments.AsNoTracking()
    .SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
if (previous is not null)
{
    if (previous.InvoiceId != invoiceId || previous.AdjustmentType != type ||
        previous.Amount != amount || previous.Reason != reason)
        throw new BusinessException(BusinessErrorType.Conflict,
            "ADJUSTMENT_KEY_REUSED", "Mã yêu cầu đã dùng cho nội dung khác.");
    return previous;
}

var balance = await _db.InvoiceBalances.AsNoTracking()
    .SingleAsync(x => x.InvoiceId == invoiceId, ct);
if (type != InvoiceAdjustmentType.AdditionalCharge &&
    balance.EffectiveAmount - amount < 0)
    throw new BusinessException(BusinessErrorType.Validation,
        "ADJUSTMENT_EXCEEDS_TOTAL", "Khoản giảm vượt tiền phải thu hiệu lực.");

_db.InvoiceAdjustments.Add(new InvoiceAdjustment
{
    Id = Guid.NewGuid(), InvoiceId = invoiceId, AdjustmentType = type,
    Amount = amount, Reason = reason, IdempotencyKey = key,
    CreatedByUserId = ownerId, CreatedAt = DateTime.UtcNow
});
await _db.SaveChangesAsync(ct);
```

Service phải chuẩn hóa/kiểm tra `key`, `reason`, `amount > 0` **trước** đoạn này. Sau `SaveChangesAsync`, đọc lại view để trả số dư mới. Với void, ghi `VoidedAt`, `VoidReason`, `VoidedByUserId`; không xóa adjustment. Chạy toàn bộ create/void/payment/refund dưới cùng quy tắc khóa `invoice:{id}` để hai lệnh giảm đồng thời không vượt tổng. Unique index trên `IdempotencyKey` vẫn cần để chặn race/retry cuối cùng.

### Mẫu F — Bước 8: xác minh payment theo số thực nhận

**File đích:** `backend/src/RoomManagement.Data/PaymentStore.cs` và `Business/Services/PaymentService.cs` (tạo mới). Đoạn sau nằm trong transaction, sau khi đã khóa invoice và xác nhận payment thuộc hóa đơn đó; `actorId` là landlord có quyền.

```csharp
if (payment.Status != PaymentStatus.Pending || actualAmount <= 0)
    throw new BusinessException(BusinessErrorType.InvalidOperation,
        "PAYMENT_INVALID_REVIEW", "Không thể xác minh khoản thanh toán này.");

var now = DateTime.UtcNow;
var oldStatus = payment.Status;
var oldVerified = payment.VerifiedAmount;
payment.Status = PaymentStatus.Success;
payment.VerifiedAmount = actualAmount; // Ghi đúng tiền đã nhận, kể cả vượt số dư.
payment.PaidAt ??= now;
payment.VerifiedAt = now;
payment.VerifiedByUserId = actorId;
_db.PaymentReviewEvents.Add(new PaymentReviewEvent
{
    Id = Guid.NewGuid(), PaymentId = payment.Id,
    PreviousStatus = oldStatus, NewStatus = payment.Status,
    PreviousVerifiedAmount = oldVerified,
    NewVerifiedAmount = payment.VerifiedAmount,
    Reason = reviewReason, ReviewedByUserId = actorId, ReviewedAt = now
});
await _db.SaveChangesAsync(ct);
```

`PaymentConfiguration.cs` yêu cầu `Success` có `VerifiedAmount > 0`, `PaidAt`, `VerifiedAt`, `VerifiedByUserId`. Với `Failed/Cancelled`, các trường xác minh phải là `null`; viết nhánh riêng và test check constraint. `IdempotencyKey` hiện nằm trên lệnh **tạo Payment**; lệnh review cần quy tắc riêng khi người dùng gửi lại, ví dụ trả kết quả cũ nếu đã review giống hệt, trả 409 nếu khác. Không chuyển một Payment `Success` thành `Failed` để sửa tiền đã nhận.
