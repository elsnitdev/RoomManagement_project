# Mô tả các bảng trong cơ sở dữ liệu RoomManagement

**Nguồn:** [`room-management-current-schema.dbml`](room-management-current-schema.dbml), được tạo từ `RoomManagementDbContextModelSnapshot.cs` đến migration `20261109163319_ActivePropertyRulesVersion`. Phần tính số dư của hai view được đối chiếu thêm với SQL định nghĩa view trong các migration.

Tài liệu bao gồm **31 bảng vật lý và 2 SQL view**. Mỗi dòng dưới mục **Liên hệ và tác động** nói về đúng một bảng hoặc view khác:

- **FK**: quan hệ khóa ngoại trực tiếp; mũi tên `A.cột → B.cột` chỉ bản ghi A tham chiếu B. **FK ngược** là quan hệ nhìn từ phía B.
- **Nghiệp vụ, không FK**: dữ liệu được dùng trong một luồng xử lý hoặc phép tính, nhưng cơ sở dữ liệu không ràng buộc hai bảng bằng khóa ngoại.
- Các dòng “tác động” diễn tả phụ thuộc dữ liệu và kết quả nghiệp vụ; chúng **không có nghĩa** sửa một bảng sẽ tự động sửa bảng kia. Phần lớn FK trong schema dùng `NO ACTION`, còn thao tác thêm/sửa/xóa do ứng dụng quyết định.

## 1. Tài khoản, khu trọ và phòng

### `Users`

Lưu tài khoản đăng nhập, vai trò Admin/Chủ trọ/Người thuê, thông tin cá nhân và trạng thái duyệt tài khoản. Đây là bảng danh tính được nhiều nghiệp vụ dùng để ghi nhận chủ sở hữu hoặc người thao tác.

**Liên hệ và tác động:**

- `Properties` — **FK ngược** `Properties.OwnerUserId → Users.Id`: xác định chủ sở hữu khu trọ.
- `TenantProfiles` — **FK ngược** `TenantProfiles.CreatedByUserId/UserId → Users.Id`: ghi người tạo hồ sơ và tài khoản được liên kết với hồ sơ người thuê.
- `TenantPhoneChallenges` — **FK ngược** `TenantPhoneChallenges.LandlordId → Users.Id`: giới hạn thử thách xác minh số điện thoại theo chủ trọ.
- `ContractRulesVersions` — **FK ngược** `ContractRulesVersions.CreatedByUserId → Users.Id`: ghi người tạo phiên bản nội quy.
- `ContractRulesAcceptances` — **FK ngược** `ContractRulesAcceptances.AcceptedByUserId → Users.Id`: ghi tài khoản chấp nhận nội quy.
- `ContractRulesEvidence` — **FK ngược** `ContractRulesEvidence.UploadedByUserId → Users.Id`: ghi tài khoản tải bằng chứng nội quy lên.
- `MemberCommitments` — **FK ngược** `MemberCommitments.ConfirmedByUserId → Users.Id`: ghi người xác nhận cam kết của thành viên.
- `InvoiceAdjustments` — **FK ngược** `CreatedByUserId/VoidedByUserId → Users.Id`: truy vết người tạo và người hủy điều chỉnh hóa đơn.
- `Payments` — **FK ngược** `SubmittedByUserId/VerifiedByUserId → Users.Id`: truy vết người khai và người xác nhận khoản tiền.
- `PaymentEvidence` — **FK ngược** `PaymentEvidence.UploadedByUserId → Users.Id`: ghi người tải bằng chứng thanh toán lên.
- `PaymentReviewEvents` — **FK ngược** `PaymentReviewEvents.ReviewedByUserId → Users.Id`: ghi người duyệt từng lần thay đổi trạng thái thanh toán.
- `Refunds` — **FK ngược** `Refunds.CreatedByUserId → Users.Id`: ghi người tạo yêu cầu hoàn tiền.
- `DepositApplications` — **FK ngược** `DepositApplications.AppliedByUserId → Users.Id`: ghi người thực hiện cấn trừ cọc.
- `TenantRequests` — **FK ngược** `TenantRequests.AssignedToUserId → Users.Id`: ghi tài khoản được giao xử lý yêu cầu.

