# Chương 5 — Payment, Refund và phân bổ cọc

**Đích:** số tiền trên hóa đơn phản ánh **tiền thực nhận**, khoản hoàn thực trả và cọc đã dùng. Các entity, EF configuration và hai view đã có; controller/service/store của ba nghiệp vụ chưa có. Làm Payment trước, rồi Refund, cuối cùng DepositApplication. Tất cả thao tác ghi trên cùng hóa đơn phải dùng **cùng khóa invoice trong transaction** được bắt đầu ở [Chương 3](03-invoice.md).

## 1. Payment — từng file phải ghi gì

| File cần tạo | Nội dung cụ thể |
| --- | --- |
| `backend/src/RoomManagement.Business/DTOs/PaymentModels.cs` | `DeclarePaymentInput(Amount, Method, PaidAt, TransactionCode, IdempotencyKey, Note)`, `ReviewPaymentInput(Status, VerifiedAmount, Reason)`, `PaymentResult`. Không nhận `VerifiedByUserId` từ client. |
| `backend/src/RoomManagement.Business/Interfaces/IPaymentStore.cs` | `CreatePendingAsync(invoiceId, tenantOrLandlordId, input, ct)`, `ReviewAsync(paymentId, ownerId, input, ct)`, `ListByInvoiceAsync(invoiceId, actorId, ct)`. Store phải chứng minh actor có quyền trên hóa đơn. |
| `backend/src/RoomManagement.Business/Services/PaymentService.cs` | Kiểm số khai `> 0`, phương thức hợp lệ, key, quyền và trạng thái invoice `Issued`; review chỉ cho landlord sở hữu, chỉ từ `Pending`. |
| `backend/src/RoomManagement.Data/PaymentStore.cs` | EF tạo `Payment(Pending)`, lookup key để retry, unique conflict; review transaction + khóa invoice + cập nhật Payment + thêm `PaymentReviewEvent`; đọc lại `InvoiceBalances`. |
| `backend/src/RoomManagement.API/Contracts/Payments/PaymentRequests.cs`, `PaymentResponse.cs` | Request có validation HTTP, `ToInput()`; response không lộ dữ liệu ngân hàng nhạy cảm. |
| `backend/src/RoomManagement.API/Controllers/PaymentsController.cs` | `POST /api/invoices/{id}/payments`, `GET /api/invoices/{id}/payments`, `POST /api/payments/{paymentId}/review`. Role/phạm vi sở hữu ở service/store, không chỉ controller. |

Khi khai đã chuyển tiền, tạo row:

```csharp
var payment = new Payment
{
    Id = Guid.NewGuid(), InvoiceId = invoiceId,
    ClaimedAmount = input.Amount, PaymentMethod = input.Method,
    Status = PaymentStatus.Pending, PaidAt = input.PaidAt,
    TransactionCode = input.TransactionCode,
    IdempotencyKey = input.IdempotencyKey,
    Note = input.Note, CreatedAt = DateTime.UtcNow
};
```

`Pending` **không giảm nợ** trong `vwInvoiceBalances`. Khi review thành công, lưu số chủ trọ xác minh được, kể cả ít hơn/lớn hơn khách khai. Phần code của `PaymentStore.ReviewAsync` sau khi khóa invoice và tải payment có tracking:

```csharp
if (payment.Status != PaymentStatus.Pending)
    throw new BusinessException(BusinessErrorType.Conflict,
        "PAYMENT_ALREADY_REVIEWED", "Khoản thanh toán đã được xử lý.");
var oldStatus = payment.Status;
var oldAmount = payment.VerifiedAmount;
var now = DateTime.UtcNow;
payment.Status = PaymentStatus.Success;
payment.VerifiedAmount = input.VerifiedAmount;
payment.PaidAt ??= now;
payment.VerifiedAt = now;
payment.VerifiedByUserId = ownerId;
_db.PaymentReviewEvents.Add(new PaymentReviewEvent
{
    Id = Guid.NewGuid(), PaymentId = payment.Id,
    PreviousStatus = oldStatus, NewStatus = payment.Status,
    PreviousVerifiedAmount = oldAmount, NewVerifiedAmount = payment.VerifiedAmount,
    Reason = input.Reason, ReviewedByUserId = ownerId, ReviewedAt = now
});
await _db.SaveChangesAsync(ct);
```

Đây là **nhánh Success**; service phải bắt buộc `VerifiedAmount > 0`. Nhánh `Failed/Cancelled` phải để `VerifiedAmount`, `VerifiedAt`, `VerifiedByUserId` bằng `null`, theo `PaymentConfiguration.cs`. `Payment.IdempotencyKey` chống tạo lặp; lệnh review cũng cần quy tắc retry riêng: request review giống hệt trên bản ghi đã review trả kết quả cũ, khác thì 409. Không đổi một `Success` thành `Failed` để xóa tiền đã nhận. Nếu ngân hàng nhận vượt số còn nợ, vẫn lưu đúng `VerifiedAmount`; `RemainingAmount` âm là tiền cần đối soát.

## 2. Refund — từng file phải ghi gì

