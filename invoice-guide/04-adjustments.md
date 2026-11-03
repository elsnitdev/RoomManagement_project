# Chương 4 — Cộng/trừ tiền sau khi hóa đơn đã phát hành

**Đích:** giữ `InvoiceItems`/`Invoices.TotalAmount` gốc, ghi phụ thu hoặc giảm trừ thành một giao dịch có lý do. Hóa đơn đã trả đủ vẫn có thể phát sinh nợ mới hoặc tiền phải hoàn. Chương này theo mục 4 của [kế hoạch](../plans/invoices-backend-implementation-plan.md). Chỉ làm cho `Monthly/Issued` trong MVP; không chỉnh hóa đơn cọc.

## 1. `Domain/Enums/InvoiceAdjustmentType.cs` — thêm loại cộng tiền

**Sửa** `backend/src/RoomManagement.Domain/Enums/InvoiceAdjustmentType.cs`: thêm `AdditionalCharge` ở **cuối** để không đổi giá trị số của enum cũ.

```csharp
public enum InvoiceAdjustmentType
{
    Discount = 1,
    Correction,
    WriteOff,
    AdditionalCharge
}
```

Ý nghĩa: `AdditionalCharge` cộng tiền; ba loại còn lại giảm tiền. `Amount` trong entity luôn dương. Cộng vì tính thiếu tiền cũng dùng `AdditionalCharge`, giải thích cụ thể trong `Reason`.

## 2. `Data/Configurations/InvoiceAdjustmentConfiguration.cs` — cập nhật DB check

**Sửa** biểu thức `CK_InvoiceAdjustments_Type` để nhận `N'AdditionalCharge'`. Giữ nguyên `CK_InvoiceAdjustments_Amount (Amount > 0)`, FK, unique `IdempotencyKey` và ba trường void. Không tạo cột `Direction` mới vì loại điều chỉnh đã xác định chiều tiền.

```csharp
table.HasCheckConstraint("CK_InvoiceAdjustments_Type",
    "(AdjustmentType IN (N'Discount', N'Correction', N'WriteOff', N'AdditionalCharge'))");
```

## 3. Migration mới — thay view số dư, không sửa migration cũ

**Tạo** bằng `dotnet ef migrations add AddInvoiceAdditionalCharge --project backend/src/RoomManagement.Data --startup-project backend/src/RoomManagement.API`. File migration sinh ra nằm trong `backend/src/RoomManagement.Data/Migrations/`. Kiểm tra `Up` có drop/add `CK_InvoiceAdjustments_Type`; EF **không tự phát hiện nội dung SQL view**, nên bạn thêm lệnh cập nhật view thủ công vào migration mới.

Đọc toàn bộ SQL hiện có ở `backend/src/RoomManagement.Data/Migrations/InitialSchema.Views.cs`. Sao chép định nghĩa `vwInvoiceBalances` sang migration mới, đổi `CREATE VIEW` thành `ALTER VIEW`, rồi thay **duy nhất** phép tính tổng adjustment:

```sql
OUTER APPLY (
    SELECT SUM(CASE
        WHEN a.AdjustmentType = N'AdditionalCharge'
            THEN -CONVERT(decimal(38,2), a.Amount)
        ELSE CONVERT(decimal(38,2), a.Amount)
    END) AS TotalAdjustments
    FROM dbo.InvoiceAdjustments AS a
    WHERE a.InvoiceId = i.Id AND a.VoidedAt IS NULL
) AS a
```

Giữ các cột view `TotalAdjustments`, `EffectiveAmount`, `RemainingAmount` và quy tắc Deposit/Refund. Khi `AdditionalCharge` 200.000đ, `TotalAdjustments = −200.000đ`, nên `EffectiveAmount = TotalAmount − (−200.000đ)`. Dòng cũ vẫn giữ nghĩa giảm; **không backfill**. Chạy `ALTER VIEW` trong `migrationBuilder.Sql("EXEC(N'...')")` như `InitialSchema` vì SQL Server yêu cầu `ALTER VIEW` là câu lệnh đầu batch. `Down` phải trả view về nội dung cũ **trước** khi trả check constraint cũ; nếu đã có dòng AdditionalCharge, rollback cần xử lý dữ liệu trước, không thể cứ chạy Down trực tiếp.

## 4. `Business/DTOs/InvoiceAdjustmentModels.cs` — input/result

**Tạo** `backend/src/RoomManagement.Business/DTOs/InvoiceAdjustmentModels.cs`:

