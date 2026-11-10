# RoomManagement_project

Backend dùng .NET 10 và SQL Server. Khi chạy cục bộ, API và công cụ EF đọc cấu hình từ `backend/src/RoomManagement.API/appsettings.Local.json`. File này được Git bỏ qua để không đưa khóa JWT vào repository.

## Cấu hình cục bộ

Tạo `backend/src/RoomManagement.API/appsettings.Local.json` một lần với `ConnectionStrings:RoomManagement` và `Jwt:SigningKey` (khóa ngẫu nhiên tối thiểu 32 byte). File này chỉ được API nạp trong môi trường Development; khi chạy `dotnet ef`, chuỗi kết nối cũng được đọc từ file. Nếu cần tạo Admin đầu tiên, thêm `InitialAdmin:Password` là một mật khẩu riêng có ít nhất 12 ký tự. Nếu database đã có Admin, seed không đổi mật khẩu tài khoản đó. Mật khẩu mặc định từng được commit phải được đổi riêng tại mọi môi trường đã dùng nó.

Trong PowerShell, từ thư mục gốc dự án:

    dotnet ef database update --project backend/src/RoomManagement.Data --startup-project backend/src/RoomManagement.API
    dotnet run --project backend/src/RoomManagement.API --launch-profile http

API sẽ không khởi động khi thiếu khóa ký JWT hoặc khi đã yêu cầu seed Admin nhưng seed thất bại. Có thể bỏ qua seed bằng cách để trống InitialAdmin:Password nếu database đã được cấp Admin theo cách khác. Ngoài Development, cấu hình bằng biến môi trường hoặc hệ thống quản lý secrets của môi trường triển khai.

POST /api/auth/login nhận userNameOrEmail và password, trả accessToken Bearer có thời hạn. Các route /api/admin/users yêu cầu vai trò Admin; hai route hồ sơ /api/users/{id}/profile-status và /api/users/{id}/profile yêu cầu token của chính tài khoản có ID tương ứng. Tài khoản chờ duyệt, bị từ chối hoặc bị khóa không được sử dụng token.

## Kiểm thử

    dotnet test backend/RoomManagement.sln

Các test SQL chỉ chạy khi cấu hình ROOMMANAGEMENT_TEST_SQLSERVER trỏ tới SQL Server dành cho kiểm thử. Chúng tạo database tên ngẫu nhiên RoomManagement_Tests_<guid> rồi xóa chính database đó sau test. Tài khoản kết nối cần quyền tạo/xóa database; không dùng connection của database ứng dụng để chạy test.

Sơ đồ luồng và trách nhiệm từng file: [account-and-approval-flow.md](docs/account-and-approval-flow.md).

Collection và environment Postman để thử các API hiện có: [postman/README.md](postman/README.md).

## Hợp đồng thuê phòng

Sau khi cập nhật database bằng lệnh EF ở trên, chủ trọ dùng `POST /api/properties/{propertyId}/contracts` để tạo bản nháp, rồi `POST /api/properties/{propertyId}/contracts/{contractId}/users` với `{ "email": "tenant@example.com", "joinDate": "2026-11-03" }` để gắn tài khoản Tenant đã hoàn thiện họ tên và số điện thoại. `POST .../{contractId}/activate` kích hoạt hợp đồng. Chủ trọ xem danh sách/chi tiết qua `GET /api/properties/{propertyId}/contracts` và `GET .../{contractId}`; người thuê xem hợp đồng của chính mình qua `GET /api/me/contracts` và `GET /api/me/contracts/{contractId}`.

Chủ trọ có thể ghi nhận người rời phòng bằng `POST .../{contractId}/tenants/{linkId}/leave`, hủy bản nháp bằng `POST .../{contractId}/cancel`, hoặc kết thúc hợp đồng đang hoạt động bằng `POST .../{contractId}/terminate`. Phản hồi chi tiết có danh sách người thuê, số dư chưa thanh toán, cọc đang giữ và trạng thái bản ghi tất toán. Phòng trả `contractId` và `occupants` suy ra từ hợp đồng đang có hiệu lực theo ngày Việt Nam. Hợp đồng hết hạn được cập nhật tự động; công nợ và hóa đơn vẫn giữ trên hợp đồng cũ.

Khi tạo hợp đồng có tiền cọc, API đồng thời phát hành hóa đơn cọc. Trạng thái mặc định là chưa thanh toán; nếu chủ trọ chọn đã nhận đủ, cần phương thức và thời điểm nhận. Hợp đồng cũ có cọc nhưng chưa từng có hóa đơn cọc có thể dùng `POST /api/contracts/{contractId}/invoices/deposit` để tạo **hóa đơn nháp** rồi phát hành sau khi kiểm tra; `GET` cùng đường dẫn trả hóa đơn cọc hiện có hoặc `null`. Khi hủy hợp đồng nháp, hóa đơn cọc chưa có giao dịch cũng được hủy trong cùng giao dịch. Nếu đã có khoản thanh toán, hoàn tiền, điều chỉnh hoặc dùng cọc, việc hủy hợp đồng bị chặn để đối soát. Kịch bản kiểm tra chi tiết ở [deposit-workflow-test-plan.md](../docs/deposit-workflow-test-plan.md).
