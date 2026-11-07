# Test API bằng Postman

Import `RoomManagement.postman_collection.json` và `RoomManagement.postman_environment.json`, sau đó chọn environment **RoomManagement Local**.

Nếu Postman báo `getaddrinfo ENOTFOUND {{baseurl}}`, biến `baseUrl` chưa được nhận. Chọn **RoomManagement Local** ở góc trên bên phải, hoặc vào **Collection → Variables** và đặt `baseUrl` thành `http://localhost:5185`. Collection cũng có sẵn giá trị mặc định này; import lại collection sau khi cập nhật file. URL được Postman gửi phải hiện thành `http://localhost:5185/api/auth/login`.

1. Khởi động SQL Server, chạy migration và API theo [`../README.md`](../README.md). Mặc định API HTTP dùng `http://localhost:5185`; đổi `baseUrl` nếu chạy cổng khác.
2. Điền `adminPassword` trong environment. Chạy **Admin login**; test script lưu `adminToken`.
3. Để tạo tài khoản Tenant, sửa username/email trong **Create user** để không trùng dữ liệu cũ, rồi chạy request. Script lưu `userId` và `userNameOrEmail`. Điền `userPassword` bằng mật khẩu vừa dùng, chạy **User login**, rồi thử hai request **Profile**.
4. Để thử luồng chủ trọ, sửa username/email/CCCD trong **Register landlord** thành giá trị chưa tồn tại rồi chạy. Script lưu `pendingLandlordId` và `userNameOrEmail`. Chạy **Pending landlords**, sau đó **Approve landlord** hoặc **Reject landlord**. Hai request xét duyệt cần hai tài khoản Pending khác nhau; request thứ hai trên cùng ID sẽ trả `409`. Sau khi duyệt, điền `userPassword` và chạy **User login**.

Collection có kiểm tra HTTP status và vài trường phản hồi chính trong tab **Test Results**. Token có hiệu lực 15 phút theo cấu hình hiện tại; khi nhận `401` do hết hạn, chạy lại request login. `POST /api/auth/register` trả `501` vì chưa được triển khai.

Các request tạo tài khoản, cập nhật hồ sơ, duyệt và từ chối sẽ ghi vào database đang kết nối. Hãy dùng database thử nghiệm khi chạy toàn bộ luồng.

Khi thử CRUD khu trọ/phòng, import thêm `RoomManagement.Crud.postman_collection.json`. Response tạo/xem/cập nhật có `rowVersion`; collection tự lưu vào `propertyVersion` hoặc `roomVersion` và gửi lại ở request PUT. Nếu hai người cùng mở bản ghi rồi một người lưu trước, người lưu sau nhận `409 ROW_VERSION_CONFLICT`; chạy lại GET để lấy dữ liệu và version mới trước khi sửa tiếp.

## Hợp đồng gia đình và trạng thái phòng

Import thêm `RoomManagement.Occupancy.postman_collection.json` và dùng cùng environment. Đăng nhập bằng **chủ trọ đã được duyệt** qua request **User login** ở collection chính để có `accessToken`, rồi chạy thư mục **Family occupancy flow** theo thứ tự (hoặc dùng Collection Runner). Không cần tạo tài khoản Tenant.

Flow tự tạo dữ liệu mẫu trong database đang kết nối: một khu trọ, một phòng gia đình tối đa 3 người, một phòng đối chứng còn trống, bốn `TenantProfile` không có tài khoản và hai kỳ hợp đồng liên tiếp. Cả bốn profile có ngày sinh để kiểm tra người đủ tuổi ký cam kết. Ba profile đầu thuộc kỳ đầu; người thứ nhất là người liên hệ. Request `13A` kiểm tra kích hoạt bị từ chối khi thiếu cam kết. Các request `13B–13D` ghi cam kết riêng cho cả ba người, gồm trường hợp phụ huynh ký thay trẻ em; `EvidenceStatus` vẫn là `PendingUpload` vì chưa triển khai ảnh/PDF. Sau đó flow kích hoạt hợp đồng, kiểm tra sức chứa và trạng thái phòng.

Các request `19–24` tạo report `MoveOutNotice`, xác minh thời điểm gửi do máy chủ ghi, xử lý report, đổi người liên hệ rồi ghi người liên hệ cũ rời phòng. Report không tự ghi ngày rời hoặc hoàn tiền. `LeaveDate` là ngày cuối người đó còn thuộc hợp đồng, nên request 24 kiểm tra ngày đã ghi; số người hiện tại giảm từ ngày hôm sau. Các request `25–35` chuẩn bị hợp đồng tháng sau: một người tham gia ngày 1, một người tiếp tục ở sau 7 ngày trống và một người sau 8 ngày trống. API thời gian ở chỉ tính đến ngày hiện tại, vì vậy để quan sát kết quả 7/8 ngày trên dữ liệu mẫu này cần gọi lại request `33–34` từ ngày 9 của tháng sau; bài kiểm thử backend kiểm tra quy tắc ngay lập tức với ngày cố định. Các ID và ngày thử được lưu trong **Collection Variables** để gọi lại API chi tiết.

Mỗi lần chạy từ request 01 sẽ tạo một bộ dữ liệu mới. Chỉ chạy trên database thử nghiệm. Cần áp dụng migration đã được duyệt vào database thử nghiệm trước khi chạy flow; collection không tự chạy migration.

Thư mục **Legacy open-ended contract** có request riêng cho `PUT .../contracts/{id}/end-date`. Điền `legacyContractId` bằng ID của hợp đồng cũ có `endDate = null` trong database thử nghiệm; request này không nằm trong Runner chính vì API tạo hợp đồng mới đã bắt buộc có ngày kết thúc cuối tháng.
