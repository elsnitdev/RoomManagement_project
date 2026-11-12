import { useEffect, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'
import { formatVietnamDate } from './paymentDate'
import { EvidenceManager, EvidenceSelector } from './PaymentEvidence'

const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const newReceipt = () => ({ amount: '', method: 'BankTransfer', transactionCode: '', note: '', idempotencyKey: crypto.randomUUID() })
const statusText = { Pending: 'Chờ duyệt', Success: 'Đã xác nhận', Failed: 'Từ chối', Cancelled: 'Đã hủy' }

function ReviewPayment({ payment, amountDue, busy, onReview }) {
  const [amount, setAmount] = useState(String(payment.claimedAmount))
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [selection, setSelection] = useState(null)
  function submit(event) { event.preventDefault(); decide('Success') }
  function decide(status) {
    if (!reason.trim()) { setError('Hãy nhập lý do xác nhận hoặc từ chối.'); return }
    if (status === 'Success' && (!amount || Number(amount) <= 0)) { setError('Số tiền thực nhận phải lớn hơn 0.'); return }
    if (status === 'Success' && Number(amount) > Number(amountDue) && !window.confirm(`Số thực nhận ${money(amount)} vượt số còn phải thu ${money(amountDue)}. Bạn đã đối chiếu và xác nhận số tiền thực nhận này? Phần dư cần được hoàn cho người thuê.`)) return
    setError('')
    onReview(payment.id, status, status === 'Success' ? Number(amount) : null, reason.trim(), status === 'Success' ? selection : null)
  }
  return <li className="bill-payment-review">
    <strong>{payment.submittedByName || 'Người thuê'} · Đã khai {money(payment.claimedAmount)}</strong>
    <span>{payment.method === 'Cash' ? 'Đã giao tiền mặt' : 'Đã chuyển khoản'} · {payment.paidAt ? formatVietnamDate(payment.paidAt) : '—'}</span>
    {payment.transactionCode && <small>Mã giao dịch: {payment.transactionCode}</small>}{payment.note && <small>Ghi chú: {payment.note}</small>}
    <form className="bill-form bill-payment-form" onSubmit={submit}>
      <label>Số tiền thực nhận *<input type="number" min="0.01" step="any" required value={amount} onChange={event => setAmount(event.target.value)} /></label>
      <label>Lý do duyệt *<input maxLength={500} value={reason} onChange={event => setReason(event.target.value)} placeholder="Ví dụ: Đã đối chiếu sao kê" /></label>
      <EvidenceSelector selection={selection} onChange={setSelection} disabled={busy} />
      <p className="bill-hint bill-field--wide">Ảnh được lưu khi xác nhận thực nhận; nếu từ chối, ảnh không được lưu.</p>
      {error && <p className="bill-error bill-field--wide" role="alert">{error}</p>}
      <div className="bill-actions bill-field--wide"><button className="bill-button" disabled={busy} type="submit">Xác nhận thực nhận</button><button className="bill-button bill-button--danger" disabled={busy} type="button" onClick={() => decide('Failed')}>Từ chối</button></div>
    </form>
  </li>
}

export default function LandlordPaymentPanel({ invoiceId, amountDue, creditToRefund, onChanged, onRefresh }) {
  const [payments, setPayments] = useState([])
  const [receipt, setReceipt] = useState(newReceipt)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [actionError, setActionError] = useState('')
  const [revision, setRevision] = useState(0)
  const [selection, setSelection] = useState(null)
  const [pendingUpload, setPendingUpload] = useState(null)
  const [uploading, setUploading] = useState(false)
  useEffect(() => {
    if (!uploading && !pendingUpload) return
    const warn = event => { event.preventDefault(); event.returnValue = '' }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [uploading, pendingUpload])
  useEffect(() => {
    const controller = new AbortController()
    setLoading(true); setLoadError('')
    billingApi.landlordPayments(invoiceId, controller.signal).then(rows => {
      if (!controller.signal.aborted) setPayments(rows || [])
    }).catch(cause => { if (cause.name !== 'AbortError') setLoadError(cause.message) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [invoiceId, revision])

  async function act(request, message, image) {
    setBusy(true); setActionError('')
    try {
      const payment = await request()
      onChanged(image ? `${message} Đang tải ảnh bằng chứng…` : message)
      setRevision(value => value + 1)
      let uploadFailed = false
      if (image) {
        setUploading(true)
        try { await billingApi.uploadPaymentEvidence(payment.id, image) }
        catch (cause) { uploadFailed = true; setPendingUpload({ paymentId: payment.id, selection: image }); setActionError(`Khoản thanh toán đã được ghi nhận, nhưng chưa tải được ảnh: ${cause.message}. Hãy thử tải lại ảnh.`) }
        finally { setUploading(false) }
      }
      if (image) onChanged(uploadFailed ? 'Đã ghi nhận thanh toán; ảnh chưa tải lên.' : `${message} Đã lưu ảnh bằng chứng.`)
      setRevision(value => value + 1); return true
    }
    catch (cause) {
      setActionError(cause.message)
      setRevision(value => value + 1)
      if (cause.status === 409 && cause.details?.code === 'INVOICE_NO_AMOUNT_DUE') onRefresh?.()
      return false
    }
    finally { setBusy(false) }
  }
  function update(field, value) { setReceipt(current => ({ ...current, [field]: value, idempotencyKey: crypto.randomUUID() })) }
  async function record(event) {
    event.preventDefault()
    if (Number(amountDue) <= 0) {
      setActionError('Hóa đơn đã thu đủ hoặc đang dư tiền. Vui lòng tải lại hóa đơn.')
      onRefresh?.()
      return
    }
    if (Number(receipt.amount) > Number(amountDue) && !window.confirm(`Số thực nhận ${money(receipt.amount)} vượt số còn phải thu ${money(amountDue)}. Bạn đã đối chiếu số tiền thực nhận? Phần dư cần được hoàn cho người thuê.`)) return
    const body = {
      ...receipt, amount: Number(receipt.amount),
      transactionCode: receipt.method === 'BankTransfer' ? receipt.transactionCode.trim() || null : null,
      note: receipt.note.trim() || null,
    }
    if (await act(() => billingApi.recordReceived(invoiceId, body), 'Đã ghi nhận tiền thực nhận.', selection)) { setReceipt(newReceipt()); setSelection(null) }
  }
  function review(paymentId, status, verifiedAmount, reason, image) {
    act(() => billingApi.reviewPayment(paymentId, { status, verifiedAmount, reason }),
      status === 'Success' ? 'Đã xác nhận khoản thanh toán.' : 'Đã từ chối yêu cầu.', image)
  }
  async function retryUpload() {
    setBusy(true); setUploading(true); setActionError('')
    try { await billingApi.uploadPaymentEvidence(pendingUpload.paymentId, pendingUpload.selection); setPendingUpload(null); setRevision(value => value + 1); onChanged('Đã tải ảnh bằng chứng.') }
    catch (cause) { setActionError(`Chưa tải được ảnh: ${cause.message}. Bạn có thể thử lại.`) }
    finally { setUploading(false); setBusy(false) }
  }

  const pending = payments.filter(item => item.status === 'Pending')
  return <div className="bill-grid">
    {actionError && <p className="bill-error" role="alert">{actionError}</p>}
    {uploading && <p className="bill-payment-alert" role="status">Thanh toán đã được ghi nhận. Đang tải ảnh bằng chứng, vui lòng chờ…</p>}
    {pendingUpload && <div className="bill-payment-alert"><strong>Khoản tiền đã được lưu. Ảnh chưa được tải lên.</strong><div className="bill-actions"><button type="button" className="bill-button" disabled={busy} onClick={retryUpload}>Thử tải lại ảnh</button><button type="button" className="bill-button bill-button--secondary" disabled={busy} onClick={() => { setPendingUpload(null); setActionError('') }}>Bỏ ảnh, bổ sung sau</button></div></div>}
    <section className="bill-card"><h2>Yêu cầu chờ xác nhận ({pending.length})</h2><p>Đối chiếu số thực nhận trước khi duyệt. Khoản chờ duyệt chưa làm thay đổi số dư hóa đơn.</p>{loading ? <p role="status">Đang tải yêu cầu…</p> : loadError ? <p className="bill-error" role="alert">{loadError} <button className="bill-link" type="button" onClick={() => setRevision(value => value + 1)}>Thử lại</button></p> : pending.length ? <ul className="bill-payment-list">{pending.map(item => <ReviewPayment key={item.id} payment={item} amountDue={amountDue} busy={busy || Boolean(pendingUpload)} onReview={review} />)}</ul> : <p className="bill-empty">Không có yêu cầu chờ duyệt.</p>}</section>
    <section className="bill-card"><h2>Tự ghi khoản đã nhận</h2>{Number(amountDue) > 0 ? <><p>Dùng khi đã nhận tiền mà người thuê chưa khai. Khoản này được xác nhận ngay. <strong>Còn phải thu: {money(amountDue)}.</strong></p><form className="bill-form bill-payment-form" onSubmit={record}><label>Số tiền thực nhận *<input type="number" min="0.01" step="any" required value={receipt.amount} onChange={event => update('amount', event.target.value)} /></label><label>Phương thức<select value={receipt.method} onChange={event => update('method', event.target.value)}><option value="BankTransfer">Chuyển khoản</option><option value="Cash">Tiền mặt</option></select></label>{receipt.method === 'BankTransfer' && <label>Mã giao dịch (nếu có)<input maxLength={150} value={receipt.transactionCode} onChange={event => update('transactionCode', event.target.value)} /></label>}<label className="bill-field--wide">Ghi chú<textarea maxLength={500} value={receipt.note} onChange={event => update('note', event.target.value)} /></label><EvidenceSelector selection={selection} onChange={setSelection} disabled={busy || Boolean(pendingUpload)} /><button className="bill-button" type="submit" disabled={busy || Boolean(pendingUpload) || !receipt.amount}>{busy ? 'Đang ghi…' : 'Ghi đã nhận'}</button></form></> : <p className="bill-payment-alert">{Number(creditToRefund) > 0 ? <>Hóa đơn đang dư <strong>{money(creditToRefund)}</strong>. Hãy đối soát và ghi nhận khoản hoàn ở phần Hoàn tiền thu vượt.</> : 'Hóa đơn đã thu đủ. Không thể ghi thêm khoản đã nhận.'}</p>}</section>
    <section className="bill-card"><h2>Lịch sử thanh toán</h2>{loading ? <p role="status">Đang tải lịch sử…</p> : loadError ? <p className="bill-empty">Chưa tải được lịch sử. Thử lại ở phần yêu cầu chờ xác nhận.</p> : payments.length ? <ul className="bill-payment-list">{payments.map(item => <li key={item.id}><strong>{item.status === 'Success' ? `Thực nhận ${money(item.verifiedAmount)}` : `Đã khai ${money(item.claimedAmount)}`}</strong><span className="bill-payment-status" data-status={item.status}>{statusText[item.status] || item.status}</span><span>{item.submittedByName || (item.submittedByUserId && item.submittedByUserId !== item.verifiedByUserId ? 'Người thuê' : 'Chủ trọ')} · {item.method === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản'}</span><small>{item.verifiedAt ? "Xác nhận: " + formatVietnamDate(item.verifiedAt, true) : item.paidAt ? "Ngày khai: " + formatVietnamDate(item.paidAt) : ''}{item.transactionCode && ` · Mã ${item.transactionCode}`}</small>{item.reviewReason && <small>Lý do: {item.reviewReason}</small>}{item.status === 'Success' && <EvidenceManager paymentId={item.id} />}</li>)}</ul> : <p className="bill-empty">Chưa có khoản thanh toán.</p>}</section>
  </div>
}
