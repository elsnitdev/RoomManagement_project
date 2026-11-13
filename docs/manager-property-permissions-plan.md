# Kế hoạch phân quyền Quản lý dãy (Manager)

## 1. Mục tiêu và phạm vi

Thêm vai trò `Manager` bên cạnh `Admin`, `Landlord`, `Tenant`. Phạm vi quản lý là **cả khu trọ (`Property`)**, không phải một `RoomRow`. Mỗi Manager thuộc đúng **một Landlord**, có thể được giao nhiều Property của Landlord đó; một Property có thể có nhiều Manager. Manager không được xem hoặc tác động tài khoản Manager khác.

Landlord vẫn sở hữu Property và toàn quyền quản lý Manager của mình. Phần đăng ký, tra cứu và quản lý người thuê được thay đổi theo [kế hoạch tài khoản người thuê và xác nhận hợp đồng](tenant-self-registration-contract-confirmation-plan.md); tài liệu đó thay thế mọi mô tả cũ về quyền tạo hồ sơ người thuê ở đây.

## 2. Quyết định nghiệp vụ đã chốt

| Nghiệp vụ | Landlord | Manager |
| --- | --- | --- |
| Tạo Property | Có | Không |
| Xem, sửa Property | Property sở hữu | Chỉ Property được giao |
| Xóa Property; đổi ngân hàng, số tài khoản, tên chủ tài khoản nhận tiền | Có | Không |
| Quản lý dãy/phòng, ảnh, dịch vụ, điện nước, nội quy | Property sở hữu | Chỉ Property được giao |
| Tạo và xử lý hợp đồng, hóa đơn, thanh toán | Property sở hữu | Chỉ Property được giao |
| Hoàn tiền dư hóa đơn và hoàn cọc hợp đồng | Có | Chỉ Property được giao |
| Tìm ứng viên thuê bằng SĐT và CCCD để lập hợp đồng | Có, theo quy trình tra cứu giới hạn | Có, cho Property được giao |
| Quản lý người thuê trong hợp đồng; kết thúc hợp đồng, cho rời phòng | Hợp đồng thuộc Property sở hữu | Hợp đồng thuộc Property được giao |
| Xóa tài khoản người thuê | Không; thuộc Admin | Không; thuộc Admin |
| Xem nghiệp vụ liên kết người thuê (phòng, hợp đồng, hóa đơn, lịch sử tài chính) | Chỉ hợp đồng thuộc Property sở hữu | Chỉ hợp đồng thuộc Property được giao |
| Tạo, phân công, thu hồi, khóa hoặc xóa Manager | Manager thuộc mình | Không |
| Xem thông tin Manager cùng cấp | Có | Không |

Không có quyền ngầm theo vai trò: Manager chỉ được thao tác tài nguyên khi **phân công Property đang hiệu lực**. Property mới của Landlord không tự động xuất hiện trong danh sách của Manager.

## 3. Luồng người dùng

### 3.1. Tạo tài khoản và phân công

1. Landlord tạo hồ sơ/tài khoản Manager thuộc chính mình; tên đăng nhập phải duy nhất.
2. Landlord giao một hoặc nhiều Property mà mình sở hữu. Hệ thống từ chối Property của Landlord khác và phân công trùng đang hiệu lực.
3. **Giải pháp tạm khi chưa có email/OTP:** Landlord đặt mật khẩu tạm lúc tạo tài khoản và bàn giao trực tiếp cho Manager. Manager đăng nhập, bắt buộc đổi sang mật khẩu tự chọn trước khi vào nghiệp vụ. Tài khoản chưa đổi mật khẩu tạm không được dùng các API nghiệp vụ. Mật khẩu chỉ lưu dạng hash và không hiển thị lại sau khi tạo.
4. Luồng gửi email/OTP và tự kích hoạt qua kênh liên lạc để ở giai đoạn sau. Không dùng mã OTP cố định `1111` trong bản triển khai có người dùng thật; nếu cần dữ liệu demo, chỉ cấu hình mã này riêng cho môi trường phát triển.

### 3.2. Làm việc hằng ngày

