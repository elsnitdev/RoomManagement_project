import { useEffect, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'
import BillingInputs from './BillingInputs'
import '../../styles/billing-live.css'

const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const errorOf = error => error?.message || 'Không thể hoàn tất thao tác.'
const monthLabel = invoice => invoice.billingMonth ? `${invoice.billingMonth}/${invoice.billingYear}` : 'Tiền cọc'
const statusLabel = { Draft: 'Bản nháp', Issued: 'Đã phát hành', Cancelled: 'Đã hủy', Paid: 'Đã thanh toán', Partial: 'Đã thu một phần', Unpaid: 'Chưa thanh toán', Overdue: 'Quá hạn', Credit: 'Dư tiền' }

export default function LandlordBilling({ invoiceId, navigate }) {
  const [properties, setProperties] = useState([])
  const [propertyId, setPropertyId] = useState('')
  const [rooms, setRooms] = useState([])
  const [contracts, setContracts] = useState([])
  const [tab, setTab] = useState('invoices')
  const [month, setMonth] = useState(() => new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 7))
  const [contractId, setContractId] = useState('')
  const [preview, setPreview] = useState(null)
  const [invoices, setInvoices] = useState([])
  const [meta, setMeta] = useState(null)
  const [page, setPage] = useState(1)
  const [detail, setDetail] = useState(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [revision, setRevision] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    billingApi.properties(controller.signal).then(response => {
      setProperties(response.data || [])
      setPropertyId(current => current && response.data?.some(item => item.id === current) ? current : response.data?.[0]?.id || '')
    }).catch(cause => { if (cause.name !== 'AbortError') setError(errorOf(cause)) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [])
  useEffect(() => {
    if (!propertyId) { setRooms([]); setContracts([]); setInvoices([]); return }
    const controller = new AbortController()
    setLoading(true); setError('')
    Promise.all([
      billingApi.rooms(propertyId, controller.signal),
      billingApi.contracts(propertyId, controller.signal),
      billingApi.invoices(propertyId, page, controller.signal),
    ]).then(([roomResponse, contractResponse, invoiceResponse]) => {
      setRooms(roomResponse.data || [])
      setContracts(contractResponse.data || [])
      setInvoices(invoiceResponse.data || [])
      setMeta(invoiceResponse.meta)
    }).catch(cause => { if (cause.name !== 'AbortError') setError(errorOf(cause)) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [propertyId, page, revision])
  useEffect(() => {
    if (!invoiceId) { setDetail(null); return }
    const controller = new AbortController()
    setError('')
    billingApi.invoice(invoiceId, controller.signal).then(setDetail)
      .catch(cause => { if (cause.name !== 'AbortError') setError(errorOf(cause)) })
    return () => controller.abort()
  }, [invoiceId, revision])

  function changeProperty(value) {
    setPropertyId(value); setPage(1); setPreview(null); setContractId('')
    if (invoiceId) navigate('landlord/billing')
  }
  const period = month.split('-').map(Number)
  const monthlyPayload = { contractId, year: period[0], month: period[1] }
  async function action(request, message, after) {
    setBusy(true); setError(''); setSuccess('')
    try {
      const result = await request()
      setSuccess(message)
      setRevision(value => value + 1)
      after?.(result)
    } catch (cause) { setError(errorOf(cause)) }
    finally { setBusy(false) }
  }
  const roomName = id => rooms.find(room => room.id === id)?.roomNumber || id?.slice(0, 8) || '—'
  const selectedContract = contracts.find(item => item.id === contractId)
  const activeContracts = contracts.filter(item => item.status === 'Active')
  const depositContracts = contracts.filter(item => item.status === 'Active' || item.status === 'Draft')

  return <div className="bill-page">
    <header className="bill-header"><div><span>QUẢN LÝ THU TIỀN</span><h1>Hóa đơn</h1><p>Nhập dữ liệu tính tiền, xem trước rồi phát hành hóa đơn cho người thuê.</p></div>
      <label>Khu trọ<select value={propertyId} onChange={event => changeProperty(event.target.value)}><option value="">Chọn khu trọ</option>{properties.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label></header>
    <div className="bill-tabs" role="group" aria-label="Quản lý hóa đơn"><button type="button" aria-pressed={tab === 'invoices'} className={tab === 'invoices' ? 'is-active' : ''} onClick={() => setTab('invoices')}>Hóa đơn</button><button type="button" aria-pressed={tab === 'inputs'} className={tab === 'inputs' ? 'is-active' : ''} onClick={() => setTab('inputs')}>Giá, chỉ số và dịch vụ</button></div>
    {!propertyId && !loading && <p className="bill-empty">Bạn chưa có khu trọ để lập hóa đơn.</p>}
    {propertyId && tab === 'inputs' && <BillingInputs propertyId={propertyId} rooms={rooms} onChanged={() => setPreview(null)} />}
    {propertyId && tab === 'invoices' && <>
      <div className="bill-grid bill-grid--two">
        <section className="bill-card">
          <h2>Tạo hóa đơn tháng</h2><p>Chỉ hợp đồng đang hoạt động mới có thể lập hóa đơn tháng.</p>
          <div className="bill-form bill-form--inline">
            <label>Hợp đồng<select value={contractId} onChange={event => { setContractId(event.target.value); setPreview(null) }}><option value="">Chọn hợp đồng</option>{activeContracts.map(item => <option key={item.id} value={item.id}>{item.contractCode} · Phòng {roomName(item.roomId)}</option>)}</select></label>
            <label>Kỳ hóa đơn<input type="month" min="2000-01" max="2100-12" value={month} onChange={event => { setMonth(event.target.value); setPreview(null) }} /></label>
            <button className="bill-button" type="button" disabled={!contractId || !month || busy} onClick={() => action(() => billingApi.preview(propertyId, monthlyPayload), 'Đã tính bản xem trước.', setPreview)}>Xem trước</button>
          </div>
          {preview && <div className="bill-preview"><h3>Bản xem trước · {preview.month}/{preview.year}</h3><p>Hạn thanh toán: {preview.dueDate}</p><InvoiceItems items={preview.items} /><div className="bill-total"><span>Tổng cộng</span><strong>{money(preview.totalAmount)}</strong></div><button className="bill-button" type="button" disabled={busy} onClick={() => action(() => billingApi.createMonthly(propertyId, monthlyPayload), 'Đã tạo hóa đơn nháp.', result => { setPreview(null); navigate(`landlord/billing/${result.id}`) })}>Tạo bản nháp</button></div>}
          {!preview && <p className="bill-hint">Cần có giá điện nước và chỉ số của cả điện, nước cho kỳ này. Chọn tab “Giá, chỉ số và dịch vụ” để nhập.</p>}
        </section>
        <section className="bill-card"><h2>Tạo hóa đơn tiền cọc</h2><p>Áp dụng cho hợp đồng nháp hoặc đang hoạt động có khoản cọc cần thu.</p>
          <ul className="bill-list">{depositContracts.filter(item => Number(item.depositAmount) > 0).map(item => <li key={item.id}><div><strong>{item.contractCode} · Phòng {roomName(item.roomId)}</strong><small>Tiền cọc {money(item.depositAmount)}</small></div><button className="bill-button bill-button--secondary" type="button" disabled={busy} onClick={() => action(() => billingApi.createDeposit(item.id), 'Đã tạo hóa đơn cọc nháp.', result => navigate(`landlord/billing/${result.id}`))}>Tạo hóa đơn cọc</button></li>)}</ul>
          {!depositContracts.some(item => Number(item.depositAmount) > 0) && <p className="bill-empty">Chưa có hợp đồng phù hợp.</p>}
        </section>
      </div>
      <section className="bill-card"><h2>Danh sách hóa đơn</h2>{loading ? <p role="status">Đang tải...</p> : invoices.length ? <div className="bill-table-wrap"><table className="bill-table"><thead><tr><th>Mã / Kỳ</th><th>Phòng</th><th>Hạn thanh toán</th><th>Trạng thái</th><th>Còn phải thu</th><th></th></tr></thead><tbody>{invoices.map(item => <tr key={item.id}><td><strong>{item.code}</strong><small>{monthLabel(item)}</small></td><td>{roomName(item.roomId)}</td><td>{item.dueDate}</td><td>{statusLabel[item.displayStatus] || item.displayStatus}</td><td>{money(item.amountDue)}</td><td><button className="bill-link" type="button" onClick={() => navigate(`landlord/billing/${item.id}`)}>Xem chi tiết</button></td></tr>)}</tbody></table></div> : <p className="bill-empty">Chưa có hóa đơn.</p>}
        <div className="bill-pagination"><button type="button" disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Trang trước</button><span>Trang {page} / {Math.max(1, meta?.totalPages || 1)}</span><button type="button" disabled={page >= (meta?.totalPages || 1)} onClick={() => setPage(value => value + 1)}>Trang sau</button></div>
      </section>
      {invoiceId && <section className="bill-card bill-detail"><div className="bill-detail-head"><h2>Chi tiết hóa đơn</h2><button type="button" className="bill-link" onClick={() => navigate('landlord/billing')}>Đóng</button></div>{detail ? <><InvoiceSummary summary={detail.summary} roomName={roomName} /><InvoiceItems items={detail.items} /><div className="bill-actions">{detail.summary.status === 'Draft' && <button type="button" className="bill-button" disabled={busy} onClick={() => { if (window.confirm('Phát hành hóa đơn này? Người thuê sẽ nhìn thấy hóa đơn.')) action(() => billingApi.issue(invoiceId), 'Đã phát hành hóa đơn.') }}>Phát hành</button>}{detail.summary.status !== 'Cancelled' && <button type="button" className="bill-button bill-button--danger" disabled={busy} onClick={() => { if (window.confirm('Hủy hóa đơn này? Thao tác không thể hoàn tác.')) action(() => billingApi.cancel(invoiceId), 'Đã hủy hóa đơn.') }}>Hủy hóa đơn</button>}</div></> : <p role="status">Đang tải chi tiết...</p>}</section>}
    </>}
    {error && <p className="bill-error" role="alert">{error}</p>}
    {success && <p className="bill-success" role="status">{success}</p>}
    {selectedContract && <span className="sr-only">Hợp đồng đã chọn: {selectedContract.contractCode}</span>}
  </div>
}

export function InvoiceItems({ items = [] }) {
  return <div className="bill-table-wrap"><table className="bill-table"><thead><tr><th>Khoản tiền</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead><tbody>{items.map((item, index) => <tr key={`${item.id || item.type}-${index}`}><td>{item.description}</td><td>{item.quantity}</td><td>{money(item.unitPrice)}</td><td>{money(item.amount)}</td></tr>)}</tbody></table></div>
}

export function InvoiceSummary({ summary, roomName }) {
  return <><dl className="bill-summary"><div><dt>Mã</dt><dd>{summary.code}</dd></div><div><dt>Phòng</dt><dd>{roomName?.(summary.roomId) || summary.roomId}</dd></div><div><dt>Trạng thái</dt><dd>{statusLabel[summary.displayStatus] || summary.displayStatus}</dd></div><div><dt>Hạn thanh toán</dt><dd>{summary.dueDate}</dd></div><div><dt>Tổng ban đầu</dt><dd>{money(summary.baseTotal)}</dd></div><div><dt>Tổng hiệu lực</dt><dd>{money(summary.effectiveTotal)}</dd></div><div><dt>Đã thanh toán</dt><dd>{money(summary.totalPaid)}</dd></div><div><dt>Đã dùng cọc</dt><dd>{money(summary.totalDepositApplied)}</dd></div><div><dt>Còn phải thu</dt><dd><strong>{money(summary.amountDue)}</strong></dd></div>{Number(summary.creditToRefund) > 0 && <div><dt>Tiền dư</dt><dd>{money(summary.creditToRefund)}</dd></div>}</dl></>
}