### `Properties`

Lưu từng khu trọ, địa chỉ, chủ sở hữu, trạng thái hoạt động và thông tin tài khoản ngân hàng nhận tiền. Khu trọ là phạm vi tổ chức phòng, giá điện nước và nội quy chung.

**Liên hệ và tác động:**

- `Users` — **FK** `Properties.OwnerUserId → Users.Id`: khu trọ thuộc một tài khoản chủ trọ.
- `Rooms` — **FK ngược** `Rooms.PropertyId → Properties.Id`: các phòng nằm trong khu trọ.
- `RoomRows` — **FK ngược** `RoomRows.PropertyId → Properties.Id`: các hàng/dãy phòng thuộc khu trọ.
- `RoomNumberCounters` — **FK ngược** `RoomNumberCounters.PropertyId → Properties.Id`: bộ đếm số phòng được quản lý theo khu.
- `RoomBatchSubmissions` — **FK ngược** `RoomBatchSubmissions.PropertyId → Properties.Id`: ghi các lần gửi yêu cầu tạo phòng hàng loạt trong khu.
- `UtilityRates` — **FK ngược** `UtilityRates.PropertyId → Properties.Id`: đơn giá điện/nước áp dụng theo khu và thời gian.
- `ContractRulesVersions` — **FK ngược** `ContractRulesVersions.PropertyId → Properties.Id`: phiên bản nội quy có thể áp dụng cho toàn khu.

### `RoomRows`

Lưu hàng/dãy phòng trong một khu trọ, gồm mã, tên và trạng thái sử dụng. Bảng này giúp nhóm phòng theo vị trí khi chủ trọ tổ chức hoặc tạo phòng hàng loạt.

**Liên hệ và tác động:**

- `Properties` — **FK** `RoomRows.PropertyId → Properties.Id`: mỗi hàng/dãy thuộc một khu trọ.
- `Rooms` — **FK ngược** `Rooms.RowId → RoomRows.Id`: phòng có thể được xếp vào một hàng/dãy; quan hệ này là tùy chọn.

### `Rooms`

Lưu phòng thuộc khu trọ, số phòng, giá cơ bản, diện tích, sức chứa và trạng thái. Phòng là đối tượng trung tâm cho hợp đồng, chỉ số điện nước, dịch vụ và hình ảnh.

**Liên hệ và tác động:**

- `Properties` — **FK** `Rooms.PropertyId → Properties.Id`: xác định khu trọ và phạm vi sở hữu của phòng.
- `RoomRows` — **FK** `Rooms.RowId → RoomRows.Id`: phòng có thể thuộc một hàng/dãy.
- `Contracts` — **FK ngược** `Contracts.RoomId → Rooms.Id`: hợp đồng gắn với phòng được thuê.
- `MeterReadings` — **FK ngược** `MeterReadings.RoomId → Rooms.Id`: lịch sử chỉ số điện/nước được ghi theo phòng.
- `RoomServices` — **FK ngược** `RoomServices.RoomId → Rooms.Id`: dịch vụ được gắn cho từng phòng.
- `RoomImages` — **FK ngược** `RoomImages.RoomId → Rooms.Id`: ảnh minh họa thuộc phòng.
- `Invoices` — **Nghiệp vụ, không có FK trực tiếp tới `Rooms`**: hóa đơn lưu `RoomId` và dùng FK ghép với `Contracts` để bảo đảm phòng khớp hợp đồng.

### `RoomImages`

Lưu siêu dữ liệu ảnh phòng như tên tệp, loại nội dung, dung lượng, vị trí blob và thứ tự hiển thị. Nội dung ảnh thực tế nằm ở kho lưu trữ tệp.

**Liên hệ và tác động:**

- `Rooms` — **FK** `RoomImages.RoomId → Rooms.Id`: ảnh thuộc một phòng cụ thể.
- `BlobCleanupJobs` — **Nghiệp vụ, không FK**: khi xóa ảnh, ứng dụng có thể ghi công việc xóa blob để dọn tệp khỏi kho lưu trữ.

### `BlobCleanupJobs`

