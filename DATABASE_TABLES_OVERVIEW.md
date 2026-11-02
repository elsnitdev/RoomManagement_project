# Tổng hợp các bảng dữ liệu

Danh sách này dựa trên EF Core model và các migration hiện có trong `backend/src/RoomManagement.Data` ngày 02/11/2026. Model định nghĩa **23 bảng nghiệp vụ** thuộc schema `dbo`; tài liệu không xác nhận database của từng môi trường đã áp dụng đủ migration.

## Tài khoản và người thuê

### 1. `Users`
- Lưu tài khoản đăng nhập, mật khẩu đã băm, vai trò và trạng thái hoạt động.
- Lưu thông tin cá nhân và trạng thái xét duyệt tài khoản chủ trọ.

### 2. `TenantProfiles`
- Lưu hồ sơ người thuê như họ tên, liên hệ và giấy tờ tùy thân.
- Có thể liên kết hồ sơ với tài khoản `Users` để người thuê truy cập hệ thống.

## Khu trọ và phòng

### 3. `Properties`
- Lưu thông tin khu trọ, địa chỉ và trạng thái hoạt động.
- Liên kết mỗi khu trọ với tài khoản chủ trọ sở hữu.

### 4. `RoomRows`
- Lưu dãy phòng trong một khu trọ với mã, tên và trạng thái hoạt động.
- Giúp nhóm các phòng và tổ chức việc đánh số phòng theo dãy.

### 5. `Rooms`
- Lưu từng phòng, số phòng, tầng, diện tích, giá cơ bản và sức chứa.
- Theo dõi trạng thái phòng và liên kết phòng với khu trọ hoặc dãy phòng.

### 6. `RoomNumberCounters`
- Lưu số thứ tự phòng cuối cùng theo khu trọ và phạm vi đánh số.
- Giúp cấp số phòng tiếp theo khi tạo nhiều phòng mà không dùng lại số cũ.

### 7. `RoomBatchSubmissions`
- Lưu yêu cầu tạo phòng hàng loạt, mã chống lặp và dấu vân tay của nội dung yêu cầu.
- Lưu kết quả để yêu cầu gửi lại không tạo trùng phòng.

## Hợp đồng và dịch vụ

### 8. `Contracts`
- Lưu hợp đồng thuê phòng, thời hạn, tiền thuê, tiền cọc và ngày đến hạn.
- Theo dõi trạng thái hợp đồng và làm căn cứ lập hóa đơn.

### 9. `ContractTenants`
- Liên kết người thuê với hợp đồng và ghi ngày tham gia, ngày rời đi.
- Cho phép một hợp đồng có nhiều người ở và lưu lịch sử cư trú.

### 10. `ContractDocuments`
- Lưu thông tin tệp, loại tài liệu và vị trí lưu của giấy tờ hợp đồng.
- Giúp tra cứu các tài liệu gắn với một hợp đồng thuê.

### 11. `ContractSettlements`
- Lưu kết quả quyết toán hợp đồng, gồm cọc, công nợ, phí và số hoàn.
- Tạo bản ghi tổng hợp để đối chiếu khi kết thúc hợp đồng.

### 12. `Services`
- Lưu danh mục dịch vụ như internet hoặc giữ xe, đơn vị tính và giá mặc định.
- Cho biết dịch vụ nào còn hoạt động để áp dụng cho phòng.

### 13. `RoomServices`
- Liên kết dịch vụ với phòng, thời gian áp dụng và giá riêng nếu có.
- Xác định khoản dịch vụ cần tính cho từng phòng theo từng giai đoạn.

### 14. `UtilityRates`
- Lưu đơn giá điện hoặc nước theo khu trọ và khoảng thời gian hiệu lực.
- Cung cấp mức giá để tính chi phí tiện ích cho kỳ hóa đơn.

### 15. `MeterReadings`
- Lưu chỉ số điện hoặc nước trước và sau của từng phòng theo tháng.
- Cung cấp lượng tiêu thụ làm đầu vào tính tiền điện nước.

## Hóa đơn và giao dịch tiền

### 16. `Invoices`
- Lưu thông tin hóa đơn gắn với hợp đồng, phòng, loại, kỳ và hạn thanh toán.
- Giữ tổng tiền gốc cùng trạng thái nháp, đã phát hành hoặc đã hủy.

### 17. `InvoiceItems`
- Lưu từng khoản trên hóa đơn, gồm mô tả, số lượng, đơn giá và thành tiền.
- Giải thích tổng tiền hóa đơn bằng các dòng như tiền phòng, điện, nước và dịch vụ.

### 18. `InvoiceAdjustments`
- Lưu khoản điều chỉnh hóa đơn sau phát hành, số tiền, lý do và người thực hiện.
- Theo schema hiện tại, các loại điều chỉnh đều làm giảm tiền; chức năng phụ thu mới chỉ nằm trong kế hoạch.

### 19. `Payments`
- Lưu khoản người thuê khai đã trả và số tiền chủ trọ xác minh thực nhận.
- Theo dõi trạng thái thanh toán để xác định số tiền đã thu của hóa đơn.

### 20. `PaymentReviewEvents`
- Lưu lịch sử duyệt hoặc thay đổi kết quả xác minh một khoản thanh toán.
- Ghi số tiền, trạng thái trước và sau cùng lý do, người duyệt và thời điểm.

### 21. `Refunds`
- Lưu khoản hoàn tiền gắn với hóa đơn, số tiền, lý do và trạng thái hoàn.
- Giúp đối chiếu tiền đã trả lại cho người thuê hoặc tiền cọc đã hoàn.

### 22. `DepositApplications`
- Lưu việc dùng tiền cọc của hợp đồng để bù một hóa đơn khác.
- Làm giảm cọc còn giữ và giảm khoản phải thu của hóa đơn được bù.

## Yêu cầu của người thuê

### 23. `TenantRequests`
- Lưu yêu cầu của người thuê về phòng, dịch vụ hoặc hóa đơn và mức ưu tiên.
- Theo dõi người xử lý, trạng thái và kết quả giải quyết yêu cầu.

## Thành phần không tính vào 23 bảng nghiệp vụ

`vwInvoiceBalances` và `vwContractDepositBalances` là **view**, dùng để tính số dư hóa đơn và tiền cọc; chúng không phải bảng lưu dữ liệu. `__EFMigrationsHistory` là bảng kỹ thuật do EF Core tạo khi áp dụng migration để theo dõi phiên bản schema, không thuộc EF model nghiệp vụ.
