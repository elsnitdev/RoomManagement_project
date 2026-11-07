# Kế hoạch triển khai hợp đồng phòng và cam kết từng người

**Trạng thái:** Backend, Postman và migration đã được viết theo hai thay đổi CSDL chủ trọ duyệt. Migration chưa chạy trên CSDL đang dùng; SQL Server cấu hình hiện chưa kết nối được từ môi trường làm việc.

## 1. Quyết định nghiệp vụ

- Một phòng có tối đa một hợp đồng phòng hiệu lực tại cùng thời điểm. Chủ trọ tạo hợp đồng; tiền thuê, cọc, dịch vụ và hóa đơn tính chung cho phòng. Người ở tự phân chia tiền với nhau, hệ thống không lưu tỷ lệ đóng góp.
- Mỗi người ở phải có `TenantProfile`; tài khoản đăng nhập là tùy chọn. Một hợp đồng có thể có nhiều người, không vượt `Room.MaxOccupants`.
- Người mà chủ trọ ưu tiên liên hệ dùng `ContractTenant.IsContactPerson` hiện có. Đây là đầu mối liên hệ, không phải vai trò pháp lý riêng. Chủ trọ đổi đầu mối trước khi người liên hệ cũ rời hợp đồng; không cần bảng lịch sử đại diện.
- Mỗi thành viên của từng hợp đồng phải có cam kết được chủ trọ xác nhận trước khi kích hoạt hợp đồng. Giai đoạn này lưu thông tin cam kết và xác nhận, cho phép kích hoạt khi chưa có tệp đã ký; ảnh/PDF và kiểm tra tệp được triển khai sau. Hồ sơ thành viên cần ngày sinh để xác định trường hợp chưa đủ tuổi; người ký thay có profile và quan hệ với người ở.
- Hợp đồng mới bắt buộc có `EndDate` là ngày cuối tháng. Hợp đồng kế tiếp cho cùng phòng không được chồng thời gian với kỳ cũ; ví dụ thông thường bắt đầu ngày 1 tháng sau. Nếu bắt đầu muộn, quy tắc 7 ngày quyết định thời gian ở của từng người có liên tiếp hay không. Không cần khóa nối hai hợp đồng; tra cứu theo `RoomId`, ngày hiệu lực và profile thành viên.
- Thời gian ở liên tiếp được tính từ các khoảng thành viên thuộc **hợp đồng đã từng kích hoạt** của cùng profile và cùng phòng. Hai khoảng cách nhau tối đa 7 ngày trống được coi là một đợt; khi kỳ tiếp theo xuất hiện, các ngày trống đó cũng được tính vào thời gian ở. Không tạo bảng cư trú hoặc bước check-in riêng.
- Thông báo dừng/rời trọ là một loại report trong `TenantRequests`. Hệ thống tự ghi thời điểm gửi; chủ trọ quyết định ngày rời thực tế, hoàn phí và tiền cọc. Chưa kiểm tra tự động số tháng ở tối thiểu, thời hạn báo trước, khoản phạt hoặc cấn trừ cọc.

## 2. Nền tảng hiện có

| Thành phần | Cách dùng |
| --- | --- |
| `Contract` | Hợp đồng phòng, thời hạn, giá thuê/cọc chung; trạng thái `Draft/Active/Expired/Terminated/Cancelled`. |
| `ContractTenant` | Liên kết profile với từng hợp đồng, `JoinDate/LeaveDate` và `IsContactPerson`; đã có ràng buộc một người liên hệ chưa rời cho mỗi hợp đồng. |
| `TenantProfile` | Hồ sơ từng người; `UserId` có thể `null`, người liên hệ phải có số điện thoại hoặc email. |
| `TenantRequests` | Đã có `ContractId`, `RequestedByTenantId`, `Category`, `Description`, `RequestedAt`, `Status`, `ResolutionNote`; hiện chưa có API/service/store cho report. |
| `ContractDocument` | Hiện yêu cầu `FileName` và `StorageKey`, vì vậy chưa dùng để ghi cam kết chưa có tệp. |
| `Invoice` | Gắn với hợp đồng và phòng; hiện đã chặn hóa đơn tháng trùng **trong cùng hợp đồng**, cần thêm kiểm tra xuyên hợp đồng cho cùng phòng/kỳ. |

Các sửa đổi backend và Postman hiện có trong working tree phải được giữ lại và phát triển tiếp; không hoàn tác.

## 3. Thay đổi CSDL đã duyệt để triển khai

### 3.1. Bảng `MemberCommitments`

Một bản ghi gắn với một `ContractTenant`, tức cam kết riêng của người đó trong một kỳ hợp đồng. Đề xuất cột:

| Cột | Công dụng |
| --- | --- |
| `Id` | Khóa chính. |
| `ContractTenantId` | Khóa ngoại tới thành viên của đúng hợp đồng. |
| `CommitmentType` | **Loại** cam kết, khác trạng thái xử lý; giai đoạn này chỉ có `RoomRulesAgreement` (cam kết tuân thủ nội quy thuê trọ). Có thể thêm loại khác về sau mà không đổi ý nghĩa bản ghi cũ. |
| `SignedOn` | Ngày người thuê hoặc người ký thay ký bản giấy, do chủ trọ ghi nhận. |
| `SignerName`, `SignerTenantProfileId`, `SignerRelation` | Tên người ký; profile và quan hệ để ghi nhận trường hợp ký thay. Profile người ở vẫn bắt buộc độc lập với người ký. |
| `TermsSnapshot` | Nội dung điều khoản được xác nhận tại thời điểm ký; thay đổi nội quy về sau không viết lại lịch sử. |
| `ConfirmedAt`, `ConfirmedByUserId` | Thời điểm và chủ trọ xác nhận đã nhận cam kết. Đây là điều kiện kích hoạt trong giai đoạn chưa có ảnh/PDF. |
| `EvidenceStatus` | Ban đầu `PendingUpload`, dùng để biết tệp đã ký còn thiếu; trạng thái `Uploaded` chỉ được dùng khi chức năng tệp hoàn thành. |
| `SupersededAt`, `CreatedAt` | Giữ lịch sử bản cũ khi thay bằng bản mới và ghi thời điểm tạo. |

Khóa ngoại và chỉ mục duy nhất có điều kiện bảo đảm mỗi `ContractTenantId` + `CommitmentType` có tối đa một bản chưa bị thay thế. Với thành viên của hợp đồng cũ, migration tạo bản ghi `LegacyPendingEvidence` và để trống thông tin ký/xác nhận; chủ trọ đã duyệt cách chuyển đổi này. Bản ghi đó không được coi là đã ký. Hợp đồng đã kích hoạt trước migration tiếp tục tồn tại, còn hợp đồng kích hoạt mới phải qua điều kiện cam kết được xác nhận.

### 3.2. Mở rộng `TenantRequests.Category`

Thêm giá trị `MoveOutNotice` vào enum và check constraint `CK_TenantRequests_Category`; **không thêm bảng/cột** cho thông báo rời. `RequestedByTenantId` trỏ tới `TenantProfile` nên không bắt buộc tài khoản; chủ trọ có thể ghi nhận thông báo thay người chưa có tài khoản. `RequestedAt` do máy chủ đặt theo UTC và API hiển thị theo múi giờ Việt Nam. `Status` hiện có (`Pending/InProgress/Resolved`) và `ResolutionNote` lưu xử lý; `ContractTenant.LeaveDate` là ngày rời thực tế do chủ trọ xác nhận. Ngày gửi và ngày rời không được đánh đồng.

### 3.3. Hóa đơn trùng kỳ

Trước mắt kiểm tra trong nghiệp vụ và giao dịch rằng không phát hành hai hóa đơn `Monthly` chưa hủy cho cùng `RoomId` + năm + tháng, kể cả ở hai hợp đồng. Chỉ mục duy nhất theo phòng/kỳ là phương án tăng bảo đảm dữ liệu ở cấp DB, nhưng cần kiểm tra hóa đơn trùng hiện có và xin duyệt riêng trước khi thêm; không nằm trong hai thay đổi schema đang xin duyệt ở trên.

Không thêm `TenantStays`, `ContractTenants.TenantStayId`, `RepresentativeAssignments`, `MoveOutNotices`, `Contracts.PreviousContractId` hoặc cột trạng thái thuê trên `Rooms`.

## 4. Quy tắc tính thời gian ở liên tiếp

1. Chọn các liên kết `ContractTenant` của cùng `TenantProfile.Id` và cùng `Contract.RoomId`. Chỉ chọn hợp đồng đã từng được kích hoạt: hiện là `Active`, `Expired` hoặc `Terminated`; loại `Draft` và `Cancelled`.
2. Khoảng của mỗi liên kết bắt đầu tại ngày muộn hơn giữa `Contract.StartDate` và `ContractTenant.JoinDate`. Ngày cuối là ngày sớm nhất của `ContractTenant.LeaveDate` (nếu có), `Contract.EndDate` (nếu có), `Contract.ClosedOn` khi chấm dứt trước hạn, và ngày đang tra cứu. Bỏ khoảng có ngày cuối trước ngày đầu. Không kéo dài hợp đồng đã hết hạn chỉ vì `LeaveDate = null`.
3. Sắp xếp các khoảng theo ngày đầu. Gộp khoảng giao nhau hoặc liền nhau. Số **ngày trống** giữa hai khoảng là `ngày đầu kỳ sau - ngày cuối kỳ trước - 1`. Nếu số này từ 0 đến 7, gộp thành một đợt; nếu từ 8 trở lên, bắt đầu đợt mới. Ví dụ kết thúc 31/1, bắt đầu lại 8/2: 7 ngày trống và vẫn liên tiếp; bắt đầu 9/2: 8 ngày trống và đứt quãng.
4. Khi đã có kỳ sau, thời gian của đợt gộp tính từ ngày đầu kỳ trước đến ngày cuối kỳ sau, **bao gồm** ngày trống. Khi chưa có kỳ sau, không tự kéo dài một đợt đã kết thúc qua 7 ngày chờ. API nên trả cả khoảng và số ngày để người dùng hiểu phép tính.

