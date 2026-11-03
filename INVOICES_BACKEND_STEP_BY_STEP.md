# Học và code backend Invoices theo từng file

Tài liệu này bám theo [kế hoạch Invoice](plans/invoices-backend-implementation-plan.md) và code trong repo ngày 02/11/2026. Mỗi chương nói **file nào, ghi nội dung gì, gọi file nào tiếp theo, kiểm tra ra sao**. Các đoạn code là mẫu để bạn tự triển khai; chúng chưa được chép vào source code.

| Thứ tự | Chương | Kết quả sau chương |
| --- | --- | --- |
| 1 | [Hợp đồng và người thuê](invoice-guide/01-contract.md) | Tạo, xem, kích hoạt hợp đồng; liên kết người thuê. Có code cụ thể cho `ContractStore` và các file đang để trống. |
| 2 | [Giá, chỉ số, dịch vụ](invoice-guide/02-billing-inputs.md) | Có đủ dữ liệu thực để tính một kỳ hóa đơn. |
| 3 | [Tính tiền và tạo hóa đơn](invoice-guide/03-invoice.md) | Preview, lưu draft, phát hành, hủy và xem số dư. |
| 4 | [Cộng/trừ sau phát hành](invoice-guide/04-adjustments.md) | Phụ thu, giảm trừ, void và migration cho view số dư. |
| 5 | [Thanh toán, hoàn tiền, cọc](invoice-guide/05-payments-refunds.md) | Ghi nhận tiền thực nhận, đối soát, hoàn và phân bổ cọc. |

## Cách dùng

1. Đọc **một chương**, thực hiện các file theo thứ tự trong chương.
2. Sau mỗi file, build để phát hiện lỗi namespace/DI sớm; sau mỗi chương, chạy checkpoint và test được ghi ở cuối chương.
3. Trong các đoạn code, tên kiểu mới được định nghĩa ở chương tương ứng. Không tạo tất cả file cùng lúc rồi để trống: một lát cắt chạy được dễ kiểm tra hơn.

**Trạng thái hiện tại:** các entity và cấu hình EF cho Contract/Invoice/Payment đã có. Bạn đã tạo bộ khung file Contract; `ContractModels.cs`, `ContractService.cs`, `ContractsController.cs`, `ContractRequests.cs`, `ContractResponse.cs` đang rỗng; `IContractStore.cs` và `ContractStore.cs` mới có khai báo kiểu. DI cho Contract đã thêm vào `Program.cs` và `Data/ServiceCollectionExtensions.cs`. Các chương bắt đầu từ đúng trạng thái này. Các quy tắc tiền thuê tháng lẻ, giá điện nước đổi giữa kỳ, phí dịch vụ theo ngày và hạn thanh toán vẫn cần được chốt trước khi đưa vào sử dụng thực tế.
