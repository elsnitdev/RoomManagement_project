# Task 5: Luồng và các hàm chính

Tài liệu mô tả mã hiện tại theo thứ tự gọi hàm: giao diện → API → nghiệp vụ → truy cập dữ liệu. Task 5 gồm tra cứu và tạo hóa đơn cọc cho hợp đồng cũ, hủy hợp đồng nháp và kiểm thử luồng cọc.

## 1. Mở hợp đồng và kiểm tra hóa đơn cọc

1. **`useEffect` — `LandlordContracts.jsx`**: Khi mở chi tiết hợp đồng có cọc > 0, gọi `billingApi.depositInvoice(contractId)`. Trong lúc chờ, giao diện hiện trạng thái tải; nếu lỗi, cho phép thử lại.
2. **`billingApi.depositInvoice` → `apiRequest`**: Gửi `GET /api/contracts/{contractId}/invoices/deposit` và trả về phần `data` của phản hồi.
3. **`ContractInvoicesController.GetDeposit`**: Nhận yêu cầu của chủ trọ, gọi `InvoiceService.FindDepositAsync` và trả thông tin hóa đơn hoặc `null`.
4. **`InvoiceService.FindDepositAsync`**: Xác định chủ trọ hiện tại, kiểm tra hợp đồng thuộc quyền sở hữu qua `GetOwnedContractByIdAsync`, rồi gọi store tìm hóa đơn.
5. **`InvoiceStore.GetOwnedDepositAsync`**: Tìm hóa đơn cọc của hợp đồng; ưu tiên hóa đơn chưa hủy, sau đó chọn hóa đơn gần nhất. Giao diện hiện liên kết nếu tìm thấy, hoặc nút tạo nháp khi hợp đồng đủ điều kiện.

## 2. Tạo hóa đơn cọc nháp cho hợp đồng cũ

1. **`createLegacyDeposit` — `LandlordContracts.jsx`**: Sau khi chủ trọ xác nhận, gọi `billingApi.createDeposit(contractId)`. Khi thành công, hiện thông báo và chuyển đến chi tiết hóa đơn vừa tạo.
2. **`billingApi.createDeposit` → `apiRequest`**: Gửi `POST /api/contracts/{contractId}/invoices/deposit`; phản hồi chứa ID, mã và trạng thái hóa đơn.
3. **`ContractInvoicesController.CreateDeposit`**: Gọi `InvoiceService.CreateDepositAsync` và trả HTTP 201 cùng thông tin hóa đơn đã tạo.
4. **`InvoiceService.CreateDepositAsync`**: Kiểm tra chủ trọ, hợp đồng thuộc quyền quản lý, hợp đồng đang nháp/hoạt động và tiền cọc > 0. Hàm tạo đối tượng hóa đơn `Draft` cùng một dòng tiền cọc, rồi gọi store để lưu.
5. **`InvoiceStore.CreateDraftAsync`**: Kiểm tra dữ liệu, mở transaction và khóa phòng; kiểm tra lại quyền và lịch sử hóa đơn cọc. Nếu đã từng có hóa đơn cọc, trả lỗi `DEPOSIT_INVOICE_EXISTS`; nếu hợp lệ, lưu hóa đơn và dòng tiền cùng lúc rồi commit.

**Kết quả:** Hóa đơn ở trạng thái `Draft`, chưa tự phát hành. Chủ trọ cần kiểm tra trước khi bấm phát hành.

## 3. Phát hành hóa đơn cọc nháp

1. **`action` — `LandlordBilling.jsx`**: Khi chủ trọ xác nhận phát hành, gọi `billingApi.issue(invoiceId)`; sau khi thành công, tải lại chi tiết hóa đơn.
2. **`billingApi.issue` → `apiRequest`**: Gửi `POST /api/invoices/{invoiceId}/issue` đến API.
3. **`InvoicesController.Issue` → `InvoiceService.IssueAsync`**: Controller chuyển yêu cầu cho service; service lấy chủ trọ hiện tại và gọi `InvoiceStore.IssueAsync`.
4. **`InvoiceStore.IssueAsync`**: Khóa hóa đơn, kiểm tra quyền, trạng thái hợp đồng, hạn thanh toán và tổng các dòng tiền; sau đó đổi hóa đơn sang `Issued` và commit. Người thuê đủ quyền mới nhìn thấy hóa đơn đã phát hành.

