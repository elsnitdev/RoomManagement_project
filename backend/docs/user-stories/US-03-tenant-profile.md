# US-03 — Quản lý hồ sơ người thuê

**Ưu tiên:** P0  
**Tác nhân:** Chủ trọ  
**Mục tiêu:** Lưu thông tin người ở trước khi lập hợp đồng, kể cả khi người đó chưa có tài khoản đăng nhập.

## User story

> Là chủ trọ, tôi muốn tạo, xem và cập nhật hồ sơ người thuê độc lập với tài khoản đăng nhập, để quản lý thông tin người ở và chọn đúng người khi lập hợp đồng.

## Tiêu chí chấp nhận

1. **Tạo hồ sơ:** Chủ trọ nhập họ tên và số điện thoại bắt buộc; có thể nhập email, CCCD, ngày sinh, giới tính và địa chỉ thường trú. Sau khi lưu, hệ thống cấp mã hồ sơ riêng và có thể xem lại thông tin.
2. **Không cần tài khoản:** Tạo hồ sơ không tạo `AppUser`, không yêu cầu tên đăng nhập/mật khẩu và để `TenantProfile.UserId` rỗng. Một hồ sơ chưa có tài khoản vẫn có thể được chọn khi lập hợp đồng.
3. **Kiểm tra dữ liệu:** Từ chối họ tên hoặc số điện thoại trống/sai định dạng; ngày sinh không hợp lệ; giới tính ngoài `Male`, `Female`, `Other`. Nếu CCCD đã có ở một hồ sơ khác, không tạo bản ghi trùng và trả lỗi xung đột. Các trường tùy chọn được phép để trống.
4. **Xem và tìm kiếm:** Chủ trọ xem danh sách/chi tiết hồ sơ trong phạm vi mình được quản lý; có thể tìm theo họ tên hoặc số điện thoại. Danh sách có phân trang để không tải toàn bộ hồ sơ một lần.
5. **Cập nhật:** Chủ trọ sửa thông tin liên hệ/cá nhân của hồ sơ đã tồn tại. Hệ thống kiểm tra lại dữ liệu và CCCD trùng trước khi lưu; việc sửa hồ sơ không tự sửa thông tin đã chốt trên chứng từ hay tự tạo tài khoản.
6. **Dùng lại khi lập hợp đồng:** Một hồ sơ có thể được chọn cho hợp đồng qua `ContractTenants`. Mối quan hệ hợp đồng, ngày vào ở và ngày rời ở được lưu ở `ContractTenants`, không lưu trực tiếp trong `TenantProfile`.
7. **Giới hạn truy cập:** Người không có quyền đối với hồ sơ không xem hoặc sửa được dữ liệu cá nhân của hồ sơ đó. API không trả toàn bộ hồ sơ người thuê của các chủ trọ khác.

## Kịch bản kiểm thử chính

| Trường hợp | Kết quả mong đợi |
| --- | --- |
| Tạo hồ sơ chỉ với họ tên và số điện thoại hợp lệ | Tạo thành công; `UserId = null`. |
| Tạo hồ sơ thiếu họ tên hoặc số điện thoại | Trả lỗi nhập liệu `400`; không lưu bản ghi. |
| Tạo hoặc sửa với CCCD đã được hồ sơ khác sử dụng | Trả lỗi xung đột `409`; không ghi đè dữ liệu. |
| Xem hồ sơ thuộc phạm vi chủ trọ khác | Bị từ chối theo quy tắc phân quyền; không lộ dữ liệu hồ sơ. |
| Chọn hồ sơ chưa có tài khoản để lập hợp đồng | Tạo liên kết ở `ContractTenants`; không cần tạo `AppUser`. |
| Cập nhật số điện thoại của hồ sơ đã có hợp đồng | Hồ sơ được cập nhật; quan hệ hợp đồng vẫn giữ nguyên. |

## Ranh giới và phụ thuộc

- US này gồm tạo, xem, tìm kiếm và cập nhật hồ sơ. Cấp tài khoản, đăng nhập, liên kết hồ sơ với tài khoản và thao tác xóa hồ sơ là các luồng riêng. Chưa thiết kế xóa hồ sơ đã có lịch sử hợp đồng.
- Entity `TenantProfile` hiện có các trường cần lưu và `UserId` tùy chọn; CCCD có unique index khi khác null. `ContractTenants` là bảng liên kết hồ sơ với hợp đồng.
- **Cần chốt trước khi triển khai API danh sách:** `TenantProfiles` hiện không có `OwnerUserId` hoặc `PropertyId`. Vì vậy chưa thể xác định chủ sở hữu của hồ sơ mới tạo nhưng chưa gắn hợp đồng. Cần bổ sung quan hệ sở hữu phù hợp (ví dụ `OwnerUserId` tới chủ trọ, hoặc bảng liên kết hồ sơ–khu trọ) và migration tương ứng để đáp ứng tiêu chí 4 và 7 trong hệ thống nhiều chủ trọ.
