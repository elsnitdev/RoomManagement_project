# Test API bằng Postman

Import `RoomManagement.postman_collection.json` và `RoomManagement.postman_environment.json`, sau đó chọn environment **RoomManagement Local**.

Nếu Postman báo `getaddrinfo ENOTFOUND {{baseurl}}`, biến `baseUrl` chưa được nhận. Chọn **RoomManagement Local** ở góc trên bên phải, hoặc vào **Collection → Variables** và đặt `baseUrl` thành `http://localhost:5185`. Collection cũng có sẵn giá trị mặc định này; import lại collection sau khi cập nhật file. URL được Postman gửi phải hiện thành `http://localhost:5185/api/auth/login`.

1. Khởi động SQL Server, chạy migration và API theo [`../README.md`](../README.md). Mặc định API HTTP dùng `http://localhost:5185`; đổi `baseUrl` nếu chạy cổng khác.
2. Điền `adminPassword` trong environment. Chạy **Admin login**; test script lưu `adminToken`.
3. Để tạo tài khoản Tenant, sửa username/email trong **Create user** để không trùng dữ liệu cũ, rồi chạy request. Script lưu `userId` và `userNameOrEmail`. Điền `userPassword` bằng mật khẩu vừa dùng, chạy **User login**, rồi thử hai request **Profile**.
4. Để thử luồng chủ trọ, sửa username/email/CCCD trong **Register landlord** thành giá trị chưa tồn tại rồi chạy. Script lưu `pendingLandlordId` và `userNameOrEmail`. Chạy **Pending landlords**, sau đó **Approve landlord** hoặc **Reject landlord**. Hai request xét duyệt cần hai tài khoản Pending khác nhau; request thứ hai trên cùng ID sẽ trả `409`. Sau khi duyệt, điền `userPassword` và chạy **User login**.

Collection có kiểm tra HTTP status và vài trường phản hồi chính trong tab **Test Results**. Token có hiệu lực 15 phút theo cấu hình hiện tại; khi nhận `401` do hết hạn, chạy lại request login. `POST /api/auth/register` trả `501` vì chưa được triển khai.

Các request tạo tài khoản, cập nhật hồ sơ, duyệt và từ chối sẽ ghi vào database đang kết nối. Hãy dùng database thử nghiệm khi chạy toàn bộ luồng.
