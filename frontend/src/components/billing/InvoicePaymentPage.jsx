import { useMemo, useState } from 'react'
import { ArrowUpRight, CalendarDays, CheckCircle2, FileText, Search, Wallet } from 'lucide-react'
import '../../styles/invoice-payment.css'

const money = value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value)
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
const statusLabels = { paid: 'Đã thanh toán', partial: 'Thanh toán một phần', unpaid: 'Chưa thanh toán', overdue: 'Quá hạn' }

function InvoiceStatus({ status }) {
  return <span className={`ip-status ip-status--${status}`}>{statusLabels[status]}</span>
}

function Metric({ icon: Icon, label, value, note, tone }) {
  return <div className={`ip-metric ip-metric--${tone}`}><span className="ip-metric-icon"><Icon size={19} aria-hidden="true" /></span><span className="ip-metric-label">{label}</span><strong>{value}</strong><small>{note}</small></div>
}

function InvoiceDetail({ invoice }) {
  if (!invoice) return <aside className="ip-detail ip-detail--empty"><FileText size={30} aria-hidden="true" /><strong>Chọn một hóa đơn</strong><p>Thông tin thanh toán sẽ hiển thị tại đây.</p></aside>
  const balance = balanceOf(invoice)
  const status = statusOf(invoice)
  return <aside className="ip-detail" aria-label={`Chi tiết hóa đơn ${invoice.id}`}>
    <div className="ip-detail-head"><div><span className="ip-eyebrow">CHI TIẾT HÓA ĐƠN</span><h3>{invoice.id}</h3><p>Kỳ thanh toán {invoice.month || '—'}</p></div><InvoiceStatus status={status} /></div>
    <div className="ip-detail-person"><span>Phòng <strong>{invoice.room || '—'}</strong></span><span>Người thuê <strong>{invoice.tenant || '—'}</strong></span></div>
    <div className="ip-detail-balance"><span>Còn phải thu</span><strong>{money(balance)}</strong><small><CalendarDays size={15} aria-hidden="true" /> Hạn thanh toán {invoice.due || '—'}</small></div>
    <dl className="ip-detail-totals"><div><dt>Tổng hóa đơn</dt><dd>{money(invoice.amount)}</dd></div><div><dt>Đã ghi nhận</dt><dd>{money(invoice.paid)}</dd></div><div className="ip-detail-totals-final"><dt>Còn phải thu</dt><dd>{money(balance)}</dd></div></dl>
    <div className="ip-detail-section"><h4>Khoản thu</h4>{Array.isArray(invoice.items) && invoice.items.length > 0
      ? <ul className="ip-line-items">{invoice.items.map((item, index) => <li key={item.id || index}><span>{item.description || 'Khoản thu'}{item.quantity != null && <small>{item.quantity} × {money(item.unitPrice)}</small>}</span><strong>{money(item.amount)}</strong></li>)}</ul>
      : <p>Chưa có chi tiết từng khoản thu trong dữ liệu hiện tại.</p>}</div>
    <div className="ip-detail-section"><h4>Thanh toán</h4><p>{invoice.paid > 0 ? `Đã ghi nhận tổng cộng ${money(invoice.paid)}. Chưa có lịch sử từng giao dịch.` : 'Chưa ghi nhận khoản thanh toán nào.'}</p></div>
    <p className="ip-detail-notice">Chức năng ghi nhận thanh toán cần API và dữ liệu thực tế trước khi sử dụng.</p>
  </aside>
}

export default function InvoicePaymentPage({ invoices = [] }) {
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('all')
  const [selectedId, setSelectedId] = useState(null)
  const summary = useMemo(() => ({
    paid: invoices.reduce((sum, invoice) => sum + Number(invoice.paid || 0), 0),
    balance: invoices.reduce((sum, invoice) => sum + balanceOf(invoice), 0),
    open: invoices.filter(invoice => balanceOf(invoice) > 0).length,
  }), [invoices])
  const visible = useMemo(() => invoices.filter(invoice => {
    const matchesStatus = filter === 'all' || statusOf(invoice) === filter
    const haystack = `${invoice.id} ${invoice.room} ${invoice.tenant} ${invoice.month}`.toLocaleLowerCase('vi-VN')
    return matchesStatus && haystack.includes(search.trim().toLocaleLowerCase('vi-VN'))
  }), [filter, invoices, search])
  const selected = visible.find(invoice => invoice.id === selectedId) || visible[0] || null

  return <div className="ip-page">
    <header className="ip-heading"><div><span className="ip-eyebrow">TÀI CHÍNH</span><h1>Hóa đơn &amp; thanh toán</h1><p>Theo dõi số tiền đã ghi nhận và các khoản còn phải thu theo từng phòng.</p></div><span className="ip-demo-note">Dữ liệu mẫu · Chưa kết nối API thanh toán</span></header>
    <div className="ip-metrics"><Metric icon={FileText} label="Hóa đơn" value={String(invoices.length)} note="Tổng trong dữ liệu mẫu" tone="blue" /><Metric icon={CheckCircle2} label="Đã ghi nhận" value={money(summary.paid)} note="Tổng tiền đã thu" tone="green" /><Metric icon={Wallet} label="Còn phải thu" value={money(summary.balance)} note={`${summary.open} hóa đơn còn dư nợ`} tone="amber" /></div>
    <section className="ip-workspace" aria-label="Danh sách và chi tiết hóa đơn"><div className="ip-list"><div className="ip-list-head"><div><span className="ip-eyebrow">DANH SÁCH</span><h2>Hóa đơn</h2></div><span>{visible.length} kết quả</span></div>
      <div className="ip-toolbar"><label className="ip-search"><Search size={18} aria-hidden="true" /><span className="sr-only">Tìm hóa đơn</span><input value={search} onChange={event => setSearch(event.target.value)} placeholder="Mã, phòng hoặc người thuê" /></label><label className="ip-filter"><span className="sr-only">Lọc trạng thái</span><select value={filter} onChange={event => setFilter(event.target.value)}><option value="all">Tất cả trạng thái</option><option value="unpaid">Chưa thanh toán</option><option value="partial">Thanh toán một phần</option><option value="overdue">Quá hạn</option><option value="paid">Đã thanh toán</option></select></label></div>
      {visible.length === 0 ? <div className="ip-empty"><FileText size={28} aria-hidden="true" /><strong>Không có hóa đơn phù hợp</strong><p>Thử thay đổi từ khóa hoặc trạng thái.</p></div> : <ul className="ip-invoices">{visible.map(invoice => {
        const status = statusOf(invoice)
        return <li key={invoice.id}><button type="button" className={`ip-invoice ${selected?.id === invoice.id ? 'is-selected' : ''}`} onClick={() => setSelectedId(invoice.id)} aria-pressed={selected?.id === invoice.id}>
          <span className="ip-invoice-icon"><FileText size={19} aria-hidden="true" /></span><span className="ip-invoice-main"><strong>{invoice.id}</strong><small>Phòng {invoice.room} · {invoice.tenant}</small><span className="ip-invoice-meta"><CalendarDays size={13} aria-hidden="true" /> Kỳ {invoice.month} · Hạn {invoice.due}</span></span><span className="ip-invoice-side"><span>Tổng {money(invoice.amount)}</span><strong>Còn {money(balanceOf(invoice))}</strong><InvoiceStatus status={status} /></span><ArrowUpRight className="ip-invoice-arrow" size={16} aria-hidden="true" />
        </button></li>
      })}</ul>}
    </div><InvoiceDetail invoice={selected} /></section>
  </div>
}
