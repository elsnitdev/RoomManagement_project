import { useEffect, useRef, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'
import { contractApi } from '../../services/api/contractApi'
import { EvidenceSelector } from '../billing/PaymentEvidence'

const today = () => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())
const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const periodOf = date => ({ year: Number(date.slice(0, 4)), month: Number(date.slice(5, 7)) })
const dayBefore = date => new Date(Date.parse(`${date}T00:00:00Z`) - 86_400_000).toISOString().slice(0, 10)
const editable = ['Service', 'Damage', 'Other', 'Discount']
const emptyInvoices = []
const signedMoney = value => `${Number(value) >= 0 ? '+' : '−'}${money(Math.abs(Number(value)))}`
const pendingOneOffKey = contractId => `early-exit-one-off:${contractId}`
async function idempotencyKeyForOneOff(contractId, body, fallback) {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(JSON.stringify(body)))
  const fingerprint = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('')
  try {
    const prior = JSON.parse(localStorage.getItem(pendingOneOffKey(contractId)) || 'null')
    if (prior?.fingerprint === fingerprint && prior?.key) return prior.key
    const key = crypto.randomUUID()
    localStorage.setItem(pendingOneOffKey(contractId), JSON.stringify({ fingerprint, key }))
    return key
  } catch { return fallback }
}
function clearPendingOneOffKey(contractId) {
  try { localStorage.removeItem(pendingOneOffKey(contractId)) } catch { /* Storage may be disabled. */ }
}
function OldUnpaidInvoice({ invoice, detail }) {
  const adjustments = detail?.adjustments || []
  return <article className="ct-exit-invoice-card ct-exit-old-invoice">
    <div className="ct-exit-old-invoice__heading"><strong>{invoice.code} · {invoice.type === 'Monthly' ? `Kỳ ${invoice.billingMonth}/${invoice.billingYear}` : invoice.type === 'Deposit' ? 'Tiền cọc' : 'Hóa đơn riêng'}</strong><a href={`#/landlord/billing/${invoice.id}`} target="_blank" rel="noopener noreferrer">Mở chi tiết ↗</a></div>
    <p>Gốc: {money(invoice.baseTotal)} · Điều chỉnh: <strong>{signedMoney(Number(invoice.effectiveTotal) - Number(invoice.baseTotal))}</strong></p>
    {adjustments.length > 0 ? <ul className="ct-exit-adjustments">{adjustments.map(item => <li key={item.id}><span>{item.reason}{item.voidedAt ? ' · Đã hủy' : ''}</span><strong>{signedMoney((item.type === 'AdditionalCharge' ? 1 : -1) * Number(item.amount))}</strong></li>)}</ul> : <p>{detail ? 'Chưa có phụ thu hoặc giảm trừ.' : 'Đang tải lịch sử điều chỉnh…'}</p>}
    <p>Thành tiền: <strong>{money(invoice.effectiveTotal)}</strong> · Đã thu/cấn trừ: <strong>{money(Number(invoice.totalPaid || 0) + Number(invoice.totalDepositApplied || 0) - Number(invoice.totalRefunded || 0))}</strong></p>
    <p className="ct-exit-old-invoice__due">Còn phải thu: {money(invoice.amountDue)}</p>
  </article>
}
export function SettlementRefundEvidencePanel({ propertyId, contractId }) {
  const [visible, setVisible] = useState(false)
  const [eligible, setEligible] = useState(false)
  const [evidence, setEvidence] = useState(null)
  const [selection, setSelection] = useState(null)
  const [imageUrl, setImageUrl] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    contractApi.settlementRefundEvidence(propertyId, contractId, controller.signal)
      .then(result => { if (!controller.signal.aborted) { setVisible(true); setEligible(true); setEvidence(result); setError('') } })
      .catch(cause => {
        if (controller.signal.aborted || cause.name === 'AbortError') return
        if (cause.status === 409 && cause.details?.code === 'SETTLEMENT_NO_REFUND') { setVisible(false); setEligible(false) }
        else { setVisible(true); setEligible(false); setError(cause.message || 'Không tải được chứng từ hoàn cọc.') }
      })
    return () => controller.abort()
  }, [propertyId, contractId, revision])
  useEffect(() => {
    if (!evidence?.id) { setImageUrl(''); return }
    const controller = new AbortController()
    let objectUrl = ''
    contractApi.settlementRefundEvidenceContent(propertyId, contractId, evidence.id, controller.signal)
      .then(blob => { if (!controller.signal.aborted) { objectUrl = URL.createObjectURL(blob); setImageUrl(objectUrl) } })
      .catch(cause => { if (!controller.signal.aborted && cause.name !== 'AbortError') setError(cause.message || 'Không mở được ảnh hoàn cọc.') })
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl) }
  }, [propertyId, contractId, evidence?.id])
  async function upload(event) {
    event.preventDefault()
    if (!selection || evidence) return
    setBusy(true); setError('')
    try {
      const uploaded = await contractApi.uploadSettlementRefundEvidence(propertyId, contractId, selection)
      setEvidence(uploaded); setSelection(null); setRevision(value => value + 1)
    } catch (cause) { setError(`Chưa tải được ảnh hoàn cọc: ${cause.message}. Có thể thử lại với cùng ảnh.`) }
    finally { setBusy(false) }
  }
  if (!visible) return null
  return <section className="ct-card ct-refund-evidence-panel"><h2>Chứng từ hoàn cọc</h2>
    {error && <p className="contract-message contract-message--error" role="alert">{error}</p>}
    {eligible && (evidence ? <div className="ct-refund-evidence-panel__saved"><p>Đã lưu: <strong>{evidence.fileName}</strong></p>{imageUrl ? <a href={imageUrl} target="_blank" rel="noopener noreferrer"><img src={imageUrl} alt={`Chứng từ hoàn cọc ${evidence.fileName}`} />Mở ảnh chứng từ ↗</a> : <p role="status">Đang tải ảnh chứng từ…</p>}</div> : <form onSubmit={upload}><p>Hợp đồng đã tất toán và có khoản hoàn cọc. Tải ảnh chứng từ tại đây nếu lần tải trước chưa thành công.</p><EvidenceSelector selection={selection} onChange={setSelection} disabled={busy} /><button type="submit" className="contract-button contract-button--secondary" disabled={busy || !selection}>{busy ? 'Đang tải ảnh…' : 'Lưu ảnh hoàn cọc'}</button></form>)}
    <button type="button" className="contract-button contract-button--ghost" onClick={() => setRevision(value => value + 1)} disabled={busy}>Làm mới chứng từ</button>
  </section>
}
export default function EarlyExitFlow({ propertyId, contract, navigate, onChanged }) {
  const [open, setOpen] = useState(false)
  const [openCycle, setOpenCycle] = useState(0)
  const [step, setStep] = useState(1)
  const [handoverOn, setHandoverOn] = useState(contract.handoverOn || today())
  const [readings, setReadings] = useState([])
  const [rateSnapshot, setRateSnapshot] = useState(null)
  const [rateError, setRateError] = useState('')
  const [rateInputs, setRateInputs] = useState({ Electricity: { price: '', unit: '' }, Water: { price: '', unit: '' } })
  const [readingType, setReadingType] = useState('Electricity')
  const [previousValue, setPreviousValue] = useState('')
  const [currentValue, setCurrentValue] = useState('')
  const [extraType, setExtraType] = useState('Other')
  const [extraMode, setExtraMode] = useState('monthly')
  const [extraDescription, setExtraDescription] = useState('')
  const [extraAmount, setExtraAmount] = useState('')
  const [oneOffDueDate, setOneOffDueDate] = useState(today)
  const [oneOffCreated, setOneOffCreated] = useState(false)
  const [createdOneOffId, setCreatedOneOffId] = useState(null)
  const [oneOffAttempted, setOneOffAttempted] = useState(false)
  const [oneOffKey, setOneOffKey] = useState(() => crypto.randomUUID())
  const [oneOffDetails, setOneOffDetails] = useState({})
  const [oldInvoiceDetails, setOldInvoiceDetails] = useState({})
  const [paymentSelection, setPaymentSelection] = useState(null)
  const [pendingPaymentUpload, setPendingPaymentUpload] = useState(null)
  const [refundSelection, setRefundSelection] = useState(null)
  const [pendingRefundUpload, setPendingRefundUpload] = useState(null)
  const [settlementCompleted, setSettlementCompleted] = useState(false)
  const [paymentInvoiceId, setPaymentInvoiceId] = useState('')
  const [overviewSnapshot, setOverviewSnapshot] = useState(null)
  const [detail, setDetail] = useState(null)
  const [preview, setPreview] = useState(null)
  const [previewLoading, setPreviewLoading] = useState(false)
  const [previewError, setPreviewError] = useState('')
  const [offset, setOffset] = useState('0')
  const [offsetKey, setOffsetKey] = useState(() => crypto.randomUUID())
  const [refund, setRefund] = useState('')
  const [note, setNote] = useState('')
  const [payment, setPayment] = useState('')
  const [method, setMethod] = useState('BankTransfer')
  const [transactionCode, setTransactionCode] = useState('')
  const [paymentKey, setPaymentKey] = useState(() => crypto.randomUUID())
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [revision, setRevision] = useState(0)
  const closeButton = useRef(null)
  const dataKey = `${propertyId}:${openCycle}:${revision}`
  const overview = overviewSnapshot?.key === dataKey ? overviewSnapshot.value : null
  const invoices = overview?.invoices || emptyInvoices
  const pendingMoveOut = (overview?.contractStatus || contract.status) === 'PendingMoveOut'
  const pendingSettlement = (overview?.contractStatus || contract.status) === 'PendingSettlement'
  const fallbackPeriod = periodOf((contract.status === 'PendingSettlement' ? contract.handoverOn : handoverOn) || today())
  const period = overview?.periodYear && overview?.periodMonth ? { year: overview.periodYear, month: overview.periodMonth } : fallbackPeriod
  const periodStart = `${period.year}-${String(period.month).padStart(2, '0')}-01`
  const allowed = action => Boolean(overview?.allowedActions?.includes(action))
  const inCollect = overview?.phase === 'Collect' || overview?.phase === 'Complete'
  const missingMonthlyPeriods = overview?.missingMonthlyPeriods || []
  const outstanding = Number(overview?.outstandingAmount || 0)
  const rates = rateSnapshot?.key === dataKey ? rateSnapshot.values : null
  const rateFor = type => rates?.find(row => row.type === type && row.effectiveFrom <= periodStart && (!row.effectiveTo || row.effectiveTo >= periodStart))
  const rateEndFor = type => {
    const nextStart = rates?.filter(row => row.type === type && row.effectiveFrom > periodStart).map(row => row.effectiveFrom).sort()[0]
    return nextStart ? dayBefore(nextStart) : null
  }
  const missingRates = rates ? ['Electricity', 'Water'].filter(type => !rateFor(type)) : []
  const monthly = invoices.find(row => row.id === overview?.finalInvoiceId) || invoices.find(row => row.contractId === contract.id && row.type === 'Monthly' && row.billingYear === period.year && row.billingMonth === period.month && row.status !== 'Cancelled')
  const oldUnpaid = invoices.filter(row => row.status === 'Issued' && Number(row.amountDue) > 0 && row.id !== monthly?.id && row.id !== createdOneOffId)
  const oneOffs = invoices.filter(row => ['Damage', 'Other'].includes(row.type) && (row.status === 'Draft' || row.id === createdOneOffId))
  const readingFor = type => readings.find(row => row.type === type && row.year === period.year && row.month === period.month)
  const held = Number(overview?.depositHeld ?? contract.depositHeld ?? 0)
  const currentDetail = detail?.summary?.id === monthly?.id ? detail : null
  const currentPreview = preview?.year === period.year && preview?.month === period.month ? preview : null
  const baseTotal = Number(currentDetail?.summary?.baseTotal ?? currentPreview?.totalAmount ?? 0)
  const extra = Number(extraAmount || 0)
  const hasExtraInput = Boolean(extraDescription.trim() || extraAmount)
  const wantsExtra = extraMode === 'monthly' && hasExtraInput
  const extraValid = !hasExtraInput || (extraDescription.trim().length > 0 && /^\d+(?:\.\d{1,2})?$/.test(extraAmount) && Number.isFinite(extra) && extra > 0)
  const matchingOneOff = oneOffs.find(row => row.type === (extraType === 'Damage' ? 'Damage' : 'Other') && row.dueDate === oneOffDueDate && Number(row.baseTotal) === extra && oneOffDetails[row.id]?.items?.some(item => item.description === extraDescription.trim() && Number(item.amount) === extra))
  const oneOffReady = extraMode !== 'separate' || (!hasExtraInput && !oneOffCreated) || (oneOffCreated && invoices.some(row => row.id === createdOneOffId)) || Boolean(matchingOneOff)
  const extraSaved = currentDetail?.items?.some(row => row.type === extraType && row.description === extraDescription.trim() && Number(row.amount) === extra)
  const projectedTotal = monthly?.status === 'Issued' ? Number(monthly.effectiveTotal ?? monthly.baseTotal ?? 0) : baseTotal + (wantsExtra && !extraSaved && extraValid ? extra : 0)
  const maxOffset = Math.max(0, Math.min(held, monthly?.status === 'Issued' ? Number(monthly.amountDue || 0) : projectedTotal))
  const paymentInvoice = invoices.find(row => row.id === paymentInvoiceId && row.status === 'Issued' && Number(row.amountDue) > 0) || invoices.find(row => row.status === 'Issued' && Number(row.amountDue) > 0)
  const settlementTotal = projectedTotal + [...oneOffs, ...oldUnpaid].reduce((sum, row) => sum + Number(row.effectiveTotal ?? row.baseTotal ?? 0), 0)
  const projectedDue = (monthly?.status === 'Issued' ? Number(monthly.amountDue || 0) : projectedTotal) + [...oneOffs, ...oldUnpaid].reduce((sum, row) => sum + Number(row.amountDue || 0), 0)
  const unsettledInvoices = invoices.filter(row => row.status === 'Draft' || (row.status === 'Issued' && Number(row.amountDue) > 0))

  useEffect(() => { setHandoverOn(contract.handoverOn || today()) }, [contract.id, contract.handoverOn])
  useEffect(() => {
    if (readingType === 'Electricity' && readingFor('Electricity') && !readingFor('Water')) setReadingType('Water')
    if (readingType === 'Water' && readingFor('Water') && !readingFor('Electricity')) setReadingType('Electricity')
  }, [readings, period.year, period.month, readingType])
  useEffect(() => {
    if (!open) return
    const original = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    closeButton.current?.focus()
    const closeOnEscape = event => { if (event.key === 'Escape' && !busy) setOpen(false) }
    window.addEventListener('keydown', closeOnEscape)
    return () => { document.body.style.overflow = original; window.removeEventListener('keydown', closeOnEscape) }
  }, [open, busy])
  useEffect(() => {
    if (!open) return
    const controller = new AbortController()
    setLoading(true)
    setRateError('')
    Promise.allSettled([
      contractApi.readings(propertyId, contract.id, controller.signal),
      contractApi.settlementOverview(propertyId, contract.id, controller.signal),
      billingApi.rates(propertyId, controller.signal),
    ]).then(([readingResult, overviewResult, rateResult]) => {
      if (controller.signal.aborted) return
      if (readingResult.status === 'fulfilled') setReadings(readingResult.value || [])
      else setError(readingResult.reason?.message || 'Không tải được chỉ số điện nước.')
      if (overviewResult.status === 'fulfilled') {
        setOverviewSnapshot({ key: dataKey, value: overviewResult.value })
        setStep(current => overviewResult.value.phase === 'Handover' ? 1 : overviewResult.value.phase === 'PrepareInvoice' ? (current === 0 ? 2 : current) : 3)
        if (overviewResult.value.phase === 'Collect' || overviewResult.value.phase === 'Complete' || overviewResult.value.phase === 'Closed') {
          setExtraDescription(''); setExtraAmount(''); setOneOffCreated(false); setCreatedOneOffId(null); setOneOffAttempted(false)
        }
      } else {
        const cause = overviewResult.reason
        setError(cause?.status === 404 && (!cause.details?.code || cause.details.code === 'NOT_FOUND')
          ? 'API trạng thái tất toán chưa có trên máy chủ đang chạy. Hãy khởi động lại backend với mã nguồn mới rồi thử lại.'
          : cause?.message || 'Không tải được trạng thái tất toán.')
      }
      if (rateResult.status === 'fulfilled') setRateSnapshot({ key: dataKey, values: rateResult.value || [] })
      else setRateError(rateResult.reason?.message || 'Không tải được đơn giá điện nước.')
    })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [open, openCycle, propertyId, contract.id, revision])
  useEffect(() => {
    if (!open || !monthly?.id) { setDetail(null); return }
    const controller = new AbortController()
    billingApi.invoice(monthly.id, controller.signal).then(value => { if (!controller.signal.aborted) setDetail(value) })
      .catch(cause => { if (!controller.signal.aborted && cause.name !== 'AbortError') setError(cause.message) })
    return () => controller.abort()
  }, [open, monthly?.id, revision])
  useEffect(() => {
    setPreview(null)
    setPreviewError('')
    if (!open || step < 2 || pendingMoveOut || monthly) {
      setPreviewLoading(false)
      return
    }
    if (!readingFor('Electricity') || !readingFor('Water')) {
      setPreviewError('Kỳ này còn thiếu chỉ số điện hoặc nước. Quay lại bước 1 để nhập chỉ số.')
      setPreviewLoading(false)
      return
    }
    if (!rates) {
      setPreviewError(rateError ? `Không tải được đơn giá điện nước: ${rateError}` : '')
      setPreviewLoading(!rateError)
      return
    }
    if (missingRates.length) {
      setPreviewError(`Thiếu đơn giá ${missingRates.map(type => type === 'Electricity' ? 'điện' : 'nước').join(' và ')} có hiệu lực từ ngày ${periodStart}. Hãy cấu hình đơn giá cho kỳ này.`)
      setPreviewLoading(false)
      return
    }
    let active = true
    setPreviewLoading(true)
    billingApi.preview(propertyId, { contractId: contract.id, ...period })
      .then(value => { if (active) setPreview(value) })
      .catch(cause => { if (active) setPreviewError(cause.message || 'Không thể tính hóa đơn cuối. Kiểm tra chỉ số và đơn giá của kỳ này.') })
      .finally(() => { if (active) setPreviewLoading(false) })
    return () => { active = false }
  }, [open, step, pendingMoveOut, monthly?.id, propertyId, contract.id, period.year, period.month, readings, rates, rateError])
  useEffect(() => {
    if (!open || !oneOffs.length) { setOneOffDetails(current => Object.keys(current).length ? {} : current); return }
    const controller = new AbortController()
    Promise.all(oneOffs.map(row => billingApi.invoice(row.id, controller.signal)))
      .then(details => { if (!controller.signal.aborted) setOneOffDetails(Object.fromEntries(details.map((value, index) => [oneOffs[index].id, value]))) })
      .catch(cause => { if (!controller.signal.aborted && cause.name !== 'AbortError') setError(cause.message) })
    return () => controller.abort()
  }, [open, invoices, createdOneOffId, revision])
  useEffect(() => {
    if (!open || !oldUnpaid.length) { setOldInvoiceDetails(current => Object.keys(current).length ? {} : current); return }
    const controller = new AbortController()
    Promise.all(oldUnpaid.map(row => billingApi.invoice(row.id, controller.signal)))
      .then(details => { if (!controller.signal.aborted) setOldInvoiceDetails(Object.fromEntries(details.map((value, index) => [oldUnpaid[index].id, value]))) })
      .catch(cause => { if (!controller.signal.aborted && cause.name !== 'AbortError') setError(cause.message) })
    return () => controller.abort()
  }, [open, invoices, monthly?.id, createdOneOffId, revision])

  async function run(request, message, refreshContract = false) {
    setBusy(true); setError(''); setSuccess('')
    try {
      const result = await request()
      setSuccess(message); setRevision(value => value + 1)
      if (refreshContract) await onChanged?.()
      return result
    } catch (cause) { setError(cause.message || 'Không thể hoàn tất thao tác.'); setRevision(value => value + 1); return null }
    finally { setBusy(false) }
  }
  function resetOneOffIntent() {
    setOneOffKey(crypto.randomUUID())
    setOneOffAttempted(false)
    setOneOffCreated(false)
  }
  async function saveReading(event) {
    event.preventDefault()
    const result = await run(() => contractApi.addReading(propertyId, contract.id, { type: readingType, ...period, previousValue: Number(previousValue), currentValue: Number(currentValue) }), 'Đã lưu chỉ số.')
    if (result) { setPreviousValue(''); setCurrentValue(''); setReadingType(readingType === 'Electricity' ? 'Water' : 'Electricity') }
  }
  async function saveMissingRate(event, type) {
    event.preventDefault()
    if (rateFor(type) || !rates) return
    const { price, unit } = rateInputs[type]
    if (!/^\d+(?:\.\d{1,2})?$/.test(price) || !Number.isFinite(Number(price)) || Number(price) < 0 || !unit.trim() || unit.trim().length > 50) {
      setError('Nhập đơn giá không âm, tối đa 2 chữ số thập phân, và đơn vị không quá 50 ký tự.')
      return
    }
    const saved = await run(() => billingApi.createRate(propertyId, { type, unitPrice: Number(price), unit: unit.trim(), effectiveFrom: periodStart, effectiveTo: rateEndFor(type) }), 'Đã lưu đơn giá cho kỳ này.')
    if (saved) setRateInputs(current => ({ ...current, [type]: { price: '', unit: '' } }))
  }
  async function handover() {
    if (!readingFor('Electricity') || !readingFor('Water')) { setError('Cần nhập đủ chỉ số điện và nước.'); return }
    const result = await run(() => contractApi.handover(propertyId, contract.id, handoverOn), 'Đã xác nhận bàn giao.', true)
    if (result) setStep(2)
  }
  async function saveExtra(invoiceId, items) {
    const original = items.filter(row => editable.includes(row.type)).map(row => ({ id: row.id, type: row.type, description: row.description, quantity: row.quantity, unitPrice: row.unitPrice }))
    const saved = await run(() => billingApi.saveDraftItems(invoiceId, { items: [...original, { type: extraType, description: extraDescription.trim(), quantity: 1, unitPrice: extra }] }), 'Đã thêm khoản phát sinh vào hóa đơn.')
    if (saved) { setExtraDescription(''); setExtraAmount('') }
  }
  async function createDraft() {
    if (!allowed('CreateMonthlyInvoice')) { setError('Hóa đơn cuối đã tồn tại hoặc bước lập hóa đơn đã khép lại.'); setRevision(value => value + 1); return }
    if (!extraValid) { setError('Khoản phát sinh chưa hợp lệ.'); return }
    const created = await run(() => billingApi.createMonthly(propertyId, { contractId: contract.id, ...period }), 'Đã tạo hóa đơn cuối dạng nháp.')
    if (!created || !wantsExtra) return
    const newDetail = await run(() => billingApi.invoice(created.id), 'Đang tải hóa đơn nháp.')
    if (newDetail) await saveExtra(created.id, newDetail.items)
  }
  async function createSeparateInvoice() {
    if (!allowed('CreateSettlementOneOff')) { setError('Giai đoạn lập hóa đơn bàn giao đã kết thúc. Hãy xem hóa đơn hiện có hoặc thêm khoản phát sinh qua trang hóa đơn.'); setRevision(value => value + 1); return }
    if (oneOffAttempted || matchingOneOff || !hasExtraInput || !extraValid || !oneOffDueDate) return
    setOneOffAttempted(true)
    setBusy(true); setError(''); setSuccess('')
    try {
      const body = {
        contractId: contract.id,
        type: extraType === 'Damage' ? 'Damage' : 'Other',
        dueDate: oneOffDueDate,
        note: 'Khoản phát sinh khi bàn giao',
        items: [{ type: extraType === 'Damage' ? 'Damage' : 'Other', description: extraDescription.trim(), quantity: 1, unitPrice: extra }],
        source: 'EarlyExitSettlement',
      }
      const idempotencyKey = await idempotencyKeyForOneOff(contract.id, body, oneOffKey)
      const created = await billingApi.createOneOff(propertyId, { ...body, idempotencyKey })
      clearPendingOneOffKey(contract.id)
      setOneOffKey(crypto.randomUUID())
      setCreatedOneOffId(created.id); setOneOffCreated(true); setExtraDescription(''); setExtraAmount('')
      setSuccess('Đã tạo hóa đơn riêng dạng nháp.')
      await onChanged?.()
    } catch (cause) {
      if (cause.status >= 400 && cause.status < 500 && cause.status !== 408) setOneOffAttempted(false)
      setError(cause.message || 'Không thể tạo hóa đơn riêng. Kiểm tra danh sách hóa đơn trước khi thử lại.')
    } finally { setRevision(value => value + 1); setBusy(false) }
  }
  async function issueOneOff(invoiceId) {
    await run(() => billingApi.issue(invoiceId), 'Đã phát hành hóa đơn riêng.', true)
  }
  async function issueAndOffset() {
    if (!monthly) return
    if (monthly.status === 'Draft') {
      const issued = await run(() => billingApi.issue(monthly.id), 'Đã phát hành hóa đơn cuối.', true)
      if (!issued) return
    }
    if (Number(offset) > 0) {
      const applied = await run(() => contractApi.applyDeposit(propertyId, contract.id, { invoiceId: monthly.id, amount: Number(offset), reason: 'Cấn trừ cọc vào hóa đơn cuối', idempotencyKey: offsetKey }), 'Đã cấn trừ tiền cọc.', true)
      if (applied) { setOffset('0'); setOffsetKey(crypto.randomUUID()) }
    }
  }
  async function recordPayment(event) {
    event.preventDefault()
    if (!allowed('RecordPayment')) { setError('Trạng thái công nợ đã thay đổi. Hãy tải lại dữ liệu.'); setRevision(value => value + 1); return }
    if (!paymentInvoice) return
    if (!Number.isFinite(Number(payment)) || Number(payment) <= 0) { setError('Nhập số tiền đã nhận hợp lệ.'); return }
    if (Number(payment) > Number(paymentInvoice.amountDue) && !window.confirm('Số tiền nhận vượt số còn phải thu. Phần dư cần được đối soát và hoàn tiền. Tiếp tục?')) return
    const result = await run(() => billingApi.recordReceived(paymentInvoice.id, { amount: Number(payment), method, transactionCode: method === 'BankTransfer' ? transactionCode.trim() || null : null, idempotencyKey: paymentKey }), 'Đã ghi nhận khoản thanh toán.', true)
    if (!result) return
    setPayment(''); setTransactionCode(''); setPaymentKey(crypto.randomUUID())
    if (paymentSelection) await uploadManualPaymentEvidence(result.id, paymentSelection)
  }
  async function uploadManualPaymentEvidence(paymentId, selection) {
    setBusy(true); setError('')
    try {
      await billingApi.uploadPaymentEvidence(paymentId, selection)
      setPaymentSelection(null); setPendingPaymentUpload(null)
      setSuccess('Đã ghi nhận thanh toán và lưu ảnh chứng từ.')
    } catch (cause) {
      setPendingPaymentUpload({ paymentId, selection })
      setError(`Đã ghi nhận thanh toán nhưng chưa tải được ảnh: ${cause.message}. Hãy thử tải lại, không ghi nhận thanh toán lần nữa.`)
    } finally { setBusy(false) }
  }
  async function finish(event) {
    event.preventDefault()
    if (settlementCompleted) return
    if (!allowed('CompleteSettlement')) { setError('Còn hóa đơn chưa xử lý hoặc dữ liệu đã đổi. Hãy tải lại trạng thái tất toán.'); setRevision(value => value + 1); return }
    const disposition = held > 0 ? 'Refunded' : invoices.some(row => Number(row.totalDepositApplied) > 0) ? 'Offset' : 'NoDeposit'
    setBusy(true); setError(''); setSuccess('')
    try {
      await contractApi.completeSettlement(propertyId, contract.id, { depositDisposition: disposition, refundAmount: held > 0 ? Number(refund) : 0, note: note.trim() || null })
      setSettlementCompleted(true)
      if (held > 0 && refundSelection) {
        try {
          await contractApi.uploadSettlementRefundEvidence(propertyId, contract.id, refundSelection)
          setRefundSelection(null); setPendingRefundUpload(null)
          setSuccess('Đã hoàn tất hợp đồng và lưu ảnh hoàn cọc.')
        } catch (cause) {
          setPendingRefundUpload(refundSelection)
          setError(`Hợp đồng đã tất toán nhưng ảnh hoàn cọc chưa tải được: ${cause.message}. Hãy thử tải lại ảnh; không tất toán lần nữa.`)
          return
        }
      } else setSuccess('Đã hoàn tất hợp đồng.')
      await onChanged?.()
    } catch (cause) { setError(cause.message || 'Không thể hoàn tất hợp đồng.') }
    finally { setBusy(false) }
  }
  async function retryRefundEvidence() {
    if (!pendingRefundUpload) return
    setBusy(true); setError('')
    try {
      await contractApi.uploadSettlementRefundEvidence(propertyId, contract.id, pendingRefundUpload)
      setPendingRefundUpload(null); setRefundSelection(null)
      setSuccess('Đã lưu ảnh hoàn cọc.')
      await onChanged?.()
    } catch (cause) { setError(`Chưa tải được ảnh hoàn cọc: ${cause.message}. Hãy thử lại.`) }
    finally { setBusy(false) }
  }

  return <section className="ct-card ct-exit-launch">
    <div><h2>Quy trình bàn giao và tất toán</h2><p>{contract.status === 'PendingSettlement' ? 'Tiếp tục hồ sơ đang xử lý và đối soát các khoản còn phải thu.' : 'Nhập dữ liệu, kiểm tra hóa đơn và đối soát tiền cọc theo từng bước.'}</p></div>
    <button type="button" className="contract-button contract-button--primary" onClick={() => { setStep(0); setOpenCycle(value => value + 1); setOpen(true) }}>{contract.status === 'PendingSettlement' ? 'Tiếp tục tất toán' : 'Mở quy trình'}</button>
    {open && <div className="ct-exit-overlay" onMouseDown={event => { if (event.target === event.currentTarget && !busy) setOpen(false) }}>
      <div className="ct-exit-dialog" role="dialog" aria-modal="true" aria-labelledby="ct-exit-title">
        <header><div><span>HỢP ĐỒNG {contract.contractCode}</span><h2 id="ct-exit-title">Bàn giao và tất toán</h2></div><button ref={closeButton} type="button" aria-label="Đóng quy trình" onClick={() => setOpen(false)}>×</button></header>
        <ol className="ct-exit-steps"><li className={step === 1 ? 'is-current' : step > 1 ? 'is-done' : ''}>1. Dữ liệu bàn giao</li><li className={step === 2 ? 'is-current' : step > 2 ? 'is-done' : ''}>2. Hóa đơn và cọc</li><li className={step === 3 ? 'is-current' : ''}>3. Đối soát</li></ol>
        <div className="ct-exit-dialog__body">
          {loading && <p role="status">Đang tải dữ liệu…</p>}
          {error && <p className="contract-message contract-message--error" role="alert">{error}</p>}
          {success && <p className="contract-message" role="status">{success}</p>}
          {!overview && !loading && <button type="button" className="contract-button contract-button--secondary" onClick={() => setRevision(value => value + 1)}>Tải lại trạng thái tất toán</button>}
          {overview && inCollect && <div className="ct-exit-resume" role="status">
            <strong>{overview.phase === 'Complete' ? 'Sẵn sàng hoàn tất hợp đồng' : 'Đang xử lý hồ sơ tất toán hiện có'}</strong>
            <p>Hóa đơn cuối {monthly?.code || 'đang cập nhật'} đã được phát hành.{outstanding > 0 ? ` Còn phải thu ${money(outstanding)}.` : ' Công nợ đã được thanh toán.'} Không lập lại hóa đơn bàn giao.</p>
            {missingMonthlyPeriods.length > 0 && <p>Còn kỳ chưa phát hành hóa đơn: <strong>{missingMonthlyPeriods.join(', ')}</strong>. <a href={`#/landlord/billing/rooms/${propertyId}/${contract.roomId}`} target="_blank" rel="noopener noreferrer">Mở trang Hóa đơn ↗</a>, chọn các kỳ này và phát hành trước khi hoàn tất hợp đồng.</p>}
          </div>}
          {overview && step === 1 && <div className="ct-exit-stage">
            <h3>Thông tin bàn giao</h3>
            <label>Ngày bàn giao<input type="date" min={contract.startDate} max={contract.endDate && contract.endDate < today() ? contract.endDate : today()} value={handoverOn} onChange={event => setHandoverOn(event.target.value)} /></label>
            <p>Kỳ hóa đơn cuối: {period.month}/{period.year}</p>
            <div className="ct-exit-readings">{['Electricity', 'Water'].map(type => { const row = readingFor(type); return <p key={type}><strong>{type === 'Electricity' ? 'Điện' : 'Nước'}:</strong> {row ? [row.previousValue, ' → ', row.currentValue] : 'Chưa nhập'}</p> })}</div>
            <div className="ct-exit-rate-check" aria-live="polite"><strong>Đơn giá áp dụng ngày {periodStart}</strong>
              {rates ? ['Electricity', 'Water'].map(type => { const rate = rateFor(type); return <p key={type}><strong>{type === 'Electricity' ? 'Điện' : 'Nước'}:</strong> {rate ? `${money(rate.unitPrice)} / ${rate.unit} · hiệu lực ${rate.effectiveFrom} đến ${rate.effectiveTo || 'về sau'}` : 'Chưa có đơn giá có hiệu lực cho kỳ này'}</p> }) : <p>{rateError ? `Không tải được đơn giá: ${rateError}` : 'Đang kiểm tra đơn giá…'}</p>}
              {missingRates.length > 0 && <p className="ct-exit-rate-warning" role="alert">Thiếu đơn giá {missingRates.map(type => type === 'Electricity' ? 'điện' : 'nước').join(' và ')} có hiệu lực ngày {periodStart}. Cần cấu hình đơn giá trước khi tính hóa đơn.</p>}
              {missingRates.map(type => <form className="ct-exit-rate-form" key={type} onSubmit={event => saveMissingRate(event, type)}>
                <strong>Thêm giá {type === 'Electricity' ? 'điện' : 'nước'} cho kỳ này</strong>
                <p>Hiệu lực từ {periodStart} đến {rateEndFor(type) || 'khi có giá mới'}.</p>
                <label>Đơn giá<input type="number" min="0" step="0.01" required value={rateInputs[type].price} onChange={event => setRateInputs(current => ({ ...current, [type]: { ...current[type], price: event.target.value } }))} /></label>
                <label>Đơn vị<input maxLength="50" required placeholder={type === 'Electricity' ? 'Ví dụ: kWh' : 'Ví dụ: m³'} value={rateInputs[type].unit} onChange={event => setRateInputs(current => ({ ...current, [type]: { ...current[type], unit: event.target.value } }))} /></label>
                <button type="submit" className="contract-button contract-button--secondary" disabled={busy}>Lưu giá {type === 'Electricity' ? 'điện' : 'nước'}</button>
              </form>)}
              {(missingRates.length > 0 || rateError) && <div className="ct-exit-actions"><button type="button" className="contract-button contract-button--secondary" onClick={() => setRevision(value => value + 1)}>Kiểm tra lại đơn giá</button>{navigate && <button type="button" className="contract-button contract-button--ghost" onClick={() => { setOpen(false); navigate(`landlord/billing/rooms/${propertyId}/${contract.roomId}`) }}>Đến trang hóa đơn để cấu hình giá</button>}</div>}
              {(missingRates.length > 0 || rateError) && navigate && <p>Trong trang hóa đơn, mở tab “Giá, chỉ số và dịch vụ” để thêm giá của kỳ này.</p>}
            </div>
            {(!readingFor('Electricity') || !readingFor('Water')) && <form className="ct-exit-form" onSubmit={saveReading}>
              <label>Loại chỉ số<select value={readingType} onChange={event => setReadingType(event.target.value)}><option value="Electricity" disabled={Boolean(readingFor('Electricity'))}>Điện</option><option value="Water" disabled={Boolean(readingFor('Water'))}>Nước</option></select></label>
              <label>Chỉ số đầu<input type="number" min="0" step="0.01" required value={previousValue} onChange={event => setPreviousValue(event.target.value)} /></label>
              <label>Chỉ số cuối<input type="number" min={previousValue || '0'} step="0.01" required value={currentValue} onChange={event => setCurrentValue(event.target.value)} /></label>
              <button className="contract-button contract-button--secondary" disabled={busy || Boolean(readingFor(readingType))}>Lưu chỉ số</button>
            </form>}
            {!inCollect && <fieldset className="ct-exit-extra"><legend>Khoản thu thêm (nếu có)</legend>
              <label>Đưa vào<select value={extraMode} onChange={event => { setExtraMode(event.target.value); resetOneOffIntent(); if (event.target.value === 'separate' && extraType === 'Service') setExtraType('Other') }}><option value="monthly">Hóa đơn cuối tháng</option><option value="separate">Hóa đơn riêng</option></select></label>
              <label>Loại<select value={extraType} onChange={event => { setExtraType(event.target.value); resetOneOffIntent() }}><option value="Other">Khoản thu khác</option><option value="Damage">Phí sửa chữa</option>{extraMode === 'monthly' && <option value="Service">Dịch vụ</option>}</select></label>
              <label>Mô tả<input maxLength="255" value={extraDescription} onChange={event => { setExtraDescription(event.target.value); resetOneOffIntent() }} /></label>
              <label>Số tiền<input type="number" min="0.01" step="0.01" value={extraAmount} onChange={event => { setExtraAmount(event.target.value); resetOneOffIntent() }} /></label>
              {extraMode === 'separate' && <label>Hạn thanh toán<input type="date" value={oneOffDueDate} onChange={event => { setOneOffDueDate(event.target.value); resetOneOffIntent() }} /></label>}
              {extraMode === 'separate' && <p>Hóa đơn riêng được tạo nháp ở bước 2 và phát hành sau khi đối soát.</p>}
            </fieldset>}
            <div className="ct-exit-actions">{pendingMoveOut ? <button type="button" className="contract-button contract-button--primary" disabled={busy || !readingFor('Electricity') || !readingFor('Water') || !extraValid} onClick={handover}>Xác nhận bàn giao và tiếp tục</button> : <button type="button" className="contract-button contract-button--primary" disabled={!readingFor('Electricity') || !readingFor('Water') || !extraValid} onClick={() => setStep(2)}>Tiếp tục</button>}</div>
          </div>}
          {overview && step === 2 && <div className="ct-exit-stage">
            <div className="ct-exit-columns">
              <section>
                <h3>Hóa đơn cuối · {period.month}/{period.year}</h3>
                {monthly ? <p>Mã {monthly.code} · {monthly.status === 'Draft' ? 'Bản nháp' : 'Đã phát hành'}</p> : <div>{previewLoading && <p role="status">Đang tính hóa đơn cuối…</p>}{previewError && <p className="contract-message contract-message--error" role="alert">{previewError}</p>}{previewError && <button type="button" className="contract-button contract-button--secondary" onClick={() => setRevision(value => value + 1)}>Kiểm tra dữ liệu và thử lại</button>}{previewError && missingRates.length > 0 && navigate && <button type="button" className="contract-button contract-button--ghost" onClick={() => { setOpen(false); navigate(`landlord/billing/rooms/${propertyId}/${contract.roomId}`) }}>Đến trang cấu hình giá</button>}</div>}
                {(currentDetail?.items || currentPreview?.items)?.length ? <ul className="ct-exit-line-items">{(currentDetail?.items || currentPreview?.items).map((row, index) => <li key={row.id || index}><span>{row.description}</span><strong>{money(row.amount)}</strong></li>)}</ul> : <p>Chưa có hóa đơn được tính.</p>}
                {wantsExtra && !extraSaved && <p>Khoản phát sinh dự kiến: {extraDescription} · {money(extra)}</p>}
                <strong>Tổng dự kiến: {money(projectedTotal)}</strong>
                {!monthly && currentPreview && allowed('CreateMonthlyInvoice') && <button type="button" className="contract-button contract-button--primary" disabled={busy || !extraValid} onClick={createDraft}>Tạo hóa đơn nháp</button>}
                {monthly?.status === 'Draft' && wantsExtra && !extraSaved && <button type="button" className="contract-button contract-button--secondary" disabled={busy || !extraValid || !currentDetail} onClick={() => saveExtra(monthly.id, currentDetail.items)}>Lưu khoản phát sinh</button>}
                {inCollect && monthly && <p>Phí phát sinh mới cần được ghi rõ lý do trong <a href={`#/landlord/billing/${monthly.id}`} target="_blank" rel="noopener noreferrer">chi tiết hóa đơn ↗</a>.</p>}
                <div className="ct-exit-separate"><h3>Hóa đơn riêng</h3>
                  {oneOffs.length ? oneOffs.map(row => <div className="ct-exit-invoice-card" key={row.id}><strong>{row.code} · {row.type === 'Damage' ? 'Sửa chữa' : 'Khoản thu khác'}</strong><span>{row.status === 'Draft' ? 'Nháp' : 'Đã phát hành'} · {money(row.effectiveTotal ?? row.baseTotal)}</span></div>) : <p>Chưa có hóa đơn riêng.</p>}
                  {allowed('CreateSettlementOneOff') && extraMode === 'separate' && hasExtraInput && <button type="button" className="contract-button contract-button--secondary" disabled={busy || !extraValid || oneOffAttempted || Boolean(matchingOneOff)} onClick={createSeparateInvoice}>Tạo hóa đơn riêng dạng nháp</button>}
                  {extraMode === 'separate' && oneOffAttempted && !oneOffCreated && <p role="alert">Chưa xác định kết quả tạo hóa đơn. Kiểm tra danh sách hóa đơn trước khi tạo lại để tránh trùng.</p>}
                  {oneOffAttempted && !oneOffCreated && <div className="ct-exit-actions"><button type="button" className="contract-button contract-button--ghost" onClick={() => setRevision(value => value + 1)}>Làm mới danh sách</button><button type="button" className="contract-button contract-button--ghost" onClick={() => setOneOffAttempted(false)} disabled={Boolean(matchingOneOff)}>Đã kiểm tra, thử lại</button></div>}
                  {oneOffCreated && allowed('CreateSettlementOneOff') && <button type="button" className="contract-button contract-button--ghost" onClick={() => { resetOneOffIntent(); setExtraDescription(''); setExtraAmount('') }}>Thêm hóa đơn riêng khác</button>}
                </div>
              </section>
              <section>
                <h3>Đối chiếu tiền cọc</h3><p>Cọc đang giữ: <strong>{money(held)}</strong></p>
                <label>Số cọc muốn cấn trừ vào hóa đơn cuối<input type="number" min="0" max={maxOffset} step="0.01" value={offset} onChange={event => { setOffset(event.target.value); setOffsetKey(crypto.randomUUID()) }} disabled={!monthly} /></label>
                <p>Giới hạn có thể cấn trừ: {money(maxOffset)}. Phần còn lại được hoàn khi tất toán.</p>
                <p>Tổng các hóa đơn: <strong>{money(settlementTotal)}</strong></p>
                <p>Dự kiến còn phải thu: <strong>{money(Math.max(0, projectedDue - Number(offset || 0)))}</strong></p>
              </section>
            </div>
            <section className="ct-exit-old-invoices"><h3>Hóa đơn cũ còn nợ ({oldUnpaid.length})</h3>{oldUnpaid.length ? oldUnpaid.map(row => <OldUnpaidInvoice key={row.id} invoice={row} detail={oldInvoiceDetails[row.id]} />) : <p>Không có hóa đơn cũ còn nợ.</p>}</section>
            <div className="ct-exit-actions">{!inCollect && <button type="button" className="contract-button contract-button--ghost" onClick={() => setStep(1)}>Quay lại</button>}<button type="button" className="contract-button contract-button--primary" disabled={!monthly || !currentDetail || (!inCollect && (!extraValid || !oneOffReady || (wantsExtra && !extraSaved))) || Number(offset) < 0 || Number(offset) > maxOffset} onClick={() => setStep(3)}>{inCollect ? 'Trở lại công nợ' : 'Đối soát lần cuối'}</button></div>
          </div>}
          {overview && step === 3 && <div className="ct-exit-stage">
            <h3>{inCollect ? 'Tiếp tục thu công nợ' : 'Đối soát và phát hành'}</h3>
            {inCollect && <p>{outstanding > 0 ? <>Còn phải thu: <strong>{money(outstanding)}</strong>.</> : missingMonthlyPeriods.length > 0 ? 'Đã thu đủ các hóa đơn hiện có; cần phát hành thêm kỳ còn thiếu trước khi hoàn tất.' : 'Các hóa đơn đã được thanh toán. Kiểm tra cọc và hoàn tất hợp đồng.'} Hóa đơn đã lập được giữ nguyên khi mở lại quy trình.</p>}
            <div className="ct-exit-invoice-card"><strong>Hóa đơn cuối {monthly?.code} · {monthly?.status === 'Issued' ? 'Đã phát hành' : 'Bản nháp'}</strong><ul className="ct-exit-line-items">{currentDetail?.items?.map(row => <li key={row.id}><span>{row.description}</span><strong>{money(row.amount)}</strong></li>)}</ul><p>Tổng hóa đơn: <strong>{money(projectedTotal)}</strong> · Cọc chọn cấn trừ: <strong>{money(offset)}</strong></p>
              {monthly?.status === 'Draft' && <button type="button" className="contract-button contract-button--primary" disabled={busy} onClick={issueAndOffset}>Phát hành hóa đơn cuối{Number(offset) > 0 ? ' và cấn trừ cọc' : ''}</button>}
              {monthly?.status === 'Issued' && Number(offset) > 0 && <button type="button" className="contract-button contract-button--secondary" disabled={busy} onClick={issueAndOffset}>Áp dụng cọc đã chọn</button>}
            </div>
            {oneOffs.map(row => <div className="ct-exit-invoice-card" key={row.id}><strong>Hóa đơn riêng {row.code} · {row.status === 'Draft' ? 'Bản nháp' : 'Đã phát hành'}</strong><ul className="ct-exit-line-items">{oneOffDetails[row.id]?.items?.map(item => <li key={item.id}><span>{item.description}</span><strong>{money(item.amount)}</strong></li>)}</ul><p>Tổng: <strong>{money(row.effectiveTotal ?? row.baseTotal)}</strong> · Còn phải thu: <strong>{money(row.amountDue)}</strong></p>{row.status === 'Draft' && <button type="button" className="contract-button contract-button--secondary" disabled={busy} onClick={() => issueOneOff(row.id)}>Phát hành hóa đơn riêng</button>}</div>)}
            {oldUnpaid.length > 0 && <section className="ct-exit-old-invoices"><h3>Đối soát hóa đơn cũ còn nợ</h3>{oldUnpaid.map(row => <OldUnpaidInvoice key={row.id} invoice={row} detail={oldInvoiceDetails[row.id]} />)}</section>}
            {allowed('RecordPayment') && paymentInvoice && Number(paymentInvoice.amountDue) > 0 && <form className="ct-exit-form" onSubmit={recordPayment}><h3>Ghi nhận thanh toán (có thể một phần)</h3>
              <label>Hóa đơn<select value={paymentInvoice.id} onChange={event => { setPaymentInvoiceId(event.target.value); setPaymentKey(crypto.randomUUID()) }}>{invoices.filter(row => row.status === 'Issued' && Number(row.amountDue) > 0).map(row => <option key={row.id} value={row.id}>{row.code} · còn {money(row.amountDue)}</option>)}</select></label>
              <label>Số đã nhận<input type="number" min="0.01" step="0.01" required value={payment} onChange={event => { setPayment(event.target.value); setPaymentKey(crypto.randomUUID()) }} /></label>
              <label>Phương thức<select value={method} onChange={event => { setMethod(event.target.value); setPaymentKey(crypto.randomUUID()) }}><option value="BankTransfer">Chuyển khoản</option><option value="Cash">Tiền mặt</option></select></label>
              {method === 'BankTransfer' && <label>Mã giao dịch<input maxLength="150" value={transactionCode} onChange={event => { setTransactionCode(event.target.value); setPaymentKey(crypto.randomUUID()) }} /></label>}
              <EvidenceSelector selection={paymentSelection} onChange={setPaymentSelection} disabled={busy || Boolean(pendingPaymentUpload)} />
              <button className="contract-button contract-button--primary" disabled={busy || Boolean(pendingPaymentUpload)}>Ghi nhận khoản đã thu</button>
            </form>}
            {pendingPaymentUpload && <div className="ct-exit-upload-retry"><p>Khoản thanh toán đã được ghi nhận. Ảnh chứng từ chưa tải lên.</p><button type="button" className="contract-button contract-button--secondary" disabled={busy} onClick={() => uploadManualPaymentEvidence(pendingPaymentUpload.paymentId, pendingPaymentUpload.selection)}>Thử tải lại ảnh thanh toán</button></div>}
            <form className="ct-exit-form" onSubmit={finish}><h3>Hoàn tất hợp đồng</h3>{held > 0 && <label>Số cọc đã hoàn<input type="number" min="0" max={held} step="0.01" value={refund} onChange={event => setRefund(event.target.value)} /></label>}{held > 0 && <EvidenceSelector selection={refundSelection} onChange={setRefundSelection} disabled={busy || settlementCompleted} />}<label>Ghi chú<input maxLength="1000" value={note} onChange={event => setNote(event.target.value)} /></label><p>Chỉ hoàn tất khi mọi hóa đơn đã phát hành, công nợ bằng 0 và đã xử lý đủ cọc còn giữ. Ảnh hoàn cọc được lưu sau khi hoàn tất.</p><button className="contract-button contract-button--secondary" disabled={busy || settlementCompleted || Boolean(pendingPaymentUpload) || !allowed('CompleteSettlement') || unsettledInvoices.length > 0 || Number(overview?.outstandingAmount) !== 0 || overview?.hasDraftInvoices || (held > 0 && Number(refund) !== held)}>Hoàn tất hợp đồng</button></form>
            {pendingRefundUpload && <div className="ct-exit-upload-retry"><p>Hợp đồng đã tất toán. Hãy tải lại ảnh hoàn cọc; không bấm hoàn tất lần nữa.</p><button type="button" className="contract-button contract-button--secondary" disabled={busy} onClick={retryRefundEvidence}>Thử tải lại ảnh hoàn cọc</button></div>}
            <div className="ct-exit-actions"><button type="button" className="contract-button contract-button--ghost" onClick={() => setStep(2)}>Xem lại hóa đơn và cọc</button><button type="button" className="contract-button contract-button--ghost" onClick={() => setRevision(value => value + 1)}>Làm mới công nợ</button></div>
          </div>}
        </div>
      </div>
    </div>}
  </section>
}
