# Kế hoạch thay đổi tài khoản người thuê và xác nhận hợp đồng

**Trạng thái:** kế hoạch PM đã cập nhật theo các quyết định nghiệp vụ; các chi tiết dữ liệu cũ và cơ chế xác minh về sau nằm ở mục 8. Kế hoạch này cập nhật [kế hoạch phân quyền Manager](manager-property-permissions-plan.md), không thay đổi trực tiếp mã nguồn.

## 1. Mục tiêu và quy tắc đã chốt

1. Người thuê (`Tenant`) tự tạo tài khoản; không cần Landlord hoặc Manager tạo hồ sơ trước, không cần mã chủ trọ để đăng ký.
2. Landlord/Manager chỉ quản lý quan hệ thuê của người có hợp đồng trong Property họ được phép quản lý. Manager chỉ được thao tác Property đang được giao.
3. Khi lập hợp đồng, Landlord/Manager nhập **cả SĐT và CCCD** để tìm chính xác tài khoản Tenant, xem thông tin tối thiểu rồi chọn vào hợp đồng. Không cho duyệt danh sách toàn bộ Tenant.
4. **Người thuê đại diện** được xem đề nghị và xác nhận đồng ý **cả nội dung hợp đồng lẫn nội quy khu trọ**. Hợp đồng chỉ có hiệu lực sau khi người đại diện và phía Landlord/Manager hoàn tất xác nhận.
5. Landlord/Manager không được tạo hoặc xóa tài khoản Tenant. Họ được kết thúc hợp đồng, cho người thuê rời phòng và tiếp tục xử lý các nghiệp vụ hợp đồng/tài chính trong phạm vi Property. Việc quản trị tài khoản Tenant thuộc Admin.
6. Kết thúc hợp đồng hoặc rời phòng không xóa tài khoản Tenant và không xóa lịch sử hợp đồng.
7. Tenant được đăng ký bằng SĐT và CCCD **tự khai**, chưa cần SMS/email để sử dụng tài khoản và xác nhận hợp đồng. Việc xác minh danh tính được bổ sung sau; trạng thái chưa xác minh phải được lưu và hiển thị rõ.
8. Một tài khoản Tenant dùng xuyên suốt khi thuê qua nhiều Landlord, kể cả ở các thời điểm khác nhau. Landlord/Manager được kết thúc hợp đồng hoặc cho rời phòng và thông báo; không cần Tenant xác nhận bước kết thúc.

**Làm rõ thuật ngữ:** tài khoản `Users` là danh tính đăng nhập; hồ sơ định danh Tenant là dữ liệu của người thuê; hợp đồng và người tham gia hợp đồng là quan hệ thuê gắn Property. Quyền của Landlord/Manager đến từ **quan hệ thuê**, không phải quyền sở hữu tài khoản Tenant.

## 2. Ma trận quyền mục tiêu

| Hành động | Admin | Landlord | Manager | Tenant |
| --- | --- | --- | --- | --- |
| Tự đăng ký Tenant | Theo quyền quản trị hiện có | Không | Không | Có |
| Khóa, mở khóa, xóa/quản trị tài khoản Tenant | Có | Không | Không | Không |
| Tra cứu Tenant bằng đúng SĐT + CCCD để lập hợp đồng | Không cần cho luồng này | Có | Có, trong Property được giao | Không |
| Tạo/gửi đề nghị hợp đồng, gắn Tenant vào hợp đồng | Không cần cho luồng này | Property sở hữu | Property được giao | Không |
| Xem đề nghị và xác nhận hợp đồng cùng nội quy | Không | Không | Không | Chỉ người thuê đại diện của đề nghị |
| Đối ký/kích hoạt hợp đồng | Không cần cho luồng này | Property sở hữu | Property được giao | Không |
| Xem, xử lý hợp đồng và người thuê đang/đã liên kết | Theo chức năng Admin hiện có | Property sở hữu | Property được giao | Hợp đồng của mình |
| Kết thúc hợp đồng, cho rời phòng và thông báo | Không cần cho luồng này | Property sở hữu | Property được giao | Nhận thông báo; không cần đối ký |

Không thêm quyền Admin ngoài phạm vi quản trị tài khoản đã yêu cầu. Người ở cùng hoặc bên thuê không phải đại diện không được bấm xác nhận thay người đại diện.

## 3. Luồng nghiệp vụ đề xuất

### 3.1. Tenant tự đăng ký

