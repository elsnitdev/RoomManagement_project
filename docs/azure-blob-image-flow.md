# Luồng xử lý ảnh phòng với Azure Blob Storage

## Phạm vi

Chủ trọ quản lý ảnh của phòng thuộc khu trọ mình sở hữu. Ảnh nằm trong container Azure Blob **private**; SQL Server chỉ giữ metadata trong `RoomImages` và công việc dọn ảnh trong `BlobCleanupJobs`. Người thuê chưa có quyền xem qua API này. Trang chi tiết phòng mẫu `RoomDetail.jsx` chưa nối vào dữ liệu thật.

## Cấu hình trước khi chạy

1. Chạy migration `AddRoomImages` trên SQL Server. Migration tạo `RoomImages` và `BlobCleanupJobs`, không sửa dữ liệu phòng cũ.
2. Tạo Storage Account và container `room-images` với anonymous access bị tắt. Gán `Storage Blob Data Contributor` cho managed identity của API ở phạm vi container.
3. Cấu hình `AzureBlob:AccountUri` (ví dụ `https://<account>.blob.core.windows.net`) và `AzureBlob:ContainerName=room-images`. API dùng `DefaultAzureCredential`; không lưu account key trong Git.
4. Máy phát triển có thể chạy Azurite và tạo container `room-images`. `appsettings.Development.json` hiện đã đặt `AzureBlob:ConnectionString=UseDevelopmentStorage=true`, nên không cần khai báo lại khi chạy API với `ASPNETCORE_ENVIRONMENT=Development`. `appsettings.Local.json` (được ignore) hoặc biến môi trường có thể ghi đè giá trị này. Khi connection string khác rỗng, ứng dụng dùng nó thay cho `AccountUri`; nếu muốn thử Azure thật ngay trên máy phát triển, phải xóa hoặc ghi đè connection string này thành rỗng rồi đặt `AccountUri`.

Container phải tồn tại trước khi tải ảnh. API không tự tạo container khi nhận request.

## Tải ảnh lên

```mermaid
sequenceDiagram
    actor L as Chủ trọ
    participant UI as Giao diện
    participant API as RoomImagesController
    participant S as RoomImageService
    participant B as Azure Blob
    participant DB as SQL Server
    L->>UI: Chọn ảnh trong chi tiết phòng
    UI->>API: POST /api/properties/{propertyId}/rooms/{roomId}/images (multipart, JWT)
    API->>S: UploadAsync
    S->>DB: Xác nhận phòng thuộc chủ trọ; lấy danh sách ảnh
    S->>S: Kiểm tra <=10 MiB, tối đa 20 ảnh, chữ ký JPEG/PNG/WebP
    S->>B: Ghi blob private với tên do server sinh
    S->>DB: Ghi RoomImage
    S-->>UI: 201 + metadata
```

Tên blob theo `landlords/{ownerId}/properties/{propertyId}/rooms/{roomId}/{imageId}.{ext}`. Tên file từ client chỉ dùng để hiển thị, không được dùng làm đường dẫn lưu. Nếu ghi SQL thất bại sau khi ghi Blob, API thử xóa Blob ngay. Nếu API chết đúng lúc đó hoặc xóa Blob thất bại, có thể còn Blob mồ côi; cần đối soát định kỳ trước khi triển khai quy mô lớn. Hiện tại chỉ kiểm tra chữ ký định dạng, chưa giải mã toàn bộ ảnh, nén ảnh hoặc loại EXIF.

## Xem ảnh

Giao diện gọi `GET /images` lấy metadata, sau đó gọi `GET /images/{imageId}/content` với Bearer token. API kiểm tra quyền chủ trọ qua quan hệ phòng → khu trọ, đọc Blob và stream lại với `Content-Type` đã xác định khi upload, `Cache-Control: private` và `X-Content-Type-Options: nosniff`. Giao diện tạo object URL để hiển thị và thu hồi URL khi đổi phòng/đóng gallery. Không phát URL Blob công khai hoặc SAS. Thứ tự `SortOrder` xác định ảnh bìa (ảnh đầu tiên).

## Sắp xếp và xóa

- `PUT /images/order` nhận **đủ** ID ảnh của phòng theo thứ tự mới; server từ chối ID lặp/thiếu/khác phòng rồi cập nhật `SortOrder`.
- `DELETE /images/{imageId}` xóa metadata và ghi `BlobCleanupJob` trong cùng transaction SQL, trả `204`. Worker chạy mỗi phút, xóa Blob idempotent và retry khi Azure lỗi. Vì vậy `204` nghĩa là ảnh không còn hiện trong phòng và yêu cầu dọn Blob đã được lưu, Blob vật lý có thể tồn tại thêm một thời gian.
- Phòng còn ảnh không thể bị xóa; chủ trọ phải xóa ảnh trước. Điều này tránh mất liên kết metadata khi xóa phòng.

## Lỗi và vận hành

- File rỗng/quá 10 MiB/sai chữ ký: `400`. Vượt 20 ảnh: `409`. Phòng/ảnh không thuộc chủ trọ: `404`. Azure Blob lỗi: `503`.
- Chỉ Landlord có token hợp lệ được dùng các endpoint ảnh. Tenant và chủ trọ khác không được xem/sửa ảnh.
- Theo dõi lỗi `BlobCleanupWorker`, số job tồn đọng và dung lượng Blob. Dùng tài khoản Azure test để kiểm tra ngoài Azurite trước khi triển khai chính thức.
- Khi API chạy trong Docker, `UseDevelopmentStorage=true` có thể trỏ sai `localhost`; cấu hình endpoint Azurite theo tên service Docker.
