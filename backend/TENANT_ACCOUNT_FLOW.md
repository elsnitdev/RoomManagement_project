# Đăng ký tài khoản người thuê

1. Chủ trọ tạo hồ sơ qua `POST /api/landlord/tenants` và gửi mã chủ trọ 10 ký tự hiển thị trên thanh tài khoản cho người thuê.
2. Người thuê nhập mã chủ trọ và số điện thoại. `POST /api/auth/tenant-registration/check` chỉ tìm hồ sơ do chủ trọ đó tạo.
3. `POST /api/auth/tenant-registration/request-otp` tạo mã hiệu lực 5 phút. Mỗi số phải chờ 60 giây trước khi yêu cầu mã mới. Development dùng `1111`; khi có nhà cung cấp SMS thật sẽ dùng mã 6 chữ số.
4. `POST /api/auth/tenant-registration/verify-otp` nhận `{ landlordCode, phoneNumber, challengeId, code }`. Tối đa 5 lần thử.
5. `POST /api/auth/register-tenant` nhận `{ landlordCode, phoneNumber, challengeId, password, confirmPassword }`. Backend kiểm tra mã đã xác minh và chưa sử dụng, sau đó tạo tài khoản và liên kết hồ sơ trong một giao dịch.
6. Người thuê đăng nhập qua `POST /api/auth/login` với `userNameOrEmail` là `MACHUTRO:SODIENTHOAI` và mật khẩu.

`ISmsOtpSender` là điểm tích hợp nhà cung cấp SMS. Ở môi trường `Development`, yêu cầu từ loopback (`localhost`) dùng mã cố định `1111` để thử toàn bộ luồng; API trả `testOtp` trong response. Mã này không xác minh quyền sở hữu số điện thoại. Yêu cầu từ máy khác bị từ chối. Ở các môi trường khác, `SmsOtpSender` từ chối gửi và API trả `503 SMS_NOT_CONFIGURED` cho tới khi tích hợp nhà cung cấp SMS thật. Không bật chế độ Development trên máy chủ công khai.

Áp dụng migration `20261107150000_LandlordScopedTenantOtp` sau migration `20261107120000_TenantPhoneRegistration`. Migration cấp mã ngắn cho chủ trọ cũ và đổi tên đăng nhập của tài khoản người thuê đã gắn hồ sơ theo định dạng mới. Dữ liệu chủ trọ và người thuê chưa gắn hồ sơ cần được rà soát riêng.