## 4. Hủy hợp đồng nháp và hóa đơn cọc

1. **`run` — `LandlordContracts.jsx`**: Sau khi chủ trọ xác nhận hủy, gọi `contractApi.cancel(propertyId, contractId)`. Thành công thì tải lại hợp đồng; thất bại thì hiện thông báo lỗi.
2. **`contractApi.cancel` → `apiRequest`**: Gửi `POST /api/properties/{propertyId}/contracts/{contractId}/cancel`.
3. **`ContractsController.Cancel` → `ContractService.CancelAsync`**: Service gọi `GetAsync` để lấy hợp đồng thuộc chủ trọ, kiểm tra trạng thái `Draft`, rồi gọi `ContractStore.CloseSafelyAsync`.
4. **`ContractStore.CloseSafelyAsync`**: Mở transaction, khóa phòng và kiểm tra lại trạng thái hợp đồng. Nếu có hóa đơn cọc chưa hủy, hàm khóa hóa đơn và kiểm tra lịch sử tài chính trước khi thay đổi trạng thái.
5. **`InvoiceTransactionLock.AcquireAsync`**: Dùng khóa theo ID hóa đơn trong transaction để tránh xử lý thanh toán và hủy cùng lúc.
6. **`InvoiceFinancialActivity.ExistsAsync`**: Kiểm tra thanh toán, hoàn tiền, điều chỉnh và các khoản dùng cọc liên quan. Có lịch sử thì trả lỗi `CONTRACT_DEPOSIT_HAS_FINANCIAL_ACTIVITY`; hợp đồng và hóa đơn không bị hủy.
7. **`ContractStore.SaveAsync` → `CommitAsync`**: Nếu không có lịch sử tài chính, đổi hóa đơn cọc và hợp đồng sang `Cancelled`, ghi ngày đóng và lưu cùng transaction. Nếu không có hóa đơn cọc, chỉ cập nhật hợp đồng.

## 5. Hủy riêng một hóa đơn

1. **`action` → `billingApi.cancel` — giao diện hóa đơn**: Chủ trọ xác nhận, giao diện gửi `POST /api/invoices/{invoiceId}/cancel` và tải lại chi tiết sau khi thành công.
2. **`InvoicesController.Cancel` → `InvoiceService.CancelAsync` → `InvoiceStore.CancelAsync`**: Store khóa hóa đơn và gọi chung `InvoiceFinancialActivity.ExistsAsync`. Có lịch sử tài chính thì chặn; không có thì đổi trạng thái sang `Cancelled` và commit.

## 6. Các test liên quan

- **`DepositWorkflowTests.Contract_to_tenant_claim_to_owner_review_updates_balance_and_blocks_cancellation`**: Kiểm tra từ tạo hợp đồng, người thuê khai, chủ trọ duyệt/ghi tiền đến số dư; kiểm tra chặn hủy khi đã có khoản thanh toán.
- **`DepositWorkflowTests.Cancelling_unpaid_draft_cancels_deposit_invoice_in_same_transaction`**: Kiểm tra hợp đồng và hóa đơn cọc cùng bị hủy khi chưa có giao dịch.
- **`DepositWorkflowTests.Legacy_contract_without_deposit_invoice_can_create_draft_then_issue`**: Kiểm tra tạo nháp, phát hành và chặn tạo hóa đơn cọc trùng cho hợp đồng cũ.

Các test trên dùng SQL Server thử nghiệm và yêu cầu biến `ROOMMANAGEMENT_TEST_SQLSERVER`; xem thêm [kịch bản kiểm thử](deposit-workflow-test-plan.md). Việc có test trong mã không đồng nghĩa chúng đã được chạy thành công trên môi trường hiện tại.