Quy tắc 7 ngày chỉ dành cho lịch sử/thống kê. Trạng thái phòng **đang có người ở** vẫn cần hợp đồng `Active` có hiệu lực hôm nay và thành viên có `JoinDate <= hôm nay <= LeaveDate` (hoặc `LeaveDate = null`). Nếu hợp đồng đã hết và không có hợp đồng mới, không báo `Occupied` chỉ vì đợt ở trước có thể sẽ được nối sau này. Không có check-in độc lập nên thời gian ở là thời gian được chủ trọ ghi trên hợp đồng, không phải bằng chứng tự động về hiện diện thực tế.

## 5. Luồng backend và API dự kiến

| Luồng | Xử lý và API |
| --- | --- |
| Tạo hợp đồng | Giữ `POST /api/properties/{propertyId}/contracts`; hợp đồng mới bắt buộc `EndDate` cuối tháng, kiểm tra phòng thuộc chủ trọ và không chồng kỳ hiệu lực. Hợp đồng cũ thiếu `EndDate` cần chốt ngày trước khi tạo kỳ tiếp theo. |
| Chốt ngày kết thúc hợp đồng cũ | `PUT .../contracts/{id}/end-date` chỉ áp dụng cho hợp đồng Draft/Active cũ có `EndDate = null`; ngày mới phải là cuối tháng và không được trước ngày tham gia/rời của thành viên hay kỳ hóa đơn tháng chưa hủy. Chủ trọ thực hiện trong giao dịch khóa phòng. |
| Thêm thành viên | Giữ API thêm profile/tài khoản vào hợp đồng; bắt buộc profile, kiểm tra sức chứa, ngày tham gia, người ở trùng phòng/hợp đồng. Người không có tài khoản vẫn được thêm bằng `TenantProfile`. |
| Ghi cam kết | `POST .../contracts/{id}/tenants/{linkId}/commitments`: chủ trọ ghi thông tin ký, nội dung, người ký thay và xác nhận. API đọc danh sách/bản hiện hành theo thành viên. Không nhận tệp trong đợt này. |
| Chọn liên hệ ưu tiên | Dùng `PUT .../contracts/{id}/contact/{linkId}` hiện có; kiểm tra profile có điện thoại/email. Khi người liên hệ rời, thực hiện đổi liên hệ và ghi rời trong cùng giao dịch hoặc bắt buộc đổi trước. |
| Kích hoạt | Giữ `POST .../contracts/{id}/activate`. Kiểm tra ít nhất một thành viên từ ngày bắt đầu, đúng một người liên hệ, cam kết đã xác nhận của từng thành viên, sức chứa, quyền chủ trọ, ngày hợp đồng và không trùng kỳ. Thực hiện kiểm tra cuối trong giao dịch khóa phòng. |
| Người vào sau | Thành viên mới của hợp đồng `Active` phải có cam kết được xác nhận trước `JoinDate` được tính là đang ở; API không tăng số người đang ở chỉ vì đã tạo liên kết với ngày tham gia tương lai. |
| Report rời trọ | API report cho chủ trọ ghi nhận thay profile, hoặc người có tài khoản tự gửi; category `MoveOutNotice`. Máy chủ tự ghi `RequestedAt`; chủ trọ xem và cập nhật `Status/ResolutionNote`. Report không tự chấm dứt hợp đồng hoặc tính hoàn phí. |
| Xác nhận rời | Giữ `POST .../contracts/{id}/tenants/{linkId}/leave`; chủ trọ đặt `LeaveDate`, không được sớm hơn `JoinDate`; nếu là người liên hệ thì chọn người khác trước. Người cuối cùng rời thì xử lý kết thúc hợp đồng. |
| Hợp đồng kế tiếp | Tạo nháp mới cùng phòng sau khi kỳ cũ kết thúc cuối tháng, thêm lại các profile tiếp tục ở với `JoinDate` của kỳ mới, ghi cam kết cho từng người và chọn liên hệ. Không sao chép nợ/cọc/hóa đơn hoặc ghi đè hợp đồng cũ. Quy tắc 7 ngày tự nối lịch sử của cùng người/cùng phòng. |
| Xem phòng và hợp đồng | API chi tiết trả người hiện tại, liên hệ, cam kết, report và lịch sử thời gian ở. Tách trạng thái vận hành (`Available/Maintenance/Inactive`) khỏi trạng thái có hợp đồng/người đang ở; phòng có hợp đồng tương lai hoặc nháp cần được hiển thị rõ để tránh cho thuê trùng. |

