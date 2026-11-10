import { useEffect, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'
import { formatVietnamDate } from './paymentDate'

const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const newForm = () => ({ amount: '', transactionCode: '', idempotencyKey: crypto.randomUUID() })

export default function OverpaymentRefund({ invoiceId, creditToRefund, onChanged }) {
  const [form, setForm] = useState(newForm)
  const [refunds, setRefunds] = useState([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    setLoading(true); setError('')
    billingApi.overpaymentRefunds(invoiceId, controller.signal).then(rows => { if (!controller.signal.aborted) setRefunds(rows || []) })
      .catch(cause => { if (!controller.signal.aborted) setError(cause.message) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [invoiceId, creditToRefund, revision])
  function update(field, value) { setForm(current => ({ ...current, [field]: value, idempotencyKey: crypto.randomUUID() })) }
  async function submit(event) {
    event.preventDefault(); setError('')
    const amount = Number(form.amount)
    if (!Number.isFinite(amount) || amount <= 0 || amount > Number(creditToRefund)) { setError('Số tiền đã hoàn phải lớn hơn 0 và không vượt số tiền dư hiện tại.'); return }
    if (!window.confirm(`Bạn xác nhận đã hoàn ${money(amount)} cho người thuê bên ngoài hệ thống?`)) return
    setBusy(true)
    try {
      await billingApi.recordOverpaymentRefund(invoiceId, { ...form, amount, transactionCode: form.transactionCode.trim() || null })
      setForm(newForm()); setRevision(value => value + 1); onChanged('Đã ghi nhận khoản hoàn tiền thu vượt.')
    } catch (cause) { setError(cause.message) }
    finally { setBusy(false) }
  }
  if (!loading && !error && refunds.length === 0 && Number(creditToRefund) <= 0) return null
  return <section className="bill-card"><h2>Hoàn tiền thu vượt</h2>
    <p>Chủ trọ tự hoàn tiền cho người thuê bên ngoài hệ thống, sau đó ghi nhận tại đây. Tiền dư hiện tại: <strong>{money(creditToRefund)}</strong>.</p>
    {Number(creditToRefund) > 0 && <form className="bill-form" onSubmit={submit}><label>Số tiền đã hoàn *<input type="number" min="0.01" step="any" required value={form.amount} onChange={event => update('amount', event.target.value)} /></label><label>Mã giao dịch hoàn (nếu có)<input maxLength={150} value={form.transactionCode} onChange={event => update('transactionCode', event.target.value)} /></label><button type="submit" className="bill-button" disabled={busy || !form.amount}>{busy ? 'Đang ghi…' : 'Xác nhận đã hoàn tiền'}</button></form>}
    {error && <p className="bill-error" role="alert">{error} {loading || busy ? null : <button type="button" className="bill-link" onClick={() => setRevision(value => value + 1)}>Tải lại lịch sử</button>}</p>}
    {loading ? <p role="status">Đang tải lịch sử hoàn tiền…</p> : refunds.length > 0 && <ul className="bill-payment-list">{refunds.map(item => <li key={item.id}><strong>Đã hoàn {money(item.amount)}</strong><small>{formatVietnamDate(item.refundedAt, true)}{item.transactionCode && ` · Mã ${item.transactionCode}`}</small></li>)}</ul>}
  </section>
}