Lưu công việc dọn tệp blob chưa hoàn tất, số lần thử và thời điểm thử lại. Bảng này phục vụ tác vụ nền dọn dữ liệu tệp, không lưu ảnh phòng trực tiếp.

**Liên hệ và tác động:**

- `RoomImages` — **Nghiệp vụ, không FK**: công việc xóa blob được tạo khi xử lý xóa ảnh phòng; không có khóa ngoại đến bản ghi ảnh.

### `RoomNumberCounters`

Lưu số thứ tự cuối cùng dùng để đánh số phòng theo khu trọ và phạm vi đánh số. Bảng này giúp tạo mã phòng tuần tự trong luồng tạo phòng hàng loạt.

**Liên hệ và tác động:**

- `Properties` — **FK** `RoomNumberCounters.PropertyId → Properties.Id`: bộ đếm thuộc một khu trọ.
- `Rooms` — **Nghiệp vụ, không FK**: bộ đếm cấp số kế tiếp khi ứng dụng tạo phòng; bản ghi bộ đếm không đại diện cho một phòng cụ thể.

### `RoomBatchSubmissions`

Lưu dấu vết yêu cầu tạo phòng hàng loạt, khóa chống gửi trùng, mã băm yêu cầu và kết quả trả về. Bảng này giúp cùng một yêu cầu không tạo phòng lặp khi gửi lại.

**Liên hệ và tác động:**

- `Properties` — **FK** `RoomBatchSubmissions.PropertyId → Properties.Id`: yêu cầu tạo hàng loạt thuộc một khu trọ.
- `Rooms` — **Nghiệp vụ, không FK**: kết quả của lần gửi có thể chứa các phòng được tạo; bảng không có FK tới từng phòng.
- `Users` — **Nghiệp vụ, không FK**: cột `OwnerUserId` lưu mã chủ trọ để kiểm soát yêu cầu gửi lại, nhưng schema không khai báo FK tới `Users`.

### `TenantProfiles`

Lưu hồ sơ cá nhân của người thuê, có thể được tạo trước khi người thuê có tài khoản đăng nhập. Hồ sơ này dùng để gắn người thuê vào hợp đồng và làm thông tin người gửi yêu cầu.

**Liên hệ và tác động:**

- `Users` — **FK** `CreatedByUserId/UserId → Users.Id`: lần lượt ghi người tạo hồ sơ và tài khoản đăng nhập được gắn với hồ sơ; cả hai cột có thể để trống.
- `ContractTenants` — **FK ngược** `ContractTenants.TenantId → TenantProfiles.Id`: một hồ sơ có thể tham gia các hợp đồng qua các thời kỳ.
- `MemberCommitments` — **FK ngược** `MemberCommitments.SignerTenantProfileId → TenantProfiles.Id`: người ký cam kết có thể được ghi bằng hồ sơ người thuê.
- `TenantRequests` — **FK ngược** `TenantRequests.RequestedByTenantId → TenantProfiles.Id`: yêu cầu/phản ánh gắn với hồ sơ người gửi.
- `TenantPhoneChallenges` — **Nghiệp vụ, không FK**: luồng đăng ký tài khoản dùng số điện thoại và phạm vi chủ trọ để đối chiếu hồ sơ trước khi liên kết với `Users`.

### `TenantPhoneChallenges`

Lưu mã xác minh số điện thoại dạng băm, số lần thử, hạn dùng và thời điểm xác minh/sử dụng trong luồng đăng ký tài khoản người thuê.

**Liên hệ và tác động:**

- `Users` — **FK** `TenantPhoneChallenges.LandlordId → Users.Id`: thử thách xác minh được giới hạn theo chủ trọ.
- `TenantProfiles` — **Nghiệp vụ, không FK**: số điện thoại được đối chiếu với hồ sơ người thuê do chủ trọ tương ứng tạo.

## 2. Hợp đồng và cư trú

### `Contracts`

Lưu hợp đồng thuê phòng, giá thuê, tiền cọc, ngày bắt đầu/kết thúc và trạng thái hợp đồng. Đây là căn cứ xác định người ở, hóa đơn và các nghiệp vụ kết thúc thuê.

**Liên hệ và tác động:**

