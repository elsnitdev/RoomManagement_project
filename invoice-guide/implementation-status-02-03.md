# Đã triển khai chương 2 và chương 3

## Chương 2: dữ liệu đầu vào tính hóa đơn

| Phần | File chính | API |
| --- | --- | --- |
| Giá điện nước | `backend/src/RoomManagement.Business/Services/BillingInputService.cs`, `backend/src/RoomManagement.Data/BillingInputStore.cs` | `GET/POST /api/properties/{propertyId}/utility-rates` |
| Chỉ số điện nước | Cùng service/store ở trên | `GET/POST /api/properties/{propertyId}/rooms/{roomId}/meter-readings` |
| Danh mục dịch vụ | Cùng service/store ở trên | `GET/POST /api/services` (POST chỉ Admin) |
| Dịch vụ của phòng | Cùng service/store ở trên | `GET/POST /api/properties/{propertyId}/rooms/{roomId}/services` |

DTO nằm ở `backend/src/RoomManagement.Business/DTOs/BillingInputModels.cs`; hợp đồng store ở `backend/src/RoomManagement.Business/Interfaces/IBillingInputStore.cs`; request/response HTTP ở `backend/src/RoomManagement.API/Contracts/Billing/`. Store dùng transaction serializable để ngăn khoảng giá và khoảng dịch vụ chồng nhau, và yêu cầu chỉ số đồng hồ nối tiếp các kỳ đã có. Quyền landlord được kiểm theo `Property.OwnerUserId`.

## Chương 3: hóa đơn

| Phần | File chính | API |
| --- | --- | --- |
| Xem trước và tạo hóa đơn tháng | `backend/src/RoomManagement.Business/Services/InvoiceService.cs`, `MonthlyInvoiceCalculator.cs` | `POST /api/properties/{propertyId}/invoices/monthly/preview`, `POST /api/properties/{propertyId}/invoices/monthly` |
| Tạo hóa đơn cọc | `backend/src/RoomManagement.Business/Services/InvoiceService.cs` | `POST /api/contracts/{contractId}/invoices/deposit` |
| Danh sách và chi tiết của landlord | `backend/src/RoomManagement.Data/InvoiceStore.cs` | `GET /api/properties/{propertyId}/invoices`, `GET /api/invoices/{invoiceId}` |
| Phát hành và hủy | `backend/src/RoomManagement.Data/InvoiceStore.cs`, `InvoiceTransactionLock.cs` | `POST /api/invoices/{invoiceId}/issue`, `POST /api/invoices/{invoiceId}/cancel` |
| Danh sách và chi tiết của tenant | `backend/src/RoomManagement.Data/InvoiceStore.cs` | `GET /api/me/invoices`, `GET /api/me/invoices/{invoiceId}` |

DTO và interface nằm ở `backend/src/RoomManagement.Business/DTOs/InvoiceModels.cs` và `backend/src/RoomManagement.Business/Interfaces/IInvoiceStore.cs`; request/response HTTP ở `backend/src/RoomManagement.API/Contracts/Invoices/`. Số dư lấy từ view `vwInvoiceBalances`, không tính lại tại controller. Hóa đơn trùng kỳ/cọc bị chặn bằng unique index hiện có trong database; phát hành/hủy được khóa theo hóa đơn trong transaction.

### Quy tắc đang áp dụng

- Tiền thuê tháng lẻ chia theo số ngày hợp đồng có hiệu lực trong tháng; tiền được làm tròn 2 chữ số thập phân.
- Giá điện nước lấy theo ngày đầu kỳ. Chỉ số điện nước của cả hai loại là bắt buộc. Kỳ thuê lẻ tháng có tiêu thụ điện nước sẽ báo lỗi vì chưa có chỉ số bàn giao để phân bổ.
- Dịch vụ áp dụng đủ cả tháng được tính một lần với `CustomPrice`, nếu không có thì dùng `DefaultPrice`. Dịch vụ chỉ áp dụng một phần tháng sẽ báo lỗi vì chưa có quy tắc tính theo ngày.
- Hạn thanh toán là `PaymentDueDay` của tháng sau, kẹp vào ngày cuối tháng nếu cần. Không tạo/phát hành hóa đơn khi hạn đã qua. Ngày hiện tại tính theo UTC+7.
- Tenant chỉ đọc hóa đơn `Issued` của hợp đồng có liên kết `ContractTenant`. Với hóa đơn tháng, liên kết phải giao với tháng tính tiền; với hóa đơn cọc, liên kết phải còn hiệu lực tại ngày phát hành.
- Hủy bị chặn khi đã có payment, refund, adjustment hoặc phân bổ cọc. Phần ghi adjustment/payment/refund thuộc các chương sau.

### Kiểm tra

Chạy `dotnet build backend/RoomManagement.sln --no-restore -m:1 -p:UseSharedCompilation=false`. Unit test tính tiền ở `backend/tests/RoomManagement.Business.Tests/MonthlyInvoiceCalculatorTests.cs`; SQL integration test ở `backend/tests/RoomManagement.Tests/BillingInvoiceStoreTests.cs` và cần `ROOMMANAGEMENT_TEST_SQLSERVER` để kết nối database thử nghiệm.
