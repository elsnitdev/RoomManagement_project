import { useEffect, useMemo, useRef, useState } from 'react'
import { AlertCircle, ArrowLeft, CalendarDays, CheckCircle2, ChevronRight, CircleDashed, Clock3, Minus, Plus, ReceiptText, Search, X } from 'lucide-react'
import '../../styles/invoice-payment.css'

const money = value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(Number(value) || 0)
const parseDate = value => {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value || '')
  return match ? new Date(Number(match[3]), Number(match[2]) - 1, Number(match[1])) : null
}
const balanceOf = invoice => Math.max(0, Number(invoice.amount || 0) - Number(invoice.paid || 0))
const statusOf = invoice => {
  if (balanceOf(invoice) === 0) return 'paid'
  const due = parseDate(invoice.due)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  if (due && due < today) return 'overdue'
  return Number(invoice.paid || 0) > 0 ? 'partial' : 'unpaid'
}
const statuses = {
  all: { label: 'Tất cả' },
  unpaid: { label: 'Chưa thu', icon: CircleDashed },
  partial: { label: 'Một phần', icon: Clock3 },
  paid: { label: 'Đã thu', icon: CheckCircle2 },
  overdue: { label: 'Quá hạn', icon: AlertCircle },
}

function InvoiceStatus({ status }) {
  const { icon: Icon, label } = statuses[status]
  return <span className={'ip-status ip-status--' + status}><Icon size={13} strokeWidth={2.3} aria-hidden="true" />{label}</span>
}