- `Rooms` — **FK** `Contracts.RoomId → Rooms.Id`: hợp đồng gắn với một phòng.
- `ContractTenants` — **FK ngược** `ContractTenants.ContractId → Contracts.Id`: danh sách người tham gia hợp đồng.
- `ContractDocuments` — **FK ngược** `ContractDocuments.ContractId → Contracts.Id`: các tệp tài liệu của hợp đồng.
- `ContractRulesVersions` — **FK ngược** `ContractRulesVersions.ContractId → Contracts.Id`: phiên bản nội quy riêng có thể thuộc hợp đồng.
- `ContractSettlements` — **FK ngược** `ContractSettlements.ContractId → Contracts.Id`: bản quyết toán khi kết thúc hợp đồng.
- `Invoices` — **FK ngược** `(Invoices.ContractId, RoomId) → (Contracts.Id, RoomId)`: hóa đơn phải thuộc đúng hợp đồng và phòng của hợp đồng đó.
- `DepositApplications` — **FK ngược** `DepositApplications.ContractId → Contracts.Id`: các lần dùng tiền cọc của hợp đồng để bù khoản phải thu.
- `TenantRequests` — **FK ngược** `TenantRequests.ContractId → Contracts.Id`: yêu cầu/phản ánh phát sinh trong hợp đồng.

### `ContractTenants`

Lưu quan hệ giữa hợp đồng và từng hồ sơ người thuê, gồm ngày vào/ra và dấu người liên hệ chính. Bảng này cho phép một hợp đồng có nhiều người ở.

**Liên hệ và tác động:**

- `Contracts` — **FK** `ContractTenants.ContractId → Contracts.Id`: thành viên thuộc một hợp đồng.
- `TenantProfiles` — **FK** `ContractTenants.TenantId → TenantProfiles.Id`: thành viên ứng với một hồ sơ người thuê.
- `MemberCommitments` — **FK ngược** `MemberCommitments.ContractTenantId → ContractTenants.Id`: cam kết của thành viên theo hợp đồng.
- `ContractRulesAcceptances` — **FK ngược** `ContractRulesAcceptances.ContractTenantId → ContractTenants.Id`: ghi nhận thành viên đã chấp nhận phiên bản nội quy nào.

### `ContractDocuments`

Lưu thông tin tệp đính kèm hợp đồng như loại tài liệu, tên tệp và khóa lưu trữ. Dùng để quản lý bản hợp đồng, giấy tờ tùy thân hoặc biên bản liên quan.

**Liên hệ và tác động:**

- `Contracts` — **FK** `ContractDocuments.ContractId → Contracts.Id`: mọi tài liệu gắn với một hợp đồng.

### `ContractRulesVersions`

Lưu nội dung và số phiên bản nội quy, áp dụng ở phạm vi toàn khu trọ hoặc riêng hợp đồng. Ràng buộc kiểm tra yêu cầu mỗi phiên bản chỉ thuộc một trong hai phạm vi đó.

**Liên hệ và tác động:**

- `Properties` — **FK tùy chọn** `ContractRulesVersions.PropertyId → Properties.Id`: nội quy áp dụng cho toàn khu khi có `PropertyId`.
- `Contracts` — **FK tùy chọn** `ContractRulesVersions.ContractId → Contracts.Id`: nội quy áp dụng riêng cho hợp đồng khi có `ContractId`.
- `Users` — **FK** `ContractRulesVersions.CreatedByUserId → Users.Id`: ghi người tạo phiên bản.
- `ContractRulesAcceptances` — **FK ngược** `ContractRulesAcceptances.RulesVersionId → ContractRulesVersions.Id`: các lần người thuê chấp nhận phiên bản này.

### `ContractRulesAcceptances`

Lưu việc một thành viên hợp đồng chấp nhận một phiên bản nội quy, cùng thời điểm và tài khoản thực hiện. Bảng giúp kiểm tra phiên bản nào đã được chấp nhận.

**Liên hệ và tác động:**

