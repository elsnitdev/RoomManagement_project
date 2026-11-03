import { useEffect, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'
import { InvoiceItems, InvoiceSummary } from './LandlordBilling'
import '../../styles/billing-live.css'

export default function TenantBilling() {
  const [page, setPage] = useState(1)
  const [invoices, setInvoices] = useState([])
  const [meta, setMeta] = useState(null)
  const [selectedId, setSelectedId] = useState('')
  const [detail, setDetail] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true); setError('')
    billingApi.myInvoices(page, controller.signal).then(response => {
      setInvoices(response.data || []); setMeta(response.meta)
      setSelectedId(current => response.data?.some(item => item.id === current) ? current : response.data?.[0]?.id || '')
    }).catch(cause => { if (cause.name !== 'AbortError') setError(cause.message) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [page])
  useEffect(() => {
    if (!selectedId) { setDetail(null); return }
    const controller = new AbortController()
    setDetail(null)
    billingApi.myInvoice(selectedId, controller.signal).then(setDetail)
      .catch(cause => { if (cause.name !== 'AbortError') setError(cause.message) })
    return () => controller.abort()
  }, [selectedId])

  return <div className="bill-page"><header className="bill-header"><div><span>TÀI CHÍNH</span><h1>Hóa đơn của tôi</h1><p>Xem các hóa đơn đã phát hành, khoản phải thu và chi tiết từng dòng tiền.</p></div></header>
    {error && <p className="bill-error" role="alert">{error}</p>}
    {loading ? <p role="status">Đang tải hóa đơn...</p> : invoices.length ? <div className="bill-grid bill-grid--two"><section className="bill-card"><h2>Hóa đơn đã phát hành</h2><ul className="bill-list bill-list--select">{invoices.map(item => <li key={item.id}><button type="button" className={selectedId === item.id ? 'is-active' : ''} onClick={() => setSelectedId(item.id)} aria-pressed={selectedId === item.id}><strong>{item.code}</strong><small>{item.billingMonth ? `Kỳ ${item.billingMonth}/${item.billingYear}` : 'Tiền cọc'} · Hạn {item.dueDate}</small><span>Còn phải thu: {new Intl.NumberFormat('vi-VN').format(Number(item.amountDue || 0))} ₫</span></button></li>)}</ul><div className="bill-pagination"><button type="button" disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Trang trước</button><span>Trang {page} / {Math.max(1, meta?.totalPages || 1)}</span><button type="button" disabled={page >= (meta?.totalPages || 1)} onClick={() => setPage(value => value + 1)}>Trang sau</button></div></section>
      <section className="bill-card"><h2>Chi tiết</h2>{detail ? <><InvoiceSummary summary={detail.summary} /><InvoiceItems items={detail.items} /></> : <p role="status">Đang tải chi tiết...</p>}</section></div> : <p className="bill-empty">Bạn chưa có hóa đơn đã phát hành.</p>}
  </div>
}
