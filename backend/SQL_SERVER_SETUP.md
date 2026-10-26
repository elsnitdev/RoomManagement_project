## cách kết nối sql server

## 1 mở terminal hoặc powershell tại thư mục dự án RoomManagement_project

chạy lệnh
**$env:ConnectionStrings\_\_RoomManagement = 'Server=.\SQLEXPRESS02;Database=RoomManagement;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'**
Instance đã xác nhận trên máy hiện tại là `.\SQLEXPRESS02`; nếu chạy ở máy khác, thay bằng tên instance tương ứng.

## 2 chạy lệnh update migration

**dotnet ef database update --project backend/src/RoomManagement.Data --startup-project backend/src/RoomManagement.API --context RoomManagementDbContext**

## 3 Tại mục RoomManagement_project chạy lệnh khởi động API

**dotnet run --project .\backend\src\RoomManagement.API\RoomManagement.API.csproj --launch-profile http**
ELSNIT\SQLEXPRESS02