- Manager chỉ thấy danh sách Property được giao và có thể chọn một Property làm ngữ cảnh giao diện. Backend vẫn xác minh Property của **từng yêu cầu**, kể cả khi giao diện đã chọn đúng khu.
- Manager tìm tài khoản người thuê đã tự đăng ký bằng cặp SĐT và CCCD, chọn người thuê để lập hợp đồng trong Property được giao. Manager chỉ quản lý quan hệ thuê sau khi người thuê được liên kết vào hợp đồng thuộc phạm vi của mình.
- Khi tạo hợp đồng hoặc hoàn tiền, hệ thống lưu cả Landlord sở hữu, Property liên quan và tài khoản thực sự thực hiện thao tác.

### 3.3. Thu hồi, khóa và xóa

- Landlord có thể thu hồi từng Property, khóa tài khoản Manager hoặc xóa Manager khỏi danh sách quản lý. Thu hồi/khóa có hiệu lực với **yêu cầu tiếp theo ngay trong phiên đăng nhập hiện tại**.
- `Xóa Manager` trên giao diện cần giữ được lịch sử hợp đồng, hoàn tiền và các dấu vết thao tác: vô hiệu hóa/ẩn tài khoản hoặc xóa vật lý chỉ khi không có dữ liệu tham chiếu. Không xóa dây chuyền các chứng từ nghiệp vụ.
- Manager không thể tự khôi phục phân công, tự đổi Landlord sở hữu, tự thêm Property hay xem tài khoản Manager khác bằng cách gọi API trực tiếp.

## 4. Hiện trạng mã nguồn và tác động

- Vai trò Manager và kiểm tra `LandlordOrManager` đang được bổ sung trong mã nguồn; cần đối chiếu kế hoạch này với phần triển khai đang dở, không áp dụng lại migration hoặc sửa chồng các thay đổi hiện có.
- `Property.OwnerUserId` là ID Landlord; các service/store đang lọc `OwnerUserId` bằng ID người đăng nhập. Mở route cho Manager mà không sửa lọc dữ liệu sẽ làm nghiệp vụ lỗi hoặc vượt phạm vi.
- Mô hình `TenantProfile` theo Landlord và đăng ký người thuê theo mã chủ trọ không còn phù hợp với tài khoản người thuê tự đăng ký độc lập. Cần chuyển sang danh tính người thuê toàn hệ thống và quyền quản lý phát sinh từ hợp đồng.
- Một số API hóa đơn, thanh toán và hợp đồng nhận ID tài nguyên mà không có `propertyId` trong đường dẫn. Phải truy ngược tài nguyên về Property và kiểm tra quyền trước khi đọc hoặc ghi.
- Hoàn tiền dư hóa đơn đi qua `LandlordPaymentsController`/`RecordOverpaymentRefundAsync`. Hoàn cọc đi qua `ContractsController.CompleteSettlement` và `SettlementRefundEvidenceController`; cả hai luồng đều nằm trong phạm vi quyền Manager.

## 5. Thiết kế dữ liệu và kiểm tra quyền

1. Bổ sung `Manager` vào vai trò và ràng buộc `Users.Role` trong cơ sở dữ liệu.
2. Lưu quan hệ bắt buộc `Manager -> Landlord` (một Manager, một Landlord). Không cho chuyển một tài khoản Manager sang Landlord khác bằng API phân công.
3. Tạo `PropertyManagerAssignments` gồm tối thiểu `ManagerUserId`, `PropertyId`, `AssignedByUserId`, `AssignedAt`, `RevokedAt`; ràng buộc không có hai phân công đang hoạt động cho cùng cặp Manager–Property. Kiểm tra Property thuộc Landlord của Manager lúc gán.
4. Tách ngữ cảnh quyền thành `ActorUserId`, `OwnerLandlordUserId`, `PropertyId` và quyền thao tác. Không thay ID Manager bằng ID Landlord trong `ICurrentLandlord`, vì như vậy mất dấu người thực hiện.
5. Quyền xem/quản lý người thuê của Landlord và Manager phải xuất phát từ hợp đồng thuộc Property được phép truy cập. Không dùng riêng `TenantProfiles.OwnerLandlordUserId` hoặc `CreatedByUserId` để cấp quyền quản lý tài khoản người thuê.
6. Kiểm tra phân công và trạng thái tài khoản từ dữ liệu hiện hành ở mỗi yêu cầu liên quan Manager. JWT chỉ xác thực danh tính/vai trò; danh sách Property trong token không được dùng làm nguồn quyền duy nhất. Sau khi giao dịch thu hồi hoàn tất, yêu cầu tiếp theo phải thất bại.
7. Mọi truy vấn danh sách phải lọc phạm vi **trước phân trang và tính tổng**. Mọi API chi tiết/cập nhật/xóa theo ID phải xác định Property gốc, kiểm tra quyền rồi mới trả dữ liệu hoặc thay đổi trạng thái.
8. DTO cập nhật Property dành cho Manager không nhận hoặc không ghi đè trường ngân hàng. Endpoint xóa Property và các endpoint quản lý Manager chỉ cho Landlord hợp lệ.