- `ContractTenants` — **FK** `ContractRulesAcceptances.ContractTenantId → ContractTenants.Id`: việc chấp nhận thuộc một thành viên hợp đồng.
- `ContractRulesVersions` — **FK** `ContractRulesAcceptances.RulesVersionId → ContractRulesVersions.Id`: xác định phiên bản nội quy được chấp nhận.
- `Users` — **FK** `ContractRulesAcceptances.AcceptedByUserId → Users.Id`: tài khoản thực hiện chấp nhận.
- `ContractRulesEvidence` — **FK ngược** `ContractRulesEvidence.AcceptanceId → ContractRulesAcceptances.Id`: bằng chứng đính kèm lần chấp nhận.

### `ContractRulesEvidence`

Lưu thông tin tệp bằng chứng cho việc chấp nhận nội quy, gồm tên tệp, loại nội dung, dung lượng và vị trí blob. Dữ liệu tệp được lưu ngoài bảng.

**Liên hệ và tác động:**

- `ContractRulesAcceptances` — **FK** `ContractRulesEvidence.AcceptanceId → ContractRulesAcceptances.Id`: bằng chứng thuộc một lần chấp nhận nội quy.
- `Users` — **FK** `ContractRulesEvidence.UploadedByUserId → Users.Id`: ghi tài khoản tải bằng chứng lên.

### `MemberCommitments`

Lưu bản cam kết nội quy của từng thành viên hợp đồng, người ký, nội dung cam kết được chụp lại và trạng thái bằng chứng. Bản cũ có thể được đánh dấu đã thay thế.

**Liên hệ và tác động:**

- `ContractTenants` — **FK** `MemberCommitments.ContractTenantId → ContractTenants.Id`: cam kết gắn với thành viên của hợp đồng.
- `TenantProfiles` — **FK tùy chọn** `MemberCommitments.SignerTenantProfileId → TenantProfiles.Id`: người ký có thể là một hồ sơ người thuê.
- `Users` — **FK tùy chọn** `MemberCommitments.ConfirmedByUserId → Users.Id`: ghi tài khoản xác nhận cam kết.

### `ContractSettlements`

Lưu bản quyết toán khi kết thúc hợp đồng: tiền cọc, công nợ, phí hư hại, phí khác và số dự kiến hoàn. Mỗi hợp đồng có tối đa một bản quyết toán theo chỉ mục duy nhất.

**Liên hệ và tác động:**

- `Contracts` — **FK** `ContractSettlements.ContractId → Contracts.Id`: quyết toán thuộc một hợp đồng.
- `Refunds` — **Nghiệp vụ, không FK**: số tiền dự kiến hoàn trong quyết toán có thể là căn cứ cho luồng hoàn tiền, nhưng bản ghi hoàn tiền thực tế gắn với hóa đơn.

## 3. Dịch vụ, đơn giá và chỉ số

### `Services`

Lưu danh mục dịch vụ dùng chung, đơn vị tính, giá mặc định và trạng thái hoạt động. Một dịch vụ chỉ áp dụng cho phòng khi có bản ghi gán ở `RoomServices`.

**Liên hệ và tác động:**

- `RoomServices` — **FK ngược** `RoomServices.ServiceId → Services.Id`: xác định dịch vụ nào được gắn cho từng phòng.
- `InvoiceItems` — **Nghiệp vụ, không FK**: tên/giá dịch vụ có thể là đầu vào tạo dòng tiền dịch vụ trên hóa đơn; dòng hóa đơn lưu giá tại thời điểm lập.

### `RoomServices`

Lưu việc gắn dịch vụ cho một phòng, khoảng thời gian áp dụng và giá riêng nếu có. Bảng này xác định dịch vụ nào được tính cho phòng trong kỳ hóa đơn.

**Liên hệ và tác động:**

- `Rooms` — **FK** `RoomServices.RoomId → Rooms.Id`: dịch vụ áp dụng cho phòng cụ thể.
- `Services` — **FK** `RoomServices.ServiceId → Services.Id`: tham chiếu dịch vụ trong danh mục chung.
- `InvoiceItems` — **Nghiệp vụ, không FK**: các dịch vụ còn hiệu lực có thể tạo dòng tiền dịch vụ trên hóa đơn; `CustomPrice` được ưu tiên nếu có.

### `UtilityRates`

