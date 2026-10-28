# Cấu trúc gọi API của frontend

```text
src/
├── services/
│   ├── apiClient.js       # fetch, base URL, Bearer token, lỗi HTTP
│   ├── apiCache.js        # cache ngắn hạn và gộp request trùng
│   ├── api.js             # xuất lại API để trang import từ một nơi
│   └── api/
│       ├── authApi.js     # đăng nhập, đăng ký
│       ├── userApi.js     # hồ sơ cá nhân
│       └── adminUserApi.js # tài khoản và duyệt chủ trọ
├── contexts/
│   └── AuthContext.jsx    # người dùng đăng nhập, signIn, signOut
└── hooks/
    └── useApiQuery.js     # data, isLoading, error, reload
```

Trang chỉ import hàm cần dùng từ `services/api.js`; file này chỉ xuất lại hàm từ các module trong `services/api/`, nên không phình to khi thêm endpoint. Mỗi module quản lý một nhóm nghiệp vụ. Không đặt `fetch`, URL API hoặc xử lý token trong từng trang. Context chỉ giữ phiên đăng nhập; dữ liệu nghiệp vụ được tải qua hook.

## Ví dụ sử dụng

```jsx
import { approveLandlord, getPendingLandlords } from '../services/api'
import { useApiQuery } from '../hooks/useApiQuery'

function PendingLandlords() {
  const { data, isLoading, error, reload } = useApiQuery(
    'admin:pending-landlords',
    getPendingLandlords,
  )

  async function approve(id) {
    await approveLandlord(id)
    reload()
  }

  if (isLoading) return <p>Đang tải...</p>
  if (error) return <p role="alert">{error.message}</p>
  return <ul>{data.data.map(item => <li key={item.userId}>
    {item.fullName} <button onClick={() => approve(item.userId)}>Duyệt</button>
  </li>)}</ul>
}
```

```jsx
import { useAuth } from '../contexts/AuthContext'

const { user, signIn, signOut } = useAuth()
await signIn({ userNameOrEmail, password })
```

Sau khi một thao tác ghi thành công, module API tương ứng xóa cache liên quan và trang gọi `reload()` để cập nhật ngay. Các request đọc cùng `key` trong 30 giây dùng chung dữ liệu và gộp request đang chạy. `key` phải chứa ID và bộ lọc, ví dụ `profile:${userId}`.

## Cấu hình

- Mặc định `VITE_API_BASE_URL` để trống: frontend dùng Vite proxy `/api` tới `http://localhost:5185` khi phát triển.
- Khi frontend và API khác origin ở môi trường triển khai, đặt `VITE_API_BASE_URL=https://api.example.com` trong cấu hình build. Backend phải cho phép CORS cho origin của frontend.
- Phiên đăng nhập được lưu trong `sessionStorage` của tab để F5 không phải đăng nhập lại. Phiên tự xóa khi token hết hạn, API trả `401`, hoặc người dùng đăng xuất; frontend không lưu token vào `localStorage`.
- Trang `#/landlord/properties` hiển thị khu trọ từ API thật. `#/landlord/properties/{id}` chỉ hiển thị chi tiết khu trọ và liên kết tới hai trang phòng riêng: `#/landlord/properties/{id}/rooms` để tìm, lọc, sửa, xóa phòng; `#/landlord/properties/{id}/rooms/new` để tạo phòng. Các trang này dùng endpoint `/api/properties` và `/api/properties/{id}/rooms`; cần chạy backend và đăng nhập bằng tài khoản Landlord đã được duyệt.
- Các trang tổng quan, hợp đồng, hóa đơn và yêu cầu vẫn dùng dữ liệu mẫu. Chúng chưa đồng bộ với trang khu trọ/phòng dùng API thật.

Để thêm endpoint mới, thêm hàm vào module nghiệp vụ phù hợp trong `services/api/` rồi export từ `services/api.js`. Mọi module dùng `apiRequest` từ `apiClient.js` và xóa cache tương ứng sau thao tác ghi. Ví dụ hiện có: `PropertyApi.js` và `RoomApi.js`.
