# RoomManagement_project

Backend dùng .NET 10 và SQL Server. Cấu hình ConnectionStrings__RoomManagement cho API và công cụ EF; chạy migration trước khi khởi động API. Không lưu mật khẩu Admin hoặc khóa ký JWT vào repository.

## Cấu hình cục bộ

Trong PowerShell, từ thư mục gốc dự án:

    $env:ConnectionStrings__RoomManagement = 'Server=.;Database=RoomManagement;Integrated Security=True;TrustServerCertificate=True'
    dotnet user-secrets set 'Jwt:SigningKey' '<chuoi-ngau-nhien-toi-thieu-32-byte>' --project backend/src/RoomManagement.API

Nếu cần tạo Admin đầu tiên, đặt InitialAdmin:Password bằng một mật khẩu riêng có ít nhất 12 ký tự qua user secrets hoặc biến môi trường. Nếu database đã có Admin, bước seed không đổi mật khẩu tài khoản đó. Mật khẩu mặc định từng được commit phải được đổi riêng tại mọi môi trường đã dùng nó.

    dotnet user-secrets set 'InitialAdmin:Password' '<mat-khau-admin-rieng>' --project backend/src/RoomManagement.API
    dotnet ef database update --project backend/src/RoomManagement.Data --startup-project backend/src/RoomManagement.API
    dotnet run --project backend/src/RoomManagement.API

API sẽ không khởi động khi thiếu khóa ký JWT hoặc khi đã yêu cầu seed Admin nhưng seed thất bại. Có thể bỏ qua seed bằng cách để trống InitialAdmin:Password nếu database đã được cấp Admin theo cách khác.

POST /api/auth/login nhận userNameOrEmail và password, trả accessToken Bearer có thời hạn. Các route /api/admin/users yêu cầu vai trò Admin; hai route hồ sơ /api/users/{id}/profile-status và /api/users/{id}/profile yêu cầu token của chính tài khoản có ID tương ứng. Tài khoản chờ duyệt, bị từ chối hoặc bị khóa không được sử dụng token.

## Kiểm thử

    dotnet test backend/RoomManagement.sln

Các test SQL chỉ chạy khi cấu hình ROOMMANAGEMENT_TEST_SQLSERVER trỏ tới SQL Server dành cho kiểm thử. Chúng tạo database tên ngẫu nhiên RoomManagement_Tests_<guid> rồi xóa chính database đó sau test. Tài khoản kết nối cần quyền tạo/xóa database; không dùng connection của database ứng dụng để chạy test.

Sơ đồ luồng và trách nhiệm từng file: [account-and-approval-flow.md](docs/account-and-approval-flow.md).