Không có bước chuyển giao đại diện pháp lý hoặc API check-in riêng. `IsContactPerson` chỉ là người ưu tiên liên hệ.

## 6. Thứ tự triển khai

1. Chốt schema tối thiểu ở mục 3 và viết migration an toàn; kiểm tra dữ liệu cũ, giữ nguyên cam kết chưa có bằng chứng. Không tự chạy migration trên CSDL đang dùng nếu chưa được cho phép.
2. Bổ sung DTO, service, store và API cam kết; kiểm tra quyền sở hữu profile/hợp đồng, nội dung và xác nhận trong giao dịch. Mỗi function mới trong Store phải có comment nêu rõ chức năng để người đọc dễ tra cứu.
3. Triển khai report `MoveOutNotice` trên `TenantRequests`, gồm API tạo, xem, cập nhật trạng thái. Thời điểm gửi lấy từ máy chủ, không tin ngày do client gửi.
4. Cập nhật điều kiện tạo/kích hoạt/rời/chuyển liên hệ, hợp đồng kế tiếp và thống kê 7 ngày; giữ nguyên dữ liệu tài chính của từng hợp đồng.
5. Cập nhật kiểm tra hóa đơn cùng phòng/kỳ; rà dữ liệu trước khi đề xuất chỉ mục DB nếu cần. Cập nhật Postman và dữ liệu mẫu; chạy test backend.

## 7. Kịch bản kiểm thử và Postman

- Một phòng tối đa 3 người, một hợp đồng và một hóa đơn chung; cả 3 có profile nhưng không cần tài khoản. Người thứ tư bị từ chối.
- Thiếu cam kết của một thành viên thì không kích hoạt; cả 3 đã được chủ trọ xác nhận thì kích hoạt dù `EvidenceStatus = PendingUpload`.
- Đổi người liên hệ rồi người cũ rời; người liên hệ mới có điện thoại/email. Hợp đồng và tiền thuê chung giữ nguyên.
- Report `MoveOutNotice` ghi đúng thời điểm máy chủ; không tự đổi `LeaveDate`, tiền cọc hoặc trạng thái hợp đồng. Chủ trọ xử lý và ghi ngày rời riêng.
- Người tiếp tục ở qua hai hợp đồng cùng phòng: gap 7 ngày thì một đợt, gap 8 ngày thì hai đợt; ngày trống chỉ được cộng sau khi có kỳ sau. Hợp đồng `Expired` vẫn được tính lịch sử, `Draft/Cancelled` không tính.
- Phòng chỉ báo `Occupied` khi hợp đồng `Active` có hiệu lực và có thành viên thuộc ngày hôm nay; quy tắc 7 ngày không làm phòng trống trở thành đang có người.
- Không có hai hợp đồng hiệu lực chồng kỳ, không có hai hóa đơn tháng chưa hủy cho cùng phòng/kỳ; công nợ và cọc của kỳ trước không tự chuyển sang kỳ mới.
- Chủ trọ khác không được xem profile, cam kết, report hoặc hợp đồng của khu trọ này.

Postman cần có biến môi trường và dữ liệu mẫu cho toàn bộ kịch bản trên, gồm hai kỳ hợp đồng, ba profile không tài khoản, report rời, đổi người liên hệ, gap 7/8 ngày và lỗi thiếu cam kết.

## 8. Ngoài phạm vi đợt này

- Tải lên, xem, lưu và kiểm chứng ảnh/PDF bản ký; quản lý phiên bản tệp.
- Kiểm tra tháng ở tối thiểu, thời hạn báo trước, phạt rời sớm và quyết định hoàn/cấn trừ cọc tự động.
- Phân chia tiền giữa người ở, hóa đơn riêng mỗi người, rule engine tự cấu hình và ký điện tử.

Nội dung bản cam kết và giá trị chứng cứ của việc chủ trọ xác nhận metadata cần người phụ trách pháp lý của dự án duyệt trước khi dùng làm mẫu chính thức.

**Xác minh triển khai:** API và test project build thành công; JSON và script của Postman hợp lệ. VSTest chưa chạy được test runtime vì testhost không kết nối; chưa thử API với CSDL vì SQL Server cấu hình hiện không truy cập được và migration chưa được phép áp dụng.
