# Kiểm thử Invoice trên database RoomManagement

Đã thêm bộ dữ liệu `invoice_demo` vào database `RoomManagement` trên SQL Server cục bộ `ELSNIT\SQLEXPRESS02` ngày 03/11/2026. Không có hóa đơn nào được tạo sẵn; bạn sẽ tự tạo bằng giao diện. Tên đăng nhập có trong bảng dưới; mật khẩu chỉ được gửi trong cuộc trò chuyện, không lưu vào repository.

## 1. Chạy ứng dụng

Đóng phiên API cũ đang chạy để mã mới được nạp, rồi mở hai terminal tại thư mục gốc dự án:

```powershell
dotnet run --project backend/src/RoomManagement.API --launch-profile http
```

```powershell
cd frontend
npm run dev
```

Frontend proxy `/api` tới `http://localhost:5185`. Cấu hình kết nối database và JWT được đọc từ `backend/src/RoomManagement.API/appsettings.Local.json` khi API chạy trong môi trường Development.

## 2. Dữ liệu đã có

| Vai trò | Tên đăng nhập | Dùng để thử |
| --- | --- | --- |
| Admin | `invoice_admin_20261103` | Tạo và xem danh mục dịch vụ. |
| Landlord | `invoice_landlord_20261103` | Giá, chỉ số, dịch vụ phòng, hóa đơn. |
| Tenant A | `invoice_tenant_a_20261103` | Chỉ thấy hóa đơn hợp đồng phòng INV-A sau phát hành. |
| Tenant B | `invoice_tenant_b_20261103` | Chỉ thấy hóa đơn hợp đồng phòng INV-B sau phát hành. |

Khu trọ `Invoice Demo 20261103` có ID `5473c6f9-4ba2-407e-8edf-c999df7bf212` và hai phòng:

| Phòng | Contract ID | Tiền thuê | Tiền cọc | Dữ liệu điện nước tháng 11/2026 |
| --- | --- | ---: | ---: | --- |
| INV-A | `c50d6ae6-17cc-4f99-ad93-2add11009900` | 3.000.000đ | 1.000.000đ | Điện 100→180 kWh; nước 20→32 m³. |
| INV-B | `cedd2a7e-26c0-4fbd-b767-a2e9705d2f91` | 2.500.000đ | 0đ | Chưa nhập; dùng để thử form nhập chỉ số. |

Giá điện 3.500đ/kWh và nước 15.000đ/m³ áp dụng từ 01/11 đến 31/12/2026. INV-A có thêm Internet 100.000đ trọn tháng 11. Hai hợp đồng đang `Active`, người thuê đã được gắn đúng phòng.

## 3. Các kịch bản chính

1. **Xem trước hóa đơn:** đăng nhập landlord → **Hóa đơn** → chọn khu `Invoice Demo 20261103` → chọn hợp đồng INV-A và kỳ `2026-11` → **Xem trước**. Kết quả phải có 4 dòng: thuê 3.000.000đ, điện 280.000đ, nước 180.000đ, Internet 100.000đ; tổng **3.560.000đ**, hạn thanh toán **05/12/2026**.
2. **Nháp và phát hành:** bấm **Tạo bản nháp**. Trước khi phát hành, tenant A đăng nhập sẽ chưa thấy hóa đơn. Landlord mở chi tiết và bấm **Phát hành**. Tenant A đăng nhập lại sẽ thấy hóa đơn, số còn phải thu 3.560.000đ; tenant B không thấy hóa đơn INV-A.
3. **Nhập chỉ số còn thiếu:** landlord chọn INV-B, kỳ `2026-11`, bấm **Xem trước** → báo `INVOICE_INPUT_INCOMPLETE`. Ở tab **Giá, chỉ số và dịch vụ**, chọn phòng INV-B rồi nhập điện 200→250 và nước 40→48, cùng kỳ `11/2026`. Xem trước lại phải ra **2.795.000đ** (thuê 2.500.000đ + điện 175.000đ + nước 120.000đ). Tạo và phát hành; tenant B chỉ thấy hóa đơn của INV-B.
4. **Hóa đơn cọc:** landlord bấm **Tạo hóa đơn cọc** cho INV-A, xem chi tiết 1.000.000đ rồi phát hành. Tenant A thấy hóa đơn cọc; tenant B không thấy. Nếu làm trước bước 2, tenant A chỉ thấy cọc đã phát hành, chưa thấy hóa đơn tháng còn là nháp.
5. **Kiểm tra lỗi trùng:** tạo lại hóa đơn tháng 11 của INV-A khi bản cũ chưa hủy → nhận `409 INVOICE_ALREADY_EXISTS`. Ở tab dữ liệu, thử nhập lại chỉ số điện INV-A tháng 11 → nhận `409 METER_READING_CONFLICT`. Thử tạo giá điện chồng ngày với 01/11–31/12 → nhận `409 UTILITY_RATE_OVERLAP`.
6. **Admin và quyền:** đăng nhập Admin → **Dịch vụ** → tạo một dịch vụ mới. Tenant không có trang quản lý giá/hóa đơn của landlord; gọi API landlord bằng token tenant phải bị từ chối. Landlord chỉ thấy khu và hợp đồng của mình.
7. **Hủy hóa đơn:** khi chưa có giao dịch thanh toán, landlord có thể hủy một hóa đơn. Hóa đơn hủy không còn hiện cho tenant. Làm kịch bản này cuối cùng nếu muốn giữ các hóa đơn đã phát hành để quan sát.

Các form không ghi nhận thanh toán hay điều chỉnh cộng/trừ tiền vì backend mới triển khai chương 2–3; đó là phạm vi chương 4–5. Các khoảng giá/dịch vụ kết thúc vào ngày xác định để tránh bị chặn khi thử giá mới; backend hiện chưa có API sửa ngày kết thúc của khoảng đã tạo.

## 4. Vị trí mã liên quan

- Seed có thể chạy lại trên database trống ở `backend/tools/InvoiceDemoSeeder/Program.cs`; nếu tài khoản demo đã tồn tại, tool dừng và không đổi mật khẩu.
- API frontend ở `frontend/src/services/api/billingApi.js`; giao diện landlord ở `frontend/src/components/billing/LandlordBilling.jsx`, dữ liệu đầu vào ở `BillingInputs.jsx`, giao diện tenant ở `TenantBilling.jsx`.
- Test SQL ở `backend/tests/RoomManagement.Tests/BillingInvoiceStoreTests.cs` kiểm tra tạo/phát hành, số dư, giá chồng, chỉ số đứt chuỗi và phạm vi xem hóa đơn của tenant.
