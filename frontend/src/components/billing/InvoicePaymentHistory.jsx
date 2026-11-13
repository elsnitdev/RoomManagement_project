import { useEffect, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'
import { formatVietnamDate } from './paymentDate'
import { EvidenceManager } from './PaymentEvidence'

const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const labels = { Pending: 'Chờ xác nhận', Success: 'Đã xác nhận', Failed: 'Từ chối', Cancelled: 'Đã hủy' }

export default function InvoicePaymentHistory({ invoiceId }) {
  const [payments, setPayments] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError('')
    billingApi.landlordPayments(invoiceId, controller.signal)
      .then(rows => { if (!controller.signal.aborted) setPayments(rows || []) })
      .catch(cause => { if (!controller.signal.aborted && cause.name !== 'AbortError') setError(cause.message || 'Không tải được lịch sử thanh toán.') })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [invoiceId, revision])

  return <section className="bill-card" aria-labelledby="bill-payment-history-title">
    <h2 id="bill-payment-history-title">Lịch sử thanh toán</h2>
    {loading ? <p role="status">Đang tải lịch sử thanh toán…</p> : error ? <p className="bill-error" role="alert">{error} <button type="button" className="bill-link" onClick={() => setRevision(value => value + 1)}>Thử lại</button></p> : payments.length ?
      <ul className="bill-payment-list">{payments.map(payment => <li key={payment.id}>
        <strong>{payment.status === 'Success' ? `Đã thu ${money(payment.verifiedAmount)}` : `Đã khai ${money(payment.claimedAmount)}`}</strong>
        <span className="bill-payment-status" data-status={payment.status}>{labels[payment.status] || payment.status}</span>
        <span>{payment.submittedByName || 'Chủ trọ / người thuê'} · {payment.method === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản'}</span>
        <small>{payment.verifiedAt ? `Xác nhận: ${formatVietnamDate(payment.verifiedAt, true)}` : payment.paidAt ? `Ngày khai: ${formatVietnamDate(payment.paidAt)}` : 'Chưa có thời gian'}{payment.transactionCode ? ` · Mã ${payment.transactionCode}` : ''}</small>
        {payment.reviewReason && <small>Lý do: {payment.reviewReason}</small>}
        {payment.status === 'Success' && <EvidenceManager paymentId={payment.id} initialOpen />}
      </li>)}</ul> : <p className="bill-empty">Chưa có khoản thanh toán.</p>}
  </section>
}
