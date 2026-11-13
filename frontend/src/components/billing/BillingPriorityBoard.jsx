const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'

const missingLabel = {
  ElectricityReading: 'Chỉ số điện',
  WaterReading: 'Chỉ số nước',
  ElectricityRate: 'Giá điện',
  WaterRate: 'Giá nước',
}

const workPriority = { MissingInputs: 0, NeedsAttention: 1, ReadyToPreview: 2, Draft: 3, Issued: 4 }
const debtPriority = { Overdue: 0, Partial: 1, Unpaid: 2 }
const lastClosedMonth = () => {
  const current = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 7)
  const [year, month] = current.split('-').map(Number)
  return new Date(Date.UTC(year, month - 2, 1)).toISOString().slice(0, 7)
}

export default function BillingPriorityBoard({ month, onMonthChange, worklist, worklistError, worklistLoading, attention, attentionError, attentionLoading, roomName, onWorkItem, onOpenInvoice }) {
  const work = [...worklist].sort((a, b) => (workPriority[a.status] ?? 5) - (workPriority[b.status] ?? 5) || String(a.roomNumber).localeCompare(String(b.roomNumber), 'vi'))
  const needsInput = work.filter(item => item.status === 'MissingInputs' || item.status === 'NeedsAttention')
  const otherWork = work.filter(item => item.status !== 'MissingInputs' && item.status !== 'NeedsAttention' && item.status !== 'Issued')
  const pending = attention?.pendingPayments || []
  const unpaid = [...(attention?.unpaid || [])].filter(item => Number(item.amountDue) > 0)
    .sort((a, b) => (debtPriority[a.displayStatus] ?? 3) - (debtPriority[b.displayStatus] ?? 3) || String(a.dueDate).localeCompare(String(b.dueDate)))

  return (
    <div className="bill-priority-board">
      <div className="bill-stat-grid" aria-label="Tổng quan việc cần xử lý">
        <div className="bill-stat"><span>Cần nhập hoặc kiểm tra điện nước</span><strong>{worklistLoading ? '…' : needsInput.length}</strong></div>
        <div className="bill-stat"><span>Thanh toán chờ xác nhận</span><strong>{attentionLoading ? '…' : pending.length}</strong></div>
        <div className="bill-stat"><span>Hóa đơn còn phải thu</span><strong>{attentionLoading ? '…' : unpaid.length}</strong></div>
      </div>

      <section className="bill-card bill-priority-card bill-priority-card--meters" aria-labelledby="bill-worklist-title">
        <div className="bill-list-heading">
          <div><h2 id="bill-worklist-title">Cần nhập điện, nước</h2><p>Chốt chỉ số kỳ {month?.split('-').reverse().join('/')} để tạo hóa đơn đầu tháng.</p></div>
          <label>Kỳ hóa đơn <input type="month" value={month} min="2000-01" max={lastClosedMonth()} onChange={event => onMonthChange(event.target.value)} /></label>
        </div>
        {worklistError && <p className="bill-error" role="alert">{worklistError}</p>}
        {worklistLoading && <p role="status">Đang tải việc cần chốt…</p>}
        {!worklistLoading && !worklistError && !needsInput.length && <p className="bill-empty">Không có phòng thiếu dữ liệu điện nước trong kỳ này.</p>}
        {!!needsInput.length && <ul className="bill-action-list">
          {needsInput.map(item => <li className="bill-action-row" key={item.contractId}>
            <div><strong>Phòng {item.roomNumber || roomName(item.roomId)}</strong><small>{item.contractCode}</small></div>
            <div><span className="bill-badge bill-badge--urgent">{item.status === 'NeedsAttention' ? 'Cần kiểm tra' : 'Thiếu dữ liệu'}</span><p>{item.missingInputs?.map(code => missingLabel[code] || code).join(', ') || 'Dữ liệu tính tiền cần kiểm tra'}</p></div>
            <button className="bill-button" type="button" onClick={() => onWorkItem(item)}>{item.status === 'NeedsAttention' ? 'Kiểm tra dữ liệu' : 'Nhập chỉ số'}</button>
          </li>)}
        </ul>}
        {!!otherWork.length && <details className="bill-secondary-work"><summary>{otherWork.length} phòng đã có dữ liệu hoặc hóa đơn nháp</summary><ul className="bill-action-list">
          {otherWork.map(item => <li className="bill-action-row" key={item.contractId}>
            <div><strong>Phòng {item.roomNumber || roomName(item.roomId)}</strong><small>{item.contractCode}</small></div>
            <span className="bill-badge bill-badge--ready">{item.status === 'Draft' ? 'Nháp chờ phát hành' : 'Sẵn sàng xem trước'}</span>
            <button type="button" className="bill-link" onClick={() => onWorkItem(item)}>{item.invoiceId ? 'Xem hóa đơn' : 'Xem trước'}</button>
          </li>)}
        </ul></details>}
      </section>

      <section className="bill-card bill-priority-card bill-priority-card--payments" aria-labelledby="bill-payment-queue-title">
        <div className="bill-list-heading"><div><h2 id="bill-payment-queue-title">Thanh toán cần xử lý</h2><p>Chứng từ chờ xác nhận và hóa đơn còn phải thu của toàn khu trọ.</p></div></div>
        {attentionError && <p className="bill-error" role="alert">{attentionError}</p>}
        {attentionLoading && <p role="status">Đang tải thanh toán cần xử lý…</p>}
        {!attentionLoading && !attentionError && !pending.length && !unpaid.length && <p className="bill-empty">Chưa có thanh toán hoặc hóa đơn cần xử lý.</p>}
        {!!pending.length && <div><h3>Chờ xác nhận · {pending.length}</h3><ul className="bill-action-list">
          {pending.map(payment => <li className="bill-action-row" key={payment.paymentId}>
            <div><strong>{payment.invoiceCode}</strong><small>Phòng {roomName(payment.roomId)}</small></div>
            <div><span className="bill-badge bill-badge--pending">Chờ đối chiếu</span><p>{payment.submittedByName || 'Người thuê'} khai {money(payment.claimedAmount)}</p></div>
            <button className="bill-link" type="button" onClick={() => onOpenInvoice(payment.invoiceId)}>Xem hóa đơn</button>
          </li>)}
        </ul></div>}
        {!!unpaid.length && <div><h3>Còn phải thu · {unpaid.length}</h3><ul className="bill-action-list">
          {unpaid.map(invoice => <li className="bill-action-row" key={invoice.id}>
            <div><strong>{invoice.code}</strong><small>Phòng {roomName(invoice.roomId)} · Hạn {invoice.dueDate}</small></div>
            <div><span className={`bill-badge ${invoice.displayStatus === 'Overdue' ? 'bill-badge--urgent' : 'bill-badge--pending'}`}>{({ Overdue: 'Quá hạn', Partial: 'Đã thu một phần', Unpaid: 'Chưa thanh toán' })[invoice.displayStatus] || invoice.displayStatus}</span><p>Còn phải thu <strong>{money(invoice.amountDue)}</strong></p></div>
            <button className="bill-link" type="button" onClick={() => onOpenInvoice(invoice.id)}>Xem hóa đơn</button>
          </li>)}
        </ul></div>}
      </section>
    </div>
  )
}
