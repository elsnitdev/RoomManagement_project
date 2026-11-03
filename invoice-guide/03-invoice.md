# Chương 3 — Tính tiền, tạo và phát hành Invoice

> Đã triển khai. Xem [danh sách file, API và các quy tắc hiện dùng](implementation-status-02-03.md).

**Đích:** một hợp đồng đang hiệu lực tạo được một hóa đơn tháng `Draft`, xem trước các dòng tiền, phát hành rồi đọc số dư. Chỉ bắt đầu khi [Chương 1](01-contract.md) và [Chương 2](02-billing-inputs.md) cung cấp dữ liệu thật. Domain entity/configuration đã có; **chưa có** `InvoiceService`, `IInvoiceStore`, `InvoiceStore` hay `InvoicesController`.

## 1. `Business/DTOs/InvoiceModels.cs` — định nghĩa dữ liệu đầu ra trước

**Tạo file** `backend/src/RoomManagement.Business/DTOs/InvoiceModels.cs`. Đặt các record sau làm hợp đồng giữa service, store và controller. Trả `decimal`/`DateOnly`, để API serialize; không dùng tiền dạng `double` hoặc chuỗi đã format.

```csharp
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record MonthlyInvoiceInput(Guid ContractId, int Year, int Month);
public sealed record InvoiceFilter(int Page, int PageSize, Guid? RoomId,
    int? Year, int? Month, InvoiceStatus? Status);
public sealed record CalculatedItem(InvoiceItemType Type, string Description,
    decimal Quantity, decimal UnitPrice, decimal Amount, Guid? ReferenceId);
public sealed record InvoicePreviewResult(Guid ContractId, Guid RoomId,
    int Year, int Month, DateOnly DueDate,
    IReadOnlyList<CalculatedItem> Items, decimal TotalAmount);
public sealed record InvoiceDetailResult(
    Guid Id, string Code, InvoiceType Type, InvoiceStatus Status,
    DateOnly DueDate, decimal BaseTotal, decimal EffectiveTotal,
    decimal TotalPaid, decimal TotalRefunded, decimal RemainingAmount,
    IReadOnlyList<CalculatedItem> Items);
```

`InvoicePreviewResult` là thứ người dùng duyệt trước khi tạo. `InvoiceDetailResult` là dữ liệu đọc từ hóa đơn **và** `vwInvoiceBalances`, không được trả số dư tự tính từ frontend. Nếu cần thông tin tên phòng/người thuê, thêm field vào result/projection sau khi truy vấn đã đúng quyền.

## 2. `Business/Services/MonthlyInvoiceCalculator.cs` — chỉ tính, không truy vấn DB

**Tạo file** và một test file `backend/tests/RoomManagement.Business.Tests/MonthlyInvoiceCalculatorTests.cs`. Calculator nhận `Contract`, `MeterReading`, `UtilityRate`, dịch vụ đã được service chọn đúng phòng/kỳ; không tự mở `DbContext`. Một đoạn lõi:

```csharp
private static decimal Money(decimal n) =>
    decimal.Round(n, 2, MidpointRounding.AwayFromZero);

private static CalculatedItem RentItem(Contract contract, int year, int month)
{
    var from = new DateOnly(year, month, 1);
    var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
    var first = contract.StartDate > from ? contract.StartDate : from;
    var last = contract.EndDate is { } end && end < to ? end : to;
    var days = last.DayNumber - first.DayNumber + 1;
    if (days <= 0) throw new ArgumentException("Hợp đồng không thuộc kỳ.");
    var amount = Money(contract.MonthlyRent * days / DateTime.DaysInMonth(year, month));
    return new CalculatedItem(InvoiceItemType.Rent, "Tiền phòng", 1, amount, amount, null);
}

private static CalculatedItem? UtilityItem(
    InvoiceItemType type, MeterReading reading, UtilityRate rate)
{
    var used = reading.CurrentValue - reading.PreviousValue;
    if (used < 0) throw new ArgumentException("Chỉ số giảm.");
    if (used == 0) return null; // DB không chấp nhận InvoiceItem.Quantity = 0.
    return new CalculatedItem(type, type == InvoiceItemType.Electricity ? "Điện" : "Nước",
        used, rate.UnitPrice, Money(used * rate.UnitPrice), reading.Id);
}
```

Hàm public `Calculate(...)` gọi `RentItem`, `UtilityItem` cho điện/nước, tạo item dịch vụ, bỏ các item tiện ích có lượng bằng 0, rồi `TotalAmount = items.Sum(x => x.Amount)`. Kiểm tra `TotalAmount >= 0`. **Chốt trước khi viết test:** giá đổi giữa kỳ, phí dịch vụ theo ngày và tháng chứa hạn thanh toán; plan đang đề xuất giá đầu kỳ và tiền thuê tháng lẻ theo ngày. Test hợp đồng bắt đầu 16/04/2026, rent 3.000.000đ → tiền thuê 1.500.000đ.