Lưu đơn giá điện/nước theo khu trọ, loại tiện ích và khoảng thời gian hiệu lực. Bảng cung cấp giá để tính tiền tiêu thụ trong kỳ.

**Liên hệ và tác động:**

- `Properties` — **FK** `UtilityRates.PropertyId → Properties.Id`: đơn giá thuộc một khu trọ.
- `InvoiceItems` — **Nghiệp vụ, không FK**: đơn giá có hiệu lực được dùng khi lập dòng điện/nước; hóa đơn lưu lại đơn giá đã tính.

### `MeterReadings`

Lưu chỉ số điện/nước đầu và cuối kỳ của từng phòng, theo loại đồng hồ, năm và tháng. Chênh lệch chỉ số là lượng tiêu thụ dùng cho tính tiền.

**Liên hệ và tác động:**

- `Rooms` — **FK** `MeterReadings.RoomId → Rooms.Id`: chỉ số được ghi cho một phòng.
- `InvoiceItems` — **Nghiệp vụ, không FK**: lượng tiêu thụ từ chỉ số có thể tạo dòng tiền điện/nước trên hóa đơn.

## 4. Hóa đơn, thanh toán và yêu cầu

### `Invoices`

Lưu chứng từ phải thu theo hợp đồng và phòng, gồm loại hóa đơn, kỳ tính, hạn thanh toán, tổng tiền và trạng thái. Hóa đơn là điểm liên kết của các dòng tiền, thanh toán và khoản điều chỉnh.

**Liên hệ và tác động:**

- `Contracts` — **FK ghép** `(Invoices.ContractId, RoomId) → (Contracts.Id, RoomId)`: hóa đơn thuộc đúng hợp đồng và phòng của hợp đồng.
- `InvoiceItems` — **FK ngược** `InvoiceItems.InvoiceId → Invoices.Id`: các dòng chi tiết cấu thành hóa đơn.
- `InvoiceAdjustments` — **FK ngược** `InvoiceAdjustments.InvoiceId → Invoices.Id`: các khoản giảm/điều chỉnh số phải thu.
- `Payments` — **FK ngược** `Payments.InvoiceId → Invoices.Id`: các khoản tiền khai báo và được xác nhận cho hóa đơn.
- `Refunds` — **FK ngược** `Refunds.InvoiceId → Invoices.Id`: các khoản hoàn tiền liên quan hóa đơn.
- `DepositApplications` — **FK ghép ngược** `(TargetInvoiceId, ContractId) → (Invoices.Id, ContractId)`: hóa đơn đích nhận tiền cọc cấn trừ từ cùng hợp đồng.
- `TenantRequests` — **FK ghép ngược** `(RelatedInvoiceId, ContractId) → (Invoices.Id, ContractId)`: yêu cầu có thể trỏ tới hóa đơn cùng hợp đồng.
- `vwInvoiceBalances` — **Nghiệp vụ, không FK**: view lấy hóa đơn làm gốc để tính số hiệu lực và số còn nợ.
- `vwContractDepositBalances` — **Nghiệp vụ, không FK**: view chọn hóa đơn cọc đã phát hành để tính số cọc còn giữ.

### `InvoiceItems`

Lưu từng khoản trên hóa đơn như tiền phòng, điện, nước, dịch vụ hoặc khoản khác; mỗi dòng có số lượng, đơn giá và thành tiền. Cột `ReferenceId` là mã tham chiếu nghiệp vụ, không được khai báo FK.

**Liên hệ và tác động:**

- `Invoices` — **FK** `InvoiceItems.InvoiceId → Invoices.Id`: dòng tiền thuộc một hóa đơn.
- `MeterReadings` — **Nghiệp vụ, không FK**: chỉ số có thể là nguồn tính số lượng điện/nước của dòng hóa đơn.
- `UtilityRates` — **Nghiệp vụ, không FK**: đơn giá điện/nước có thể là nguồn tính `UnitPrice` khi lập dòng.
- `RoomServices` — **Nghiệp vụ, không FK**: dịch vụ đang áp dụng cho phòng có thể tạo dòng tiền dịch vụ.
- `Services` — **Nghiệp vụ, không FK**: tên/đơn vị dịch vụ giúp mô tả khoản thu, nhưng bảng dòng hóa đơn không có FK tới danh mục dịch vụ.