function AdjustmentForm({ invoices, mode, initialInvoiceId, formRef, onCancel, onSubmit }) {
  const isSingle = mode === 'single'
  const [selectedIds, setSelectedIds] = useState(isSingle ? [initialInvoiceId] : [])
  const [direction, setDirection] = useState('add')
  const [amountText, setAmountText] = useState('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const selected = invoices.filter(item => selectedIds.includes(item.id))
  const eligible = invoices.filter(item => direction === 'add' || balanceOf(item) > 0)
  const allSelected = eligible.length > 0 && eligible.every(item => selectedIds.includes(item.id))
  const amount = Number(amountText)
  const invalidTargets = direction === 'subtract' && amount > 0 ? selected.filter(item => amount > balanceOf(item)) : []
  const currentTotal = selected.reduce((sum, item) => sum + Number(item.amount), 0)
  const adjustedTotal = currentTotal + (direction === 'add' ? 1 : -1) * amount * selected.length

  function changeDirection(next) {
    setDirection(next)
    if (!isSingle && next === 'subtract') setSelectedIds(ids => ids.filter(id => invoices.some(item => item.id === id && balanceOf(item) > 0)))
    setError('')
  }
  function toggleInvoice(id) {
    setSelectedIds(ids => ids.includes(id) ? ids.filter(item => item !== id) : [...ids, id])
    setError('')
  }
  function submit(event) {
    event.preventDefault()
    if (!selected.length) return setError('Hãy chọn ít nhất một phòng có hóa đơn.')
    if (!Number.isSafeInteger(amount) || amount <= 0) return setError('Nhập số tiền lớn hơn 0 đồng.')
    if (reason.trim().length < 5) return setError('Ghi rõ lý do điều chỉnh, ít nhất 5 ký tự.')
    if (invalidTargets.length) return setError(`Khoản trừ vượt số còn thu của phòng ${invalidTargets.map(item => item.room).join(', ')}.`)
    onSubmit({ invoiceIds: selected.map(item => item.id), direction, amount, reason: reason.trim() })
  }

  return <section ref={formRef} tabIndex={-1} className="ip-adjustment" aria-labelledby="ip-adjustment-title">
    <div className="ip-adjustment-head"><div><span className="ip-eyebrow">{isSingle ? 'HÓA ĐƠN RIÊNG' : 'ÁP DỤNG CHO NHIỀU PHÒNG'}</span><h2 id="ip-adjustment-title">{isSingle ? `Điều chỉnh phòng ${selected[0]?.room || '—'}` : 'Tạo khoản cộng/trừ cho nhiều phòng'}</h2><p>{isSingle ? 'Dùng cho khấu hao, chi phí hoặc ưu đãi riêng của phòng này.' : 'Một số tiền và một lý do được áp dụng cho mỗi hóa đơn được chọn.'}</p></div><button type="button" className="ip-adjustment-close" onClick={onCancel} aria-label="Đóng biểu mẫu"><X size={18} aria-hidden="true" /></button></div>
    <form onSubmit={submit} noValidate>
      {isSingle ? <div className="ip-single-target"><ReceiptText size={17} aria-hidden="true" /><span>{selected[0]?.id} · Phòng {selected[0]?.room} · {selected[0]?.tenant}</span></div>
        : <fieldset className="ip-targets"><legend>Chọn phòng áp dụng <span>{selected.length}/{invoices.length} hóa đơn</span></legend><label className="ip-target-all"><input type="checkbox" checked={allSelected} onChange={() => { setSelectedIds(allSelected ? [] : eligible.map(item => item.id)); setError('') }} disabled={!eligible.length} />Chọn tất cả phòng có thể áp dụng</label><div className="ip-target-grid">{invoices.map(item => { const disabled = direction === 'subtract' && balanceOf(item) === 0; return <label key={item.id} className={'ip-target' + (disabled ? ' is-disabled' : '')}><input type="checkbox" checked={selectedIds.includes(item.id)} onChange={() => toggleInvoice(item.id)} disabled={disabled} /><span><strong>Phòng {item.room}</strong><small>{item.tenant} · {item.id}</small></span><em>{disabled ? 'Đã thu đủ' : `Còn ${money(balanceOf(item))}`}</em></label> })}</div></fieldset>}
      <div className="ip-adjustment-fields"><fieldset className="ip-direction"><legend>Loại điều chỉnh</legend><div><button type="button" className={direction === 'add' ? 'is-active' : ''} onClick={() => changeDirection('add')} aria-pressed={direction === 'add'}><Plus size={16} aria-hidden="true" /> Cộng tiền</button><button type="button" className={direction === 'subtract' ? 'is-active' : ''} onClick={() => changeDirection('subtract')} aria-pressed={direction === 'subtract'}><Minus size={16} aria-hidden="true" /> Trừ tiền</button></div></fieldset>
        <label className="ip-field ip-field--amount"><span>Số tiền cho mỗi hóa đơn</span><span className="ip-amount-input"><input value={amountText} onChange={event => { setAmountText(event.target.value.replace(/\D/g, '').slice(0, 12)); setError('') }} inputMode="numeric" pattern="[0-9]*" placeholder="Nhập số tiền" aria-describedby="ip-amount-hint" required /><b>đ</b></span><small id="ip-amount-hint">Chỉ nhập số nguyên dương{direction === 'subtract' && selected.length ? ` · Tối đa ${money(Math.min(...selected.map(balanceOf)))}/phòng` : ''}</small></label>
        <label className="ip-field ip-field--reason"><span>Lý do áp dụng</span><textarea value={reason} onChange={event => { setReason(event.target.value); setError('') }} placeholder="Ví dụ: Phụ thu điện; khấu hao thiết bị; giảm phí do sửa chữa phòng…" rows={3} maxLength={240} required /></label></div>
      <div className="ip-adjustment-footer"><div className="ip-adjustment-preview"><span>{selected.length} hóa đơn được chọn <strong>{money(currentTotal)}</strong></span><ChevronRight size={16} aria-hidden="true" /><span>Tổng sau điều chỉnh <strong className={invalidTargets.length ? 'is-invalid' : ''}>{invalidTargets.length ? 'Vượt số còn thu' : money(adjustedTotal)}</strong></span></div><div className="ip-adjustment-actions"><button type="button" className="ip-button ip-button--quiet" onClick={onCancel}>Hủy</button><button type="submit" className="ip-button ip-button--primary">Áp dụng cho {selected.length} hóa đơn</button></div></div>
      {error && <p className="ip-form-error" role="alert"><AlertCircle size={15} aria-hidden="true" />{error}</p>}
    </form>
  </section>
}

function InvoiceDetail({ invoice, onViewDetail }) {
  if (!invoice) return <aside className="ip-detail ip-detail--empty"><ReceiptText size={27} strokeWidth={1.8} aria-hidden="true" /><strong>Chưa có hóa đơn để hiển thị</strong><p>Thử thay đổi bộ lọc hoặc từ khóa tìm kiếm.</p></aside>
  const balance = balanceOf(invoice)
  const items = Array.isArray(invoice.items) ? invoice.items : []
  const adjustments = Array.isArray(invoice.adjustments) ? invoice.adjustments : []
  return <aside className="ip-detail" aria-label={'Chi tiết hóa đơn ' + invoice.id}>
    <div className="ip-detail-topline"><span>PHIẾU HÓA ĐƠN</span><span>Kỳ {invoice.month || '—'}</span></div>
    <div className="ip-detail-heading"><div><h2>{invoice.id}</h2><p>Chi tiết khoản phải thu theo phòng</p></div><InvoiceStatus status={statusOf(invoice)} /></div>
    {onViewDetail && <button type="button" className="ip-detail-view" onClick={onViewDetail}>Xem chi tiết hóa đơn <ChevronRight size={15} aria-hidden="true" /></button>}
    <div className="ip-detail-party"><div><span>Phòng</span><strong>{invoice.room || '—'}</strong></div><div><span>Người thuê</span><strong>{invoice.tenant || '—'}</strong></div></div>
    <div className="ip-detail-due"><CalendarDays size={16} aria-hidden="true" /><span>Hạn thanh toán</span><strong>{invoice.due || '—'}</strong></div>
    <div className="ip-detail-balance"><span>{balance > 0 ? 'Còn phải thu' : 'Đã thanh toán đủ'}</span><strong>{money(balance)}</strong><small>{balance > 0 ? 'Số dư chưa được ghi nhận thanh toán' : 'Không còn khoản phải thu trên hóa đơn này'}</small></div>
    <section className="ip-detail-section"><h3>Các khoản trên hóa đơn</h3>{items.length > 0
      ? <ul className="ip-line-items">{items.map((item, index) => <li key={item.id || index}><span>{item.description || 'Khoản thu'}{item.quantity != null && <small>{item.quantity} × {money(item.unitPrice)}</small>}</span><strong>{money(item.amount)}</strong></li>)}</ul>
      : <p>Dữ liệu mẫu chưa có chi tiết từng khoản thu.</p>}</section>
    {adjustments.length > 0 && <section className="ip-detail-section"><h3>Điều chỉnh đã thêm</h3><ul className="ip-adjustment-history">{adjustments.map(item => <li key={item.id}><div><strong>{item.reason}</strong><small>{item.direction === 'add' ? 'Cộng tiền' : 'Trừ tiền'}</small></div><b className={item.direction === 'add' ? 'is-add' : 'is-subtract'}>{item.direction === 'add' ? '+' : '−'} {money(item.amount)}</b></li>)}</ul></section>}
    <dl className="ip-detail-totals"><div><dt>Tổng hóa đơn</dt><dd>{money(invoice.amount)}</dd></div><div><dt>Đã ghi nhận</dt><dd>− {money(invoice.paid)}</dd></div><div className="ip-detail-totals-final"><dt>Còn phải thu</dt><dd>{money(balance)}</dd></div></dl>
    <section className="ip-detail-section ip-detail-section--payment"><h3>Ghi nhận thanh toán</h3><p>{Number(invoice.paid) > 0 ? 'Đã ghi nhận ' + money(invoice.paid) + '. Dữ liệu mẫu chưa có lịch sử từng giao dịch.' : 'Chưa ghi nhận khoản thanh toán nào.'}</p></section>
  </aside>
}

export default function InvoicePaymentPage({ invoices = [], historyInvoices = [], invoiceId, navigate, onAddAdjustment }) {
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('all')
  const [selectedId, setSelectedId] = useState(null)
  const [adjustment, setAdjustment] = useState(null)
  const adjustmentRef = useRef(null)
  useEffect(() => {
    if (!adjustment) return
    adjustmentRef.current?.focus({ preventScroll: true })
    adjustmentRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [adjustment])
  const summary = useMemo(() => ({
    total: invoices.reduce((sum, invoice) => sum + Number(invoice.amount || 0), 0),
    paid: invoices.reduce((sum, invoice) => sum + Number(invoice.paid || 0), 0),
    balance: invoices.reduce((sum, invoice) => sum + balanceOf(invoice), 0),
  }), [invoices])
  const counts = useMemo(() => invoices.reduce((result, invoice) => {
    result.all += 1
    result[statusOf(invoice)] += 1
    return result
  }, { all: 0, unpaid: 0, partial: 0, paid: 0, overdue: 0 }), [invoices])
  const visible = useMemo(() => invoices.filter(invoice => {
    const matchesStatus = filter === 'all' || statusOf(invoice) === filter
    const haystack = [invoice.id, invoice.room, invoice.tenant, invoice.month].join(' ').toLocaleLowerCase('vi-VN')
    return matchesStatus && haystack.includes(search.trim().toLocaleLowerCase('vi-VN'))
  }), [filter, invoices, search])
  const selected = visible.find(invoice => invoice.id === selectedId) || visible[0] || null
  const detailInvoice = invoiceId ? [...invoices, ...historyInvoices].find(invoice => invoice.id === invoiceId) : null
  const isCurrentInvoice = Boolean(detailInvoice && invoices.some(invoice => invoice.id === detailInvoice.id))
  const roomHistory = detailInvoice ? [...invoices, ...historyInvoices].filter(invoice => invoice.room === detailInvoice.room && invoice.id !== detailInvoice.id).sort((a, b) => {
    const [monthA, yearA] = (a.month || '0/0').split('/').map(Number)
    const [monthB, yearB] = (b.month || '0/0').split('/').map(Number)
    return yearB - yearA || monthB - monthA
  }) : []
  const viewInvoice = id => { setAdjustment(null); navigate?.(`landlord/billing/${id}`) }
  const saveAdjustment = value => {
    onAddAdjustment?.(value)
    setSelectedId(value.invoiceIds[0])
    setFilter('all')
    setSearch('')
    setAdjustment(null)
    if (adjustment?.mode === 'bulk') navigate?.(`landlord/billing/${value.invoiceIds[0]}`)
  }

  if (invoiceId) return <div className="ip-page ip-page--detail">
    <button type="button" className="ip-back" onClick={() => { setAdjustment(null); navigate?.('landlord/billing') }}><ArrowLeft size={16} aria-hidden="true" /> Danh sách hóa đơn</button>
    <header className="ip-heading"><div><span className="ip-eyebrow">TÀI CHÍNH / HÓA ĐƠN / CHI TIẾT</span><h1>{detailInvoice ? `Hóa đơn phòng ${detailInvoice.room}` : 'Không tìm thấy hóa đơn'}</h1><p>{detailInvoice ? `Kỳ ${detailInvoice.month} · ${detailInvoice.tenant}` : 'Hóa đơn không có trong dữ liệu mẫu.'}</p></div><div className="ip-heading-actions"><span className="ip-demo-note">Dữ liệu mẫu · Chưa kết nối thanh toán</span>{detailInvoice && <><button type="button" className="ip-button ip-button--quiet" onClick={() => setAdjustment({ mode: 'bulk' })}><Plus size={16} aria-hidden="true" /> Tạo cho nhiều phòng</button>{isCurrentInvoice && <button type="button" className="ip-button ip-button--primary" onClick={() => setAdjustment({ mode: 'single', invoiceId: detailInvoice.id })}><Plus size={16} aria-hidden="true" /> Tạo riêng cho phòng này</button>}</>}</div></header>
    {adjustment && <AdjustmentForm key={`${adjustment.mode}-${adjustment.invoiceId || ''}`} invoices={invoices} mode={adjustment.mode} initialInvoiceId={adjustment.invoiceId} formRef={adjustmentRef} onCancel={() => setAdjustment(null)} onSubmit={saveAdjustment} />}
    {detailInvoice ? <div className="ip-detail-layout"><InvoiceDetail invoice={detailInvoice} /><section className="ip-history" aria-label={`Lịch sử hóa đơn phòng ${detailInvoice.room}`}><div className="ip-history-head"><span className="ip-eyebrow">PHÒNG {detailInvoice.room}</span><h2>Lịch sử hóa đơn</h2><p>Các kỳ hóa đơn khác của phòng này.</p></div>{roomHistory.length ? <ul>{roomHistory.map(invoice => <li key={invoice.id}><div><span>Kỳ {invoice.month}</span><strong>{invoice.id}</strong><small>Hạn {invoice.due} · {money(invoice.amount)}</small></div><div><InvoiceStatus status={statusOf(invoice)} /><button type="button" className="ip-history-link" onClick={() => viewInvoice(invoice.id)}>Xem chi tiết <ChevronRight size={14} aria-hidden="true" /></button></div></li>)}</ul> : <p className="ip-history-empty">Phòng này chưa có hóa đơn kỳ trước trong dữ liệu mẫu.</p>}</section></div> : <div className="ip-empty"><ReceiptText size={26} aria-hidden="true" /><strong>Không tìm thấy hóa đơn</strong><p>Quay lại danh sách để chọn hóa đơn khác.</p></div>}
  </div>

  return <div className="ip-page">
    <header className="ip-heading"><div><span className="ip-eyebrow">TÀI CHÍNH / INVOICES</span><h1>Hóa đơn</h1><p>Theo dõi từng kỳ thu, số đã ghi nhận và số dư của mỗi phòng.</p></div><span className="ip-demo-note">Dữ liệu mẫu · Chưa kết nối thanh toán</span></header>
    <section className="ip-summary" aria-label="Tổng quan hóa đơn"><div><span>Tổng hóa đơn</span><strong>{money(summary.total)}</strong><small>{invoices.length} hóa đơn</small></div><div><span>Đã ghi nhận</span><strong>{money(summary.paid)}</strong><small>Tiền đã thu</small></div><div className="ip-summary-outstanding"><span>Còn phải thu</span><strong>{money(summary.balance)}</strong><small>{invoices.filter(invoice => balanceOf(invoice) > 0).length} hóa đơn còn số dư</small></div></section>
    <section className="ip-workspace" aria-label="Danh sách và chi tiết hóa đơn"><div className="ip-list">
      <div className="ip-list-head"><div><span className="ip-eyebrow">SỔ HÓA ĐƠN</span><h2>Danh sách hóa đơn</h2></div><span>{visible.length} / {invoices.length} hóa đơn</span></div>
      <div className="ip-toolbar"><label className="ip-search"><Search size={17} aria-hidden="true" /><span className="sr-only">Tìm hóa đơn theo mã, phòng hoặc người thuê</span><input value={search} onChange={event => setSearch(event.target.value)} placeholder="Tìm mã, phòng, người thuê…" /></label></div>
      <div className="ip-filters" role="group" aria-label="Lọc theo trạng thái">{Object.entries(statuses).map(([key, { label }]) => <button key={key} type="button" className={filter === key ? 'is-active' : ''} onClick={() => setFilter(key)} aria-pressed={filter === key}>{label}<span>{counts[key]}</span></button>)}</div>
      <div className="ip-ledger-head" aria-hidden="true"><div><span>Hóa đơn / Người thuê</span><span>Hạn thu</span><span>Tổng tiền</span><span>Còn thu</span><span>Trạng thái</span></div><span>Thao tác</span></div>
      {visible.length === 0 ? <div className="ip-empty"><Search size={24} strokeWidth={1.8} aria-hidden="true" /><strong>Không tìm thấy hóa đơn</strong><p>Thử từ khóa khác hoặc chọn một trạng thái khác.</p></div> : <ul className="ip-invoices">{visible.map(invoice => {
        const status = statusOf(invoice)
        return <li key={invoice.id} className="ip-invoice-row"><button type="button" className={'ip-invoice' + (selected?.id === invoice.id ? ' is-selected' : '')} onClick={() => setSelectedId(invoice.id)} aria-pressed={selected?.id === invoice.id} aria-label={'Xem hóa đơn ' + invoice.id + ', phòng ' + invoice.room + ', ' + statuses[status].label}>
          <span className="ip-invoice-identity"><strong>{invoice.id}</strong><small>Phòng {invoice.room} · {invoice.tenant}</small><em>Kỳ {invoice.month || '—'} · Tổng {money(invoice.amount)}</em></span><span className="ip-invoice-due">{invoice.due || '—'}</span><span className="ip-invoice-amount">{money(invoice.amount)}</span><strong className="ip-invoice-balance">{money(balanceOf(invoice))}</strong><span className="ip-invoice-status"><InvoiceStatus status={status} /><ChevronRight size={15} aria-hidden="true" /></span>
        </button><button type="button" className="ip-invoice-create" onClick={() => viewInvoice(invoice.id)} aria-label={`Xem chi tiết hóa đơn ${invoice.id} của phòng ${invoice.room}`}>Xem chi tiết <ChevronRight size={14} aria-hidden="true" /></button></li>
      })}</ul>}
    </div><InvoiceDetail invoice={selected} onViewDetail={selected ? () => viewInvoice(selected.id) : undefined} /></section>
  </div>
}