## 3. `Business/Interfaces/IInvoiceStore.cs` — những việc Data phải làm

**Tạo file** `backend/src/RoomManagement.Business/Interfaces/IInvoiceStore.cs`. Đừng đặt `DbContext` trong interface. Các method tối thiểu:

```csharp
public interface IInvoiceStore
{
    Task<Contract?> GetOwnedContractAsync(Guid propertyId, Guid contractId,
        string ownerId, CancellationToken ct);
    Task<Invoice?> GetOwnedAsync(Guid invoiceId, string ownerId, CancellationToken ct);
    Task<(IReadOnlyList<Invoice> Items, long Total)> ListOwnedAsync(
        Guid propertyId, string ownerId, InvoiceFilter filter, CancellationToken ct);
    Task<Invoice> CreateDraftAsync(Invoice invoice,
        IReadOnlyList<InvoiceItem> items, string ownerId, CancellationToken ct);
    Task<Invoice> IssueAsync(Guid invoiceId, string ownerId, CancellationToken ct);
    Task<Invoice> CancelAsync(Guid invoiceId, string ownerId, CancellationToken ct);
    Task<InvoiceDetailResult?> GetDetailAsync(Guid invoiceId, string ownerId, CancellationToken ct);
}
```

Thêm `using RoomManagement.Business.DTOs; using RoomManagement.Domain.Entities;` ở đầu file. `GetOwnedContractAsync` lấy hợp đồng qua Room/Property; service yêu cầu `Active` khi tạo hóa đơn tháng, nhưng có thể cho `Draft/Active` khi tạo hóa đơn cọc theo chính sách thu cọc trước khi ở. `GetDetailAsync` là projection kết hợp `Invoices`, `InvoiceItems`, `InvoiceBalances`; không cho tenant dùng method landlord này. Khi làm tenant endpoint, tạo method scope theo `TenantProfile.UserId → ContractTenants` và chỉ `Issued`.

## 4. `Data/InvoiceStore.cs` — cụ thể từng method phải ghi gì

**Tạo file** `backend/src/RoomManagement.Data/InvoiceStore.cs`, class `InvoiceStore : IInvoiceStore`, inject `RoomManagementDbContext`. Đây là phần thường dễ bị viết thiếu:

| Method | Nội dung cần viết |
| --- | --- |
| `GetOwnedContractAsync` | Join `Contracts → Rooms → Properties`, filter contract ID, property ID và `OwnerUserId`; service quyết định trạng thái hợp đồng hợp lệ theo loại hóa đơn. |
| `GetOwnedAsync` | Join `Invoices → Rooms → Properties`, filter invoice ID và `OwnerUserId`; không lấy invoice của khu khác rồi mới kiểm sau. |
| `ListOwnedAsync` | Query trên join sở hữu; filter `RoomId`, kỳ, `Status`; `LongCountAsync` trước `Skip/Take`; order ổn định theo `IssueDate` và ID. |
| `CreateDraftAsync` | Kiểm contract/room/property cùng chủ; transaction lưu `Invoice` và mọi `InvoiceItem`; tổng header đúng tổng item; bắt SQL unique 2601/2627 trả 409. |
| `IssueAsync` | Transaction, khóa invoice, tải trạng thái hiện tại; bắt buộc `Draft`, có item, tổng item khớp header, ngày hợp lệ; đổi `Issued`. |
| `CancelAsync` | Transaction, khóa invoice; chỉ hủy nếu không có Payment thành công, adjustment, refund hoặc cọc đã phân bổ; không xóa dữ liệu. |
| `GetDetailAsync` | Join invoice với view `InvoiceBalances`, lấy item; trả tổng gốc và số dư từ view. Số dư âm phải giữ nguyên. |

Code lõi cho query sở hữu và transaction tạo draft:

```csharp
private IQueryable<Invoice> OwnedInvoices(string ownerId) =>
    from invoice in _db.Invoices
    join room in _db.Rooms on invoice.RoomId equals room.Id
    join property in _db.Properties on room.PropertyId equals property.Id
    where property.OwnerUserId == ownerId
    select invoice;

public async Task<Invoice> CreateDraftAsync(Invoice invoice,
    IReadOnlyList<InvoiceItem> items, string ownerId, CancellationToken ct)
{
    if (items.Count == 0 || invoice.TotalAmount != items.Sum(x => x.Amount) ||
        items.Any(x => x.InvoiceId != invoice.Id))
        throw new BusinessException(BusinessErrorType.Validation,
            "INVOICE_TOTAL_MISMATCH", "Tổng hóa đơn không khớp các dòng tiền.");
    await using var tx = await _db.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.Serializable, ct);
    var owned = await (from c in _db.Contracts
        join r in _db.Rooms on c.RoomId equals r.Id
        join p in _db.Properties on r.PropertyId equals p.Id
        where c.Id == invoice.ContractId && r.Id == invoice.RoomId &&
              p.OwnerUserId == ownerId &&
              (invoice.InvoiceType == InvoiceType.Monthly
                  ? c.Status == ContractStatus.Active
                  : c.Status == ContractStatus.Draft || c.Status == ContractStatus.Active)
        select c.Id).AnyAsync(ct);
    if (!owned) throw new BusinessException(BusinessErrorType.NotFound,
        "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng hợp lệ.");
    _db.Invoices.Add(invoice);
    _db.InvoiceItems.AddRange(items);
    try { await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
    catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
    {
        throw new BusinessException(BusinessErrorType.Conflict,
            "INVOICE_ALREADY_EXISTS", "Hóa đơn kỳ này hoặc mã hóa đơn đã tồn tại.", ex);
    }
    return invoice;
}
```

Để dùng đoạn này cần `using Microsoft.Data.SqlClient; using Microsoft.EntityFrameworkCore;` và các namespace Business/Domain. `IssueAsync` và các thao tác tiền về sau phải dùng **cùng một cơ chế khóa theo invoice**. Tạo file helper dưới đây và gọi nó sau `BeginTransactionAsync`, trước khi đọc trạng thái hoặc view.

**Tạo file** `backend/src/RoomManagement.Data/InvoiceTransactionLock.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RoomManagement.Business.Errors;

namespace RoomManagement.Data;

internal static class InvoiceTransactionLock
{
    public static async Task AcquireAsync(
        RoomManagementDbContext db, Guid invoiceId, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Phải mở transaction trước khi khóa invoice.");
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            SELECT @result;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = $"invoice:{invoiceId:N}";
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (result < 0)
            throw new BusinessException(BusinessErrorType.Conflict,
                "INVOICE_BUSY", "Hóa đơn đang được xử lý. Vui lòng thử lại.");
    }
}
```

Trong `IssueAsync`, `CancelAsync`, điều chỉnh, review payment và hoàn tiền: `await using var tx = await _db.Database.BeginTransactionAsync(ct); await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);` rồi **mới** query trạng thái/số dư và ghi. `RoomBatchStore.cs` đã dùng cùng kỹ thuật. Với phân bổ cọc cần thêm khóa theo hợp đồng và thứ tự khóa cố định để tránh deadlock.

## 5. `Business/Services/InvoiceService.cs` — orchestration, không EF

**Tạo file**, inject `ICurrentLandlord`, `ILandlordStore`, `IInvoiceStore`, các store đọc giá/chỉ số/dịch vụ và `MonthlyInvoiceCalculator`. Ghi các method:

1. `PreviewMonthlyAsync(propertyId, input, ct)`: kiểm landlord; gọi `GetOwnedContractAsync` và yêu cầu `Status == Active`; lấy chỉ số/rate/dịch vụ đúng phòng/kỳ; nếu thiếu trả `INVOICE_INPUT_INCOMPLETE`; calculator trả preview. **Không ghi DB**.
2. `CreateMonthlyAsync(...)`: gọi lại `PreviewMonthlyAsync` trên server; tạo `Invoice` với `Id = Guid.NewGuid()`, mã server sinh, `InvoiceType.Monthly`, `BillingYear/Month`, `Status.Draft`; đổi từng `CalculatedItem` thành `InvoiceItem` có `InvoiceId`; gọi `CreateDraftAsync`. Không lấy `TotalAmount` từ request.
3. `CreateDepositAsync(contractId, ...)`: lấy `Contract.DepositAmount`, tạo một item loại `Deposit`, dùng unique index cọc. Phải kiểm `DepositAmount > 0` theo chính sách.
4. `IssueAsync`, `CancelAsync`, `ListAsync`, `GetDetailAsync`: xác nhận owner rồi gọi store; không tự tính số dư từ Payment ở đây.

Ví dụ đoạn chuyển preview thành entity trong `CreateMonthlyAsync`:

```csharp
var preview = await PreviewMonthlyAsync(propertyId, input, ct);
var invoiceId = Guid.NewGuid();
var invoice = new Invoice
{
    Id = invoiceId, ContractId = preview.ContractId, RoomId = preview.RoomId,
    InvoiceCode = GenerateCode(preview), InvoiceType = InvoiceType.Monthly,
    BillingYear = (short)preview.Year, BillingMonth = (byte)preview.Month,
    IssueDate = DateOnly.FromDateTime(DateTime.UtcNow), DueDate = preview.DueDate,
    TotalAmount = preview.TotalAmount, Status = InvoiceStatus.Draft,
    CreatedAt = DateTime.UtcNow
};
var items = preview.Items.Select(x => new InvoiceItem
{
    Id = Guid.NewGuid(), InvoiceId = invoiceId, ItemType = x.Type,
    Description = x.Description, Quantity = x.Quantity,
    UnitPrice = x.UnitPrice, Amount = x.Amount, ReferenceId = x.ReferenceId,
    CreatedAt = DateTime.UtcNow
}).ToList();
return await _store.CreateDraftAsync(invoice, items, ownerId, ct);
```

`GenerateCode` cần mã duy nhất; có thể dùng ID rút gọn kèm kỳ, nhưng unique index vẫn bắt collision. `IssueDate` là ngày phát hành nghiệp vụ Việt Nam, nên trong bản hoàn chỉnh dùng nguồn thời gian/múi giờ đã chốt thay vì lấy UTC date như đoạn minh họa. Cũng cần quyết định preview hết hạn thế nào: create luôn tính lại để không tin vào preview cũ.

## 6. `API/Contracts/Invoices` và `InvoicesController.cs` — JSON/API

**Tạo** `backend/src/RoomManagement.API/Contracts/Invoices/InvoiceRequests.cs` với `MonthlyInvoiceRequest(Guid ContractId, int Year, int Month)` và `ToInput()` trả `MonthlyInvoiceInput`; thêm request tạo cọc nếu route dùng body. **Tạo** `InvoiceResponse.cs` với `InvoicePreviewResponse`, `InvoiceDetailResponse`, `InvoiceItemResponse` để map Business result sang JSON. Dùng `[JsonRequired]` cho trường bắt buộc và `[Range]` cho năm/tháng; service vẫn validate.

**Tạo** `backend/src/RoomManagement.API/Controllers/InvoicesController.cs`, `[Authorize(Roles = UserRoles.Landlord)]`. Ghi route cụ thể:

| Route | Gọi method service | Trả gì |
| --- | --- | --- |
| `POST /api/properties/{propertyId}/invoices/monthly/preview` | `PreviewMonthlyAsync` | 200 + item/tổng, không ghi DB. |
| `POST /api/properties/{propertyId}/invoices/monthly` | `CreateMonthlyAsync` | 201 + ID/code draft. |
| `POST /api/contracts/{contractId}/invoices/deposit` | `CreateDepositAsync` | 201 + draft cọc. |
| `GET /api/properties/{propertyId}/invoices` | `ListAsync` | `PageResponse<T>`. |
| `GET /api/invoices/{id}` | `GetDetailAsync` | `ApiResponse<T>`. |
| `POST /api/invoices/{id}/issue` | `IssueAsync` | 200 + hóa đơn `Issued`. |
| `POST /api/invoices/{id}/cancel` | `CancelAsync` | 200 + hóa đơn `Cancelled`. |

Vì route có ba prefix, dùng hai controller nếu cần rõ: `PropertyInvoicesController` cho list/preview/create và `InvoicesController` cho detail/issue/cancel; route cọc có thể nằm trong `ContractInvoicesController`. **Không ép tất cả route vào một `[Route]` rồi tạo URL sai.** Nếu chọn hai/ba controller, giữ mọi request/response trong cùng thư mục `API/Contracts/Invoices`.

## 7. DI và test

Thêm `services.AddScoped<IInvoiceStore, InvoiceStore>();` trong `backend/src/RoomManagement.Data/ServiceCollectionExtensions.cs`; thêm `builder.Services.AddScoped<InvoiceService>();` và calculator (nếu không phải static) trong `backend/src/RoomManagement.API/Program.cs`.

Test unit: tiền thuê tháng lẻ, năm nhuận, làm tròn, điện/nước 0 và sai chỉ số. Test SQL trong `backend/tests/RoomManagement.Tests/InvoiceStoreTests.cs`: transaction không tạo header mồ côi, trùng kỳ trả 409, `Issue` chỉ từ Draft, tổng item khớp, landlord khác không đọc, tenant không thấy Draft. Dùng `SqlServerDatabase.cs` với `ROOMMANAGEMENT_TEST_SQLSERVER` dành riêng cho test. **Xong khi** preview và create cùng kỳ cho cùng tổng; draft không tính là công nợ; issued có số dư từ view. Sang [Chương 4](04-adjustments.md).