```csharp
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record AddInvoiceAdjustmentInput(
    InvoiceAdjustmentType Type, decimal Amount, string Reason, string IdempotencyKey);
public sealed record VoidInvoiceAdjustmentInput(string Reason);
public sealed record InvoiceAdjustmentResult(
    Guid Id, InvoiceAdjustmentType Type, decimal Amount, string Reason,
    DateTime CreatedAt, DateTime? VoidedAt,
    decimal EffectiveAmount, decimal RemainingAmount);
```

Input không cho client gửi `CreatedByUserId`, `CreatedAt`, `VoidedByUserId`; lấy chúng từ token và server clock. Result cần được map từ entity + `InvoiceBalanceRow` sau khi lưu.

## 5. `Business/Interfaces/IInvoiceAdjustmentStore.cs` — chữ ký bắt buộc

**Tạo** interface với ba thao tác nghiệp vụ, không phơi `DbContext`:

```csharp
public interface IInvoiceAdjustmentStore
{
    Task<InvoiceAdjustmentResult> AddAsync(Guid invoiceId, string ownerId,
        AddInvoiceAdjustmentInput input, CancellationToken ct);
    Task<IReadOnlyList<InvoiceAdjustment>> ListAsync(
        Guid invoiceId, string ownerId, CancellationToken ct);
    Task<InvoiceAdjustmentResult> VoidAsync(Guid invoiceId, Guid adjustmentId,
        string ownerId, string reason, CancellationToken ct);
}
```

Thêm `using` cho DTOs và `Domain.Entities`. `ListAsync` trả cả bản ghi đã void để đối chiếu, sắp theo `CreatedAt` và ID.

## 6. `Data/InvoiceAdjustmentStore.cs` — phần chính cần ghi

**Tạo file** inject `RoomManagementDbContext`. Đối với **cả Add và Void**: mở transaction; gọi `InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct)` của Chương 3; đọc `Invoice` bằng query sở hữu `Invoice → Room → Property.OwnerUserId`; yêu cầu `Type == Monthly && Status == Issued`; sau đó mới đọc/ghi adjustment và balance. Không tin `ownerId`/`invoiceId` tự gửi nếu query chưa xác nhận quan hệ.

Trong `AddAsync`, bắt đầu bằng:

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);
await InvoiceTransactionLock.AcquireAsync(_db, invoiceId, ct);
// Tiếp theo: query invoice có điều kiện Property.OwnerUserId == ownerId;
// nếu không tìm thấy, ném INVOICE_NOT_FOUND; kiểm Monthly/Issued.
```

Sau khi đã xác nhận quyền/trạng thái, ghi đoạn lõi:

```csharp
var old = await _db.InvoiceAdjustments.AsNoTracking()
    .SingleOrDefaultAsync(a => a.IdempotencyKey == input.IdempotencyKey, ct);
if (old is not null)
{
    if (old.InvoiceId != invoiceId || old.AdjustmentType != input.Type ||
        old.Amount != input.Amount || old.Reason != input.Reason)
        throw new BusinessException(BusinessErrorType.Conflict,
            "ADJUSTMENT_KEY_REUSED", "Mã yêu cầu đã dùng cho nội dung khác.");
    return await ResultForAsync(old, ct);
}

var balance = await _db.InvoiceBalances.AsNoTracking()
    .SingleAsync(b => b.InvoiceId == invoiceId, ct);
if (input.Type != InvoiceAdjustmentType.AdditionalCharge &&
    balance.EffectiveAmount - input.Amount < 0)
    throw new BusinessException(BusinessErrorType.Validation,
        "ADJUSTMENT_EXCEEDS_TOTAL", "Khoản giảm vượt tiền hóa đơn.");