1. Tenant cung cấp thông tin tài khoản và định danh cần thiết (ít nhất SĐT, CCCD, mật khẩu và dữ liệu hồ sơ bắt buộc theo quy tắc hiện có).
2. Hệ thống chuẩn hóa SĐT/CCCD, kiểm tra trùng tài khoản và tạo danh tính Tenant độc lập với Landlord.
3. Cho đăng ký bằng SĐT và CCCD tự khai; đặt trạng thái `Unverified` và không ghi nhận là đã xác minh. Tài khoản này vẫn được dùng để xác nhận hợp đồng theo quyết định hiện tại. Xác nhận chỉ chứng minh **tài khoản đã bấm đồng ý**, chưa chứng minh người đó thật sự sở hữu SĐT hoặc CCCD.
4. Tenant đăng nhập, xem và cập nhật thông tin bản thân theo chính sách Admin; không được tự đổi danh tính trên hợp đồng đã xác nhận mà không có luồng xử lý riêng. Một `UserId` được dùng xuyên suốt giữa các Landlord, còn từng Landlord chỉ thấy quan hệ thuê với Property của mình.

### 3.2. Tìm và mời vào hợp đồng

1. Landlord/Manager chọn Property và phòng mà mình có quyền, tạo nháp hợp đồng nội bộ.
2. Nhập **đúng cặp SĐT + CCCD**. API chỉ trả kết quả khớp chính xác và dữ liệu tối thiểu cần đối chiếu; không cung cấp tìm gần đúng, danh sách hoặc phân trang Tenant toàn hệ thống.
3. Chọn tài khoản Tenant làm bên thuê, gửi đề nghị hợp đồng. Từ thời điểm gửi, Tenant mới được xem đề nghị của chính mình. Tra cứu trúng tài khoản **chưa** tự cấp quyền quản lý người thuê cho Landlord/Manager.
4. Kiểm tra quyền Property ở backend trước khi mời. Manager không thể dùng ID Tenant/Contract để lập hợp đồng cho Property ngoài phân công.
5. Giới hạn tần suất tra cứu, ghi audit người tra cứu và trả lỗi thống nhất cho trường hợp không khớp; tránh API trở thành công cụ dò SĐT/CCCD. CCCD không xuất hiện đầy đủ trong danh sách, log hoặc thông báo lỗi.

### 3.3. Xác nhận hai phía

Đề xuất trạng thái: `Draft` (nháp nội bộ) → `PendingTenantConfirmation` (đã gửi) → `PendingOperatorConfirmation` (bên thuê đã xác nhận) → `Active` (phía chủ trọ đối ký); giữ các trạng thái kết thúc/tất toán hiện có. Cần ánh xạ vào enum/trạng thái hiện tại thay vì thêm trùng nếu đã có trạng thái tương đương.

- Khi gửi đề nghị, lưu phiên bản bất biến của điều khoản hợp đồng và nội quy áp dụng, gồm Property, phòng, các bên ký, thời hạn, tiền thuê, cọc và các điều khoản khác.
- Chỉ **người thuê đại diện** của đề nghị xem toàn bộ nội dung và bấm xác nhận đồng ý cả hợp đồng lẫn nội quy. Lưu `UserId`, thời điểm, phiên bản/hash điều khoản và phiên bản nội quy đã xác nhận. Người ở cùng và bên thuê không phải đại diện không cần bấm xác nhận.
- Landlord hoặc Manager có quyền trên Property kiểm tra xác nhận của người đại diện rồi đối ký/kích hoạt. Không kích hoạt nếu thiếu xác nhận này, phiên bản nội quy/điều khoản đã thay đổi, phòng không còn khả dụng hoặc có xung đột thuê. Điều kiện hiện có buộc mọi `ContractParty` chấp nhận nội quy cần được điều chỉnh theo quyết định chỉ người đại diện xác nhận.
- Sửa nội dung có tác động đến thỏa thuận sau khi gửi phải tạo phiên bản mới và yêu cầu Tenant xác nhận lại. Không sửa âm thầm bản đã xác nhận.
- Tenant chỉ xem đề nghị gửi cho chính tài khoản mình; nháp nội bộ không hiển thị cho Tenant.

### 3.4. Quản lý và kết thúc quan hệ thuê

