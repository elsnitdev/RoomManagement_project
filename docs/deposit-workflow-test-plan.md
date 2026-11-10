# Kiểm thử luồng hợp đồng → hóa đơn cọc → thanh toán

## Chuẩn bị

1. Cập nhật schema bằng migration `20261108140000_AddPropertyPaymentAccount` trên database thử nghiệm.
2. Có một chủ trọ, một khu trọ, một phòng trống và hai tài khoản người thuê được liên kết với hồ sơ người thuê.
3. Cấu hình tài khoản nhận chuyển khoản tại trang chi tiết khu trọ. Nếu để trống cả ba trường, người thuê phải thấy hướng dẫn liên hệ chủ trọ.
4. Chạy test tự động với SQL Server **dành riêng cho kiểm thử** bằng biến `ROOMMANAGEMENT_TEST_SQLSERVER`. Bộ test tự tạo và xóa database `RoomManagement_Tests_<guid>`.

## Trường hợp kiểm thử trên giao diện

| Mã | Thao tác | Kết quả mong đợi |
| --- | --- | --- |
| C01 | Tạo hợp đồng nháp, cọc bằng 0 | Không tạo hóa đơn cọc. |
| C02 | Tạo hợp đồng nháp, cọc > 0, giữ mặc định chưa thanh toán | Tạo đúng một hóa đơn cọc đã phát hành; số còn phải thu bằng tiền cọc; chủ trọ được đưa đến hóa đơn. |
| C03 | Tạo hợp đồng nháp, cọc > 0, chọn đã nhận, phương thức và thời điểm nhận | Hóa đơn cọc đã phát hành và được thanh toán đủ; lịch sử có khoản được chủ trọ xác nhận. |
| C04 | Thêm người thuê vào hợp đồng, đăng nhập người thuê, mở hóa đơn cọc | Người thuê thấy hóa đơn, thông tin tài khoản của đúng khu trọ; người ngoài hợp đồng không xem được. |
| C05 | Người thuê khai đã chuyển khoản hoặc đã giao tiền mặt | Tạo khoản chờ duyệt; tổng đã thu và số còn phải thu chưa đổi; người thuê khác không thấy chi tiết khoản vừa khai. |
| C06 | Chủ trọ duyệt với số thực nhận khác số khai | Khoản chuyển thành đã xác nhận; tổng đã thu và số còn phải thu thay đổi theo **số thực nhận**. |
| C07 | Chủ trọ từ chối và ghi lý do | Khoản chuyển thành bị từ chối, người khai thấy lý do; số dư hóa đơn không đổi. |
| C08 | Chủ trọ tự ghi khoản đã nhận, kể cả lớn hơn số còn phải thu | Khoản được xác nhận ngay; số dư có thể âm và tiền dư hiển thị để đối soát. |
| C09 | Người thuê rời hợp đồng | Vẫn xem hóa đơn và lịch sử của mình; không thấy biểu mẫu khai khoản mới. |
| C10 | Hủy hợp đồng nháp có hóa đơn cọc nhưng chưa có khoản thanh toán hoặc giao dịch | Hợp đồng và hóa đơn cọc cùng chuyển sang đã hủy; người thuê không còn thấy hóa đơn đã hủy. |
| C11 | Hủy hợp đồng nháp có khoản chờ duyệt hoặc đã xác nhận | Hệ thống báo cần đối soát; hợp đồng và hóa đơn giữ trạng thái cũ. |
| C12 | Mở hợp đồng cũ có cọc > 0 và chưa từng có hóa đơn cọc | Chỉ khi đó thấy nút tạo thủ công; tạo ra hóa đơn **nháp**, chủ trọ kiểm tra rồi phát hành. |
| C13 | Mở hợp đồng đã có hóa đơn cọc, kể cả hóa đơn đã hủy | Hiện liên kết đến hóa đơn và trạng thái; không hiện nút tạo thủ công. |

## Kiểm tra tự động

- `DepositWorkflowTests`: luồng tạo hợp đồng → người thuê khai → chủ trọ duyệt/ghi nhận → số dư; tự hủy hóa đơn khi hủy hợp đồng chưa có giao dịch; tạo nháp cho hợp đồng cũ.
- `ContractDepositCreationTests`, `TenantPaymentTests`, `LandlordPaymentTests`: kiểm tra quy tắc riêng từng bước, quyền truy cập và gửi lại yêu cầu.
- `npm run build` và `node --test src/components/contracts/contractForm.test.js` trong `frontend`.

Không chạy các test SQL trên database của ứng dụng. Migration chỉ nên áp dụng vào môi trường cần triển khai sau khi đã review schema.