### `InvoiceAdjustments`

Lưu khoản giảm giá, sửa sai hoặc xóa nợ của một hóa đơn; có người tạo, lý do và thông tin hủy điều chỉnh nếu phát sinh. Chỉ khoản chưa bị hủy được cộng vào phép tính số dư view.

**Liên hệ và tác động:**

- `Invoices` — **FK** `InvoiceAdjustments.InvoiceId → Invoices.Id`: điều chỉnh thuộc một hóa đơn.
- `Users` — **FK** `CreatedByUserId/VoidedByUserId → Users.Id`: ghi người tạo và người hủy điều chỉnh.
- `vwInvoiceBalances` — **Nghiệp vụ, không FK**: điều chỉnh chưa bị hủy làm thay đổi `TotalAdjustments`, `EffectiveAmount` và `RemainingAmount` được tính khi đọc view.

### `Payments`

Lưu khoản tiền được khai báo cho hóa đơn, gồm số khai, phương thức, trạng thái và số thực nhận sau xác nhận. Khoản `Pending` chưa được view tính là tiền đã thu.

**Liên hệ và tác động:**

- `Invoices` — **FK** `Payments.InvoiceId → Invoices.Id`: khoản thanh toán gắn với một hóa đơn.
- `Users` — **FK tùy chọn** `SubmittedByUserId/VerifiedByUserId → Users.Id`: ghi người khai và người xác nhận tiền.
- `PaymentEvidence` — **FK ngược** `PaymentEvidence.PaymentId → Payments.Id`: các tệp chứng minh thanh toán.
- `PaymentReviewEvents` — **FK ngược** `PaymentReviewEvents.PaymentId → Payments.Id`: lịch sử duyệt/thay đổi trạng thái khoản tiền.
- `vwInvoiceBalances` — **Nghiệp vụ, không FK**: chỉ khoản `Success` và `VerifiedAmount` được cộng vào `TotalPaid` để giảm số còn nợ.

### `PaymentEvidence`

Lưu siêu dữ liệu tệp bằng chứng thanh toán, gồm tên tệp, blob, dung lượng và giá trị SHA-256. Nội dung tệp nằm ở kho lưu trữ ngoài cơ sở dữ liệu.

**Liên hệ và tác động:**

- `Payments` — **FK** `PaymentEvidence.PaymentId → Payments.Id`: bằng chứng thuộc một khoản thanh toán.
- `Users` — **FK** `PaymentEvidence.UploadedByUserId → Users.Id`: ghi người tải bằng chứng lên.

### `PaymentReviewEvents`

Lưu lịch sử xét duyệt thanh toán: trạng thái và số tiền trước/sau, lý do, người duyệt và thời điểm. Bảng này giúp truy vết các quyết định đối soát.

**Liên hệ và tác động:**

- `Payments` — **FK** `PaymentReviewEvents.PaymentId → Payments.Id`: sự kiện duyệt thuộc một khoản thanh toán.
- `Users` — **FK** `PaymentReviewEvents.ReviewedByUserId → Users.Id`: ghi tài khoản thực hiện đối soát.

### `Refunds`

Lưu khoản hoàn tiền gắn với hóa đơn, số tiền, lý do, trạng thái và thời điểm hoàn thực tế. Cờ `IsOverpaymentRefund` phân biệt khoản hoàn do thu vượt.

**Liên hệ và tác động:**

- `Invoices` — **FK** `Refunds.InvoiceId → Invoices.Id`: khoản hoàn gắn với một hóa đơn.
- `Users` — **FK** `Refunds.CreatedByUserId → Users.Id`: ghi người tạo khoản hoàn.
- `vwInvoiceBalances` — **Nghiệp vụ, không FK**: khoản hoàn `Success` làm thay đổi `TotalRefunded`; tác động tới `RemainingAmount` theo loại hóa đơn và cờ hoàn thu vượt.
- `ContractSettlements` — **Nghiệp vụ, không FK**: số dự kiến hoàn trong quyết toán có thể làm căn cứ nghiệp vụ; `Refunds` không có FK trực tiếp tới quyết toán.