| File cần tạo | Nội dung cụ thể |
| --- | --- |
| `Business/DTOs/RefundModels.cs` | `CreateRefundInput(Amount, Reason, IdempotencyKey, TransactionCode)` và `RefundResult(Status, Amount, RefundedAt, InvoiceId)`. |
| `Business/Interfaces/IRefundStore.cs` | `CreatePendingAsync(invoiceId, ownerId, input, ct)`; `ConfirmAsync(refundId, ownerId, ct)`; `ListByInvoiceAsync`. |
| `Business/Services/RefundService.cs` | Kiểm amount/reason/key, chỉ landlord sở hữu được hoàn, invoice `Issued`; phân biệt hóa đơn thường và cọc. |
| `Data/RefundStore.cs` | EF tạo `Refund(Pending)`, key retry; khi xác nhận khóa invoice, đọc balance/cọc hiện tại, chặn hoàn vượt, đổi `Success` và gán `RefundedAt`. |
| `API/Contracts/Payments/RefundRequests.cs`, `API/Controllers/RefundsController.cs` | `POST /api/invoices/{id}/refunds`, `POST /api/refunds/{refundId}/confirm`, `GET /api/invoices/{id}/refunds`. |

Trong `ConfirmAsync`, với hóa đơn **không phải cọc**, điều kiện tối thiểu cho hoàn do giảm/trả dư là `refund.Amount <= max(0, -balance.RemainingAmount)` ngay trước commit. Với hóa đơn `Deposit`, dùng `ContractDepositBalances.DepositHeld`, không dùng `RemainingAmount` của hóa đơn cọc vì hoàn cọc không mở lại nợ thu cọc. Cả hai điều kiện phải được kiểm lại **sau khi khóa**, không dựa vào số UI gửi từ vài giây trước. `RefundConfiguration.cs` yêu cầu `RefundedAt` khác null khi `Status = Success`.

## 3. DepositApplication — từng file phải ghi gì

| File cần tạo | Nội dung cụ thể |
| --- | --- |
| `Business/DTOs/DepositApplicationModels.cs` | `ApplyDepositInput(TargetInvoiceId, Amount, Reason, IdempotencyKey)` và result có số cọc còn giữ. |
| `Business/Interfaces/IDepositApplicationStore.cs` | `ApplyAsync(contractId, ownerId, input, ct)` và `ListByContractAsync`. |
| `Business/Services/DepositApplicationService.cs` | Kiểm hợp đồng, quyền, số tiền/lý do/key; hóa đơn đích phải `Issued`, khác `Deposit`, cùng hợp đồng. |
| `Data/DepositApplicationStore.cs` | Transaction khóa hợp đồng/cọc và hóa đơn đích theo **thứ tự cố định**; đọc `ContractDepositBalances.DepositHeld`, chặn phân bổ vượt; insert row và đọc lại cả hai view. |
| `API/Contracts/Invoices/DepositApplicationRequests.cs`, `API/Controllers/DepositApplicationsController.cs` | `POST /api/contracts/{contractId}/deposit-applications`, `GET` danh sách đã phân bổ. |

Row cần lưu `ContractId`, `TargetInvoiceId`, `Amount`, `Reason`, `IdempotencyKey`, `AppliedByUserId` từ token và `AppliedAt` từ server. `DepositApplicationConfiguration.cs` đã có FK ghép để hóa đơn đích thuộc đúng hợp đồng, nhưng service vẫn phải kiểm `InvoiceType != Deposit`, `Status == Issued` và số cọc còn đủ. Sau commit: `DepositHeld` giảm, `RemainingAmount` của hóa đơn đích cũng giảm.

## 4. DI, test và checkpoint

Trong `backend/src/RoomManagement.Data/ServiceCollectionExtensions.cs`, đăng ký `IPaymentStore`, `IRefundStore`, `IDepositApplicationStore` với các store tương ứng. Trong `backend/src/RoomManagement.API/Program.cs`, đăng ký ba service cụ thể. Không đăng ký hai class khác nhau cho cùng interface rồi vô tình ghi đè.

Tạo `backend/tests/RoomManagement.Tests/PaymentTests.cs`, `RefundTests.cs`, `DepositApplicationTests.cs`. Test `Pending` không giảm nợ; Success 900.000đ khi khách khai 1.000.000đ chỉ giảm nợ 900.000đ; hai khoản tiền thật đến cùng lúc tạo dư thì ghi đủ tiền và số dư âm; giảm tiền rồi Refund đưa số dư về 0; hai Refund đồng thời không hoàn vượt; cọc thu 1,2 triệu + 0,8 triệu, bù 0,5 triệu và hoàn 1,5 triệu thì `DepositHeld = 0`. Test trên SQL Server bằng `ROOMMANAGEMENT_TEST_SQLSERVER`, không dùng SQLite để kết luận về view/filtered index/khóa.

**Kết thúc chuỗi:** `dotnet test backend/RoomManagement.sln` chạy; test SQL không bị skip; tenant chỉ thấy invoice `Issued` của mình; landlord khác không thể tạo/duyệt/hoàn ở khu trọ người khác. Các khoản đã nhận và đã hoàn luôn có lịch sử, không bị xóa để sửa số dư.