- Landlord/Manager xem người thuê **qua hợp đồng của Property được phép truy cập**, kể cả hợp đồng đã kết thúc nếu cần xử lý lịch sử, hóa đơn hoặc hoàn cọc. Không có danh sách hồ sơ Tenant toàn hệ thống trong giao diện vận hành.
- Landlord/Manager kết thúc hợp đồng, tất toán/hoàn cọc và cho người thuê rời phòng theo quy trình hiện có; ghi người thực hiện và thời điểm, gửi thông báo cho Tenant. Không cần Tenant xác nhận bước kết thúc. Tài khoản Tenant vẫn hoạt động và Tenant vẫn xem được lịch sử của mình.
- Admin quản trị vòng đời tài khoản Tenant. Việc khóa/xóa tài khoản cần giữ tham chiếu hợp đồng, hóa đơn, xác nhận và chứng từ; không xóa dây chuyền lịch sử nghiệp vụ.

## 4. Đối chiếu mã nguồn hiện tại

- `TenantRegistrationService` hiện yêu cầu `LandlordCode`, hồ sơ Tenant do Landlord tạo trước và luồng OTP SMS; username có dạng `LandlordCode:phone`. Cần thay bằng đăng ký Tenant độc lập.
- `TenantProfiles.UserId` hiện unique, hồ sơ còn gắn `OwnerLandlordUserId`. Cần tách định danh Tenant dùng chung khỏi liên kết hợp đồng/Property. Tránh để một Tenant phải tạo tài khoản mới khi đổi Landlord nếu quyết định cho phép thuê nhiều chủ trọ.
- `ContractStore.AddUserAsync` đang tìm theo email; `AddTenantAsync` nhận ID hồ sơ. Chưa có tra cứu exact match bằng SĐT + CCCD.
- `ContractStore.Mine()` không trả `Draft`, trong khi Tenant cần xem đề nghị đã gửi. Cần trạng thái đề nghị riêng và truy vấn chỉ trả đề nghị của chính Tenant.
- `ActivateSafelyAsync` hiện yêu cầu các `ContractParty` chấp nhận nội quy hiện hành. Cần đổi điều kiện này sang xác nhận của **người thuê đại diện**. `ContractRulesAcceptances` chỉ ghi nhận nội quy; cần bổ sung bằng chứng đồng ý phiên bản hợp đồng.
- `AdminUsersController` chưa phải bộ API đầy đủ để quản trị vòng đời tài khoản Tenant; cần bổ sung các thao tác Admin phù hợp.
- Vai trò Manager đang được bổ sung trong working tree. Khi triển khai cần dùng và hoàn thiện kiểm tra theo Property hiện có, không ghi đè các thay đổi đang làm.

## 5. Dữ liệu và API cần thiết kế

1. **Danh tính Tenant:** tài khoản `Users` độc lập; hồ sơ cá nhân gắn `UserId`, không gắn sở hữu độc quyền vào Landlord. Một tài khoản có thể tham gia hợp đồng với nhiều Landlord theo thời gian. Quy tắc duy nhất và thay đổi SĐT/CCCD phải tính đến hợp đồng đang hoạt động; lưu trạng thái `Unverified` và nâng cấp xác minh sau.
2. **Quan hệ thuê:** liên kết `Contract`–`Tenant UserId` (và vai trò bên thuê) là nguồn quyền vận hành. Lưu người mời, người xác nhận, người đối ký, thời điểm và trạng thái.
3. **Phiên bản thỏa thuận:** lưu snapshot/hash nội dung hợp đồng và phiên bản nội quy lúc gửi; bản ghi xác nhận của người thuê đại diện và bản ghi đối ký của Landlord/Manager gắn chính xác phiên bản. Có quy tắc vô hiệu hóa xác nhận khi nội dung thay đổi.
4. **API Tenant:** tự đăng ký, xem/cập nhật tài khoản bản thân, xem đề nghị, xác nhận/từ chối đề nghị, xem hợp đồng và lịch sử của mình.
5. **API Landlord/Manager:** tra cứu SĐT+CCCD, lập/gửi/hủy đề nghị, xem trạng thái xác nhận, đối ký, kết thúc hợp đồng, rời phòng; tất cả gắn kiểm tra Property.
6. **API Admin:** tìm, khóa/mở và xử lý tài khoản Tenant; thao tác xóa phải giữ tính toàn vẹn lịch sử hợp đồng và chứng từ.
7. **Migration:** thống kê hồ sơ có/không có `UserId`, hồ sơ trùng SĐT/CCCD và hồ sơ gắn nhiều Landlord; backfill liên kết hợp đồng trước khi bỏ bộ lọc hồ sơ theo Landlord. Không tự hợp nhất dữ liệu mâu thuẫn. Duy trì khả năng đọc hợp đồng cũ trong giai đoạn chuyển đổi.