## 6. Kế hoạch triển khai theo giai đoạn

### Giai đoạn A — Nền tảng tài khoản và dữ liệu

- Đối chiếu migration Manager đang được triển khai; bổ sung phần còn thiếu của role, quan hệ Manager–Landlord, phân công, trạng thái khóa và trạng thái buộc đổi mật khẩu. Migration danh tính người thuê nằm trong kế hoạch liên kết ở trên.
- API Landlord tạo, liệt kê, giao/thu hồi Property, khóa/xóa Manager; API Manager đổi mật khẩu tạm ở lần đăng nhập đầu.
- Ràng buộc chủ sở hữu Property và xử lý dữ liệu Manager hiện có; dữ liệu người thuê cũ cần lộ trình chuyển đổi riêng.

### Giai đoạn B — Quyền theo tài nguyên ở backend

- Tạo bộ kiểm tra quyền dùng chung cho Property và tài nguyên con; cập nhật các controller/service/store nghiệp vụ đang chỉ hỗ trợ Landlord.
- Rà soát riêng API nhận `contractId`, `invoiceId`, `paymentId`, `tenantProfileId`; quyền đối với người thuê phải dựa trên hợp đồng và Property, không dựa trên danh sách hồ sơ của Landlord.
- Tách actor và owner trong tạo hợp đồng, thanh toán, hoàn tiền và chứng từ hoàn cọc.

### Giai đoạn C — Giao diện

- Landlord: danh sách Manager, tạo tài khoản, giao nhiều Property, thu hồi, khóa/xóa, xem trạng thái.
- Manager: đổi mật khẩu tạm, danh sách/chọn Property được giao, màn hình nghiệp vụ tương ứng; ẩn nút tạo/xóa Property, sửa ngân hàng và quản lý Manager.
- Giao diện chỉ giúp tránh thao tác nhầm; backend quyết định quyền cuối cùng.

### Giai đoạn D — Kiểm thử và nghiệm thu

- Ma trận kiểm thử cho cả Landlord và Manager trên mọi API nghiệp vụ, gồm danh sách, chi tiết và ghi dữ liệu.
- Kiểm thử gọi trực tiếp bằng ID của Property, phòng, hợp đồng, hóa đơn, thanh toán ngoài phạm vi; không lộ dữ liệu trong kết quả, tổng số hay thông báo lỗi.
- Kiểm thử Manager tìm và chọn người thuê đã đăng ký, tạo hợp đồng, hoàn tiền dư và hoàn cọc trong Property được giao; Landlord vẫn thấy dữ liệu và biết ai đã thực hiện.
- Kiểm thử Manager không thấy Manager khác, không tạo/xóa Property, không đổi ngân hàng.
- Kiểm thử thu hồi một Property và khóa tài khoản khi Manager còn đăng nhập: yêu cầu tiếp theo bị từ chối, các Property còn được giao vẫn dùng được nếu tài khoản chưa bị khóa.
- Kiểm thử tài khoản mới chỉ được đổi mật khẩu tạm trước khi dùng nghiệp vụ.

## 7. Tiêu chí hoàn thành

Chức năng hoàn thành khi Manager chỉ truy cập được Property được giao trên **toàn bộ API đọc và ghi**; có thể chọn người thuê đã đăng ký, tạo hợp đồng và xử lý cả hai loại hoàn tiền trong phạm vi; không thấy Manager cùng cấp; không xóa tài khoản người thuê hoặc thực hiện thao tác dành riêng cho Landlord; Landlord có thể giao/thu hồi/khóa quyền và hiệu lực được áp dụng cho yêu cầu kế tiếp mà không cần Manager đăng nhập lại. Lịch sử nghiệp vụ vẫn truy được sau khi Manager bị xóa/khóa.