### `DepositApplications`

Lưu mỗi lần cấn trừ tiền cọc của hợp đồng vào một hóa đơn đích, gồm số tiền, lý do và người thực hiện. FK ghép bảo đảm hóa đơn đích thuộc cùng hợp đồng.

**Liên hệ và tác động:**

- `Contracts` — **FK** `DepositApplications.ContractId → Contracts.Id`: khoản cấn trừ dùng cọc của hợp đồng.
- `Invoices` — **FK ghép** `(TargetInvoiceId, ContractId) → (Invoices.Id, ContractId)`: xác định hóa đơn đích trong cùng hợp đồng.
- `Users` — **FK** `DepositApplications.AppliedByUserId → Users.Id`: ghi người thực hiện cấn trừ.
- `vwInvoiceBalances` — **Nghiệp vụ, không FK**: tổng cọc đã áp dụng vào hóa đơn đích làm giảm `RemainingAmount` của hóa đơn đó.
- `vwContractDepositBalances` — **Nghiệp vụ, không FK**: tổng cọc đã dùng làm giảm `DepositHeld` của hợp đồng.

### `TenantRequests`

Lưu yêu cầu/phản ánh của người thuê theo hợp đồng, gồm loại, nội dung, độ ưu tiên, trạng thái và kết quả xử lý. Yêu cầu có thể liên quan đến một hóa đơn cụ thể.

**Liên hệ và tác động:**

- `TenantProfiles` — **FK** `TenantRequests.RequestedByTenantId → TenantProfiles.Id`: xác định người gửi yêu cầu.
- `Contracts` — **FK** `TenantRequests.ContractId → Contracts.Id`: yêu cầu phát sinh trong hợp đồng.
- `Invoices` — **FK ghép tùy chọn** `(RelatedInvoiceId, ContractId) → (Invoices.Id, ContractId)`: yêu cầu có thể liên quan hóa đơn cùng hợp đồng.
- `Users` — **FK tùy chọn** `TenantRequests.AssignedToUserId → Users.Id`: ghi tài khoản được giao xử lý.

## 5. SQL view (không phải bảng vật lý)

### `vwInvoiceBalances`

View chỉ đọc, tính số tiền hiệu lực, đã thu, đã hoàn, cọc đã cấn trừ và số còn nợ cho từng hóa đơn. Nó không lưu số dư thành một bản ghi riêng.

**Liên hệ và tác động:**

- `Invoices` — **Phụ thuộc SQL view, không FK**: cung cấp hóa đơn gốc, tổng tiền, loại và trạng thái để tính số dư.
- `InvoiceAdjustments` — **Phụ thuộc SQL view, không FK**: cộng các điều chỉnh chưa hủy vào `TotalAdjustments`.
- `Payments` — **Phụ thuộc SQL view, không FK**: cộng `VerifiedAmount` của khoản `Success` vào `TotalPaid`.
- `Refunds` — **Phụ thuộc SQL view, không FK**: cộng khoản hoàn `Success` vào `TotalRefunded` và xét cờ hoàn thu vượt khi tính số dư hóa đơn cọc.
- `DepositApplications` — **Phụ thuộc SQL view, không FK**: cộng tiền cọc đã áp dụng vào `TotalDepositApplied` của hóa đơn đích.
- `vwContractDepositBalances` — **Phụ thuộc view, không FK**: view cọc đọc số đã thu/đã hoàn của hóa đơn cọc từ đây.

### `vwContractDepositBalances`

View chỉ đọc, tổng hợp số cọc đã thu, đã hoàn, đã dùng và số cọc còn giữ cho hợp đồng có hóa đơn cọc đã phát hành.

**Liên hệ và tác động:**

- `Invoices` — **Phụ thuộc SQL view, không FK**: chọn hóa đơn loại `Deposit` đã `Issued` và xác định hợp đồng.
- `vwInvoiceBalances` — **Phụ thuộc SQL view, không FK**: lấy số cọc thực thu và số đã hoàn từ số dư hóa đơn cọc.
- `DepositApplications` — **Phụ thuộc SQL view, không FK**: tổng tiền cọc đã áp dụng theo hợp đồng làm giảm `DepositHeld`.