## 6. Thứ tự triển khai

1. **Kiểm kê dữ liệu hiện có.** Xác định quy tắc duy nhất của SĐT/CCCD, số hồ sơ trùng/mâu thuẫn và cách giữ hợp đồng cũ. Quy tắc xác nhận và kết thúc đã được chốt ở mục 1.
2. **Danh tính và đăng ký Tenant.** Tạo tài khoản độc lập, trạng thái xác minh, API tự đăng ký và chuyển đổi dữ liệu cũ; tránh gián đoạn đăng nhập của Tenant hiện tại.
3. **Tra cứu và phạm vi quyền.** API tìm cặp SĐT+CCCD với giới hạn dữ liệu; thay quyền danh sách hồ sơ theo Landlord bằng quyền phát sinh từ hợp đồng/Property; cập nhật Manager.
4. **Vòng đời đề nghị hợp đồng.** Trạng thái, bản chụp điều khoản, hiển thị cho người thuê đại diện, xác nhận hợp đồng cùng nội quy, đối ký/kích hoạt và reset xác nhận khi sửa.
5. **Quản trị và kết thúc.** Admin quản lý tài khoản; Landlord/Manager chỉ kết thúc hợp đồng/rời phòng; giữ audit và lịch sử.
6. **Giao diện và kiểm thử.** Màn hình đăng ký, tìm người thuê, gửi đề nghị, Tenant xác nhận, theo dõi hai bên, kết thúc thuê và quản trị Admin.

## 7. Tiêu chí nghiệm thu

- Tenant tự đăng ký và đăng nhập mà không cần mã chủ trọ hoặc hồ sơ do Landlord tạo trước; không thể chiếm tài khoản đã có chỉ bằng cách biết SĐT/CCCD.
- Landlord/Manager không có API hoặc nút xóa tài khoản Tenant; Admin quản lý tài khoản mà không làm mất lịch sử hợp đồng.
- Tra cứu chỉ khớp đúng cặp SĐT+CCCD, không lộ danh sách người dùng; Manager không tạo được hợp đồng ở Property ngoài phân công.
- Chỉ người thuê đại diện thấy đề nghị gửi cho mình và xác nhận cả hợp đồng lẫn nội quy. Hợp đồng không thể kích hoạt nếu thiếu xác nhận đại diện hoặc phiên bản thỏa thuận đã thay đổi.
- Landlord/Manager chỉ thấy và quản lý thông tin người thuê trong hợp đồng thuộc Property được phép truy cập; được kết thúc hợp đồng/rời phòng rồi thông báo, không chờ Tenant xác nhận và không xóa tài khoản hoặc lịch sử của Tenant.
- Tenant có thể dùng cùng tài khoản để tham gia hợp đồng của Landlord khác; mỗi Landlord chỉ thấy hợp đồng và thông tin thuộc Property của mình.
- Quy trình hiện có về hóa đơn, thanh toán, hoàn tiền dư và hoàn cọc tiếp tục hoạt động với hợp đồng mới lẫn hợp đồng đã chuyển đổi.

## 8. Chi tiết cần xử lý khi triển khai

1. **Hồ sơ cũ trùng hoặc mâu thuẫn:** thống kê trước migration; Admin xử lý thủ công các trường hợp không thể gắn chắc chắn vào một tài khoản. Không tự hợp nhất theo CCCD tự khai.
2. **Người chưa thành niên/người ở cùng:** không bắt họ xác nhận online; lưu vai trò rõ ràng và yêu cầu người thuê đại diện chịu trách nhiệm xác nhận cho đề nghị. Nếu cần quy trình người giám hộ riêng, thiết kế thành giai đoạn tiếp theo.
3. **Xác minh về sau:** thiết kế khả năng bổ sung OTP/đối chiếu danh tính; dữ liệu xác nhận tạo khi tài khoản `Unverified` không được trình bày như bằng chứng đã xác minh danh tính.
4. **Thông báo kết thúc:** chốt kênh thông báo thực tế khi có hạ tầng; trước mắt phải có thông báo trong ứng dụng và ghi thời điểm tạo thông báo. Không để việc chưa cấu hình SMS/email chặn kết thúc hợp đồng.