var adjustment = new InvoiceAdjustment
{
    Id = Guid.NewGuid(), InvoiceId = invoiceId,
    AdjustmentType = input.Type, Amount = input.Amount,
    Reason = input.Reason, IdempotencyKey = input.IdempotencyKey,
    CreatedByUserId = ownerId, CreatedAt = DateTime.UtcNow
};
_db.InvoiceAdjustments.Add(adjustment);
await _db.SaveChangesAsync(ct);
var result = await ResultForAsync(adjustment, ct);
await tx.CommitAsync(ct);
return result;
```

`ResultForAsync` cũng phải được viết trong `InvoiceAdjustmentStore.cs`, để response luôn lấy số dư sau khi database đã tính lại view:

```csharp
private async Task<InvoiceAdjustmentResult> ResultForAsync(
    InvoiceAdjustment adjustment, CancellationToken ct)
{
    var balance = await _db.InvoiceBalances.AsNoTracking()
        .SingleAsync(b => b.InvoiceId == adjustment.InvoiceId, ct);
    return new InvoiceAdjustmentResult(
        adjustment.Id, adjustment.AdjustmentType, adjustment.Amount,
        adjustment.Reason, adjustment.CreatedAt, adjustment.VoidedAt,
        balance.EffectiveAmount, balance.RemainingAmount);
}
```

Ở `VoidAsync`: tìm adjustment đúng invoice, nếu đã void với cùng lý do thì trả bản ghi hiện tại, khác lý do trả 409; nếu chưa void, gán `VoidedAt = DateTime.UtcNow`, `VoidReason = reason`, `VoidedByUserId = ownerId`, kiểm `EffectiveAmount` sau void không âm, lưu và đọc lại view. Không xóa row. **Cả Add/Void cần guard ở service trước khi vào store**; unique index key là bảo vệ cuối khi retry đua nhau.

## 7. `Business/Services/InvoiceAdjustmentService.cs` — guard trước store

**Tạo file** inject `ICurrentLandlord`, `ILandlordStore`, `IInvoiceAdjustmentStore`. Ghi `AddAsync`, `ListAsync`, `VoidAsync`. Mỗi method lấy `ownerId` từ token và xác nhận landlord active; `AddAsync` kiểm `Amount > 0`, `Reason` dài 1–500 ký tự sau trim, `IdempotencyKey` dài 1–100, `Type` là enum hợp lệ. `VoidAsync` kiểm lý do 1–500 ký tự. Gọi store với `ownerId`, không cho client chọn người thực hiện.

```csharp
if (input.Amount <= 0 || string.IsNullOrWhiteSpace(input.Reason) ||
    input.Reason.Trim().Length > 500 ||
    string.IsNullOrWhiteSpace(input.IdempotencyKey) ||
    input.IdempotencyKey.Length > 100 ||
    !Enum.IsDefined(input.Type))
    throw new BusinessException(BusinessErrorType.Validation,
        "ADJUSTMENT_INVALID", "Khoản điều chỉnh không hợp lệ.");
```

## 8. `API/Contracts/Invoices/InvoiceAdjustmentRequests.cs` và controller

**Tạo request** `AddInvoiceAdjustmentRequest` có `AdjustmentType`, `Amount`, `Reason`, `IdempotencyKey`, `ToInput()`; `VoidInvoiceAdjustmentRequest` có `Reason`. Dùng `[JsonRequired]` và `[StringLength]`; validation nghiệp vụ vẫn ở service. Thêm vào controller route `POST /api/invoices/{id}/adjustments`, `GET /api/invoices/{id}/adjustments`, `POST /api/invoices/{id}/adjustments/{adjustmentId}/void`, tất cả Landlord, gọi `InvoiceAdjustmentService` và trả `ApiResponse`.

**Sửa** `backend/src/RoomManagement.Data/ServiceCollectionExtensions.cs`: đăng ký `IInvoiceAdjustmentStore → InvoiceAdjustmentStore`; **sửa** `backend/src/RoomManagement.API/Program.cs`: đăng ký `InvoiceAdjustmentService`. **Sửa** `InvoiceDetailResponse` của Chương 3 để trả `baseTotal`, tổng cộng, tổng trừ, `effectiveTotal`, `remainingAmount`, `amountDue = Max(0, remaining)`, `creditToRefund = Max(0, -remaining)`. Frontend không tự lấy `TotalAmount - paid`.

## 9. Test và checkpoint

Tạo `backend/tests/RoomManagement.Tests/InvoiceAdjustmentTests.cs`. Test migration từ DB cũ lên DB mới vẫn giữ số dư adjustment cũ; thêm phí 200.000đ vào hóa đơn 3.000.000đ tạo `EffectiveAmount = 3.200.000đ`; giảm 100.000đ còn 3.100.000đ; sau khi đã trả 3.000.000đ thì còn 100.000đ; giảm sau trả đủ cho số dư âm; void đảo tác động; hai giảm trừ đồng thời không làm tổng âm; key giống payload giống chỉ tạo một bản ghi; landlord khác không truy cập. Chỉ khi những test này chạy trên SQL Server mới sang [Chương 5](05-payments-refunds.md).
