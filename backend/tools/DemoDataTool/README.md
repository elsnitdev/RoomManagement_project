# Dữ liệu mẫu dùng lại cho kiểm thử thủ công

Công cụ này tạo một khu trọ và các tài khoản riêng có tiền tố `rm_demo_` trong database `RoomManagement` được khai báo tại `backend/src/RoomManagement.API/appsettings.Local.json`. Không chạy trên database có tên khác `RoomManagement` hoặc `RoomManagement_*`. Database cần được cập nhật migration trước khi chạy.

Từ thư mục gốc dự án, dùng PowerShell:

```powershell
dotnet run --project backend/tools/DemoDataTool -- seed
dotnet run --project backend/tools/DemoDataTool -- status
```

Lần seed đầu, công cụ tạo mật khẩu ngẫu nhiên và lưu trong `backend/tools/DemoDataTool/.demo-password` (Git bỏ qua file này). Đọc file đó để đăng nhập; các lần seed sau dùng lại cùng mật khẩu. Có thể đặt biến môi trường `ROOMMANAGEMENT_DEMO_PASSWORD` (ít nhất 12 ký tự) trước khi seed nếu muốn dùng mật khẩu riêng; công cụ sẽ cập nhật file cục bộ. Tất cả tài khoản được tạo cùng mật khẩu này:

| Tài khoản | Vai trò | Dùng để thử |
| --- | --- | --- |
| `rm_demo_admin` | Admin | Danh sách, duyệt chủ trọ. |
| `rm_demo_landlord` | Landlord đã duyệt | Quản lý khu trọ, người thuê, hợp đồng, dịch vụ, hóa đơn, thanh toán. |
| `rm_demo_landlord_pending` | Landlord chờ duyệt | Kiểm tra luồng duyệt/từ chối. |
| `rm_demo_tenant_a` đến `rm_demo_tenant_d` | Tenant | Xem thông tin và hóa đơn theo từng hợp đồng. |

Khu trọ có ID `d0000000-0000-4000-8000-000000000001`. Dữ liệu được định theo **tháng trước tháng chạy seed** để hóa đơn không nằm ở kỳ tương lai:

| Phòng | Trạng thái ban đầu | Tình huống |
| --- | --- | --- |
| DEMO-A | Hợp đồng đang hoạt động; tenant A | Hóa đơn đã phát hành, chưa thanh toán; thử thanh toán và kết thúc hợp đồng. |
| DEMO-B | Hợp đồng đang hoạt động; tenant B | Hóa đơn đã thanh toán đủ bằng chuyển khoản; thử tra cứu lịch sử. |
| DEMO-C | Phòng trống; tenant C chưa gắn hợp đồng | Thử tạo, kích hoạt, hủy hợp đồng mới. |
| DEMO-D | Hợp đồng đang hoạt động; tenant D | Chưa có hóa đơn; thử nhập chỉ số, tạo và phát hành hóa đơn. |

Giá điện là 3.500đ/kWh, nước 15.000đ/m³. Khu trọ có dịch vụ Internet 100.000đ/tháng và giữ xe 80.000đ/xe/tháng. Các phòng A/B có chỉ số tháng trước và hóa đơn tương ứng. Phòng D để trống chỉ số để thử nhập liệu. Hồ sơ tenant A–D thuộc chủ trọ mẫu.

Sau mỗi vòng test:

```powershell
dotnet run --project backend/tools/DemoDataTool -- reset
```

Khi muốn thử lại, dùng `seed` như trên. Hoặc dùng một lệnh để dọn và nạp lại ngay:

```powershell
powershell -ExecutionPolicy Bypass -File backend/scripts/Refresh-DemoData.ps1
```

`reset` xóa các khu trọ do tài khoản chủ trọ mẫu tạo, dữ liệu gắn với khu đó và các tài khoản/hồ sơ mẫu trong **một giao dịch**. Lệnh có thể chạy lại khi dữ liệu mẫu đã hết. Nếu phát sinh tham chiếu đến dữ liệu ngoài bộ mẫu, ràng buộc database sẽ làm giao dịch thất bại và không xóa dở. Ảnh phòng và chứng từ đã tải lên được đưa vào hàng đợi xóa blob; API cục bộ cần chạy để `BlobCleanupWorker` xử lý hàng đợi này. Dịch vụ **toàn hệ thống** do Admin tạo thủ công không thuộc khu mẫu, nên không được xóa tự động; hãy tạo dịch vụ thuộc khu mẫu khi thử luồng dịch vụ.

Nếu kết nối SQL Server không hoạt động, `seed/status/reset` sẽ dừng trước khi sửa dữ liệu. Kiểm tra SQL Server và chuỗi kết nối trong `appsettings.Local.json`, rồi chạy lại lệnh. Nếu database còn migration, áp dụng theo hướng dẫn trong `backend/README.md` trước khi seed.
