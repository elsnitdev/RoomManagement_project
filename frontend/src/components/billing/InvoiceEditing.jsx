import { useState } from 'react'
import { billingApi } from '../../services/api/billingApi'

const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const editableTypes = ['Service', 'Damage', 'Other', 'Discount']
const typeLabel = { Service: 'Dịch vụ', Damage: 'Sửa chữa', Other: 'Khoản khác', Discount: 'Giảm trừ', AdditionalCharge: 'Phụ thu', Correction: 'Điều chỉnh giảm', WriteOff: 'Xóa nợ' }
const newRow = () => ({ key: crypto.randomUUID(), type: 'Service', description: '', quantity: '1', unitPrice: '' })
const rowAmount = item => (item.type === 'Discount' ? -1 : 1) * Math.round(Number(item.quantity || 0) * Number(item.unitPrice || 0) * 100) / 100
const isMoney = value => value !== '' && Number.isFinite(Number(value)) && Number(value) >= 0 && Math.round(Number(value) * 100) === Number(value) * 100
const isQuantity = value => isMoney(value) && Number(value) > 0

export function DraftItemsEditor({ invoiceId, items, onChanged, initialOpen = false }) {
  const [open, setOpen] = useState(initialOpen)
  const [rows, setRows] = useState(() => items.filter(item => editableTypes.includes(item.type)).map(item => ({ ...item, key: item.id || crypto.randomUUID(), quantity: String(item.quantity), unitPrice: String(item.unitPrice) })))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const fixedTotal = items.filter(item => !editableTypes.includes(item.type)).reduce((sum, item) => sum + Number(item.amount || 0), 0)
  const proposedTotal = fixedTotal + rows.reduce((sum, item) => sum + rowAmount(item), 0)

  function update(key, field, value) { setRows(current => current.map(row => row.key === key ? { ...row, [field]: value } : row)) }
  async function save(event) {
    event.preventDefault()
    if (rows.some(row => !row.description.trim() || !isQuantity(row.quantity) || !isMoney(row.unitPrice)) || !Number.isFinite(proposedTotal) || proposedTotal <= 0) {
      setError('Kiểm tra mô tả, số lượng, đơn giá và tổng hóa đơn.')
      return
    }
    setBusy(true)
    setError('')
    try {
      await billingApi.saveDraftItems(invoiceId, { items: rows.map(row => ({ ...(row.id ? { id: row.id } : {}), type: row.type, description: row.description.trim(), quantity: Number(row.quantity), unitPrice: Number(row.unitPrice) })) })
      setOpen(false)
      onChanged('Đã cập nhật các khoản của hóa đơn nháp.')
    } catch (cause) { setError(cause.message || 'Không thể lưu thay đổi.') }
    finally { setBusy(false) }
  }

  return <div className="bill-editor">
    <div className="bill-section-actions"><div><h3>Chỉnh dịch vụ và các khoản kỳ này</h3><p>Thay đổi chỉ áp dụng cho hóa đơn nháp này.</p></div><button type="button" className="bill-button bill-button--secondary" aria-expanded={open} onClick={() => setOpen(value => !value)}>{open ? 'Đóng chỉnh sửa' : 'Chỉnh các khoản'}</button></div>
    {open && <form onSubmit={save}>
      {rows.map((row, index) => <div className="bill-editor-row bill-editor-row--draft" key={row.key}>
        <label>Loại khoản {index + 1}<select value={row.type} disabled={Boolean(row.id)} onChange={event => update(row.key, 'type', event.target.value)}>{editableTypes.map(type => <option key={type} value={type}>{typeLabel[type]}</option>)}</select></label>
        <label>Mô tả *<input required maxLength={255} value={row.description} onChange={event => update(row.key, 'description', event.target.value)} /></label>
        <label>Số lượng *<input required type="number" min="0.01" step="0.01" value={row.quantity} onChange={event => update(row.key, 'quantity', event.target.value)} /></label>
        <label>Đơn giá (₫) *<input required type="number" min="0" step="0.01" value={row.unitPrice} onChange={event => update(row.key, 'unitPrice', event.target.value)} /></label>
        <button className="bill-link" type="button" onClick={() => setRows(current => current.filter(item => item.key !== row.key))}>Bỏ khoản</button>
      </div>)}
      <div className="bill-section-actions"><button className="bill-button bill-button--secondary" type="button" onClick={() => setRows(current => [...current, newRow()])}>Thêm dịch vụ hoặc khoản thu/trừ</button><strong>Tổng mới: {money(proposedTotal)}</strong></div>
      {error && <p className="bill-error" role="alert">{error}</p>}
      <button className="bill-button" type="submit" disabled={busy || proposedTotal <= 0}>{busy ? 'Đang lưu…' : 'Lưu thay đổi'}</button>
    </form>}
  </div>
}

export function InvoiceAdjustmentHistory({ adjustments = [] }) {
  if (!adjustments.length) return <p className="bill-empty">Chưa có khoản cộng hoặc trừ sau phát hành.</p>
  return <ul className="bill-adjustment-list">{adjustments.map(item => <li className="bill-adjustment-row" key={item.id}>
    <div><strong>{typeLabel[item.type] || item.type}</strong><p>{item.reason}</p><small>{item.createdAt ? new Date(item.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : ''}{item.voidedAt ? ' · Đã hủy điều chỉnh' : ''}</small></div>
    <strong className="bill-adjustment-row__amount">{item.type === 'AdditionalCharge' ? '+' : '−'}{money(item.amount)}</strong>
  </li>)}</ul>
}

export function InvoiceAdjustmentEditor({ invoiceId, adjustments, summary, onChanged }) {
  const [type, setType] = useState('AdditionalCharge')
  const [amount, setAmount] = useState('')
  const [reason, setReason] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const signed = type === 'AdditionalCharge' ? 1 : -1
  const newEffective = Number(summary.effectiveTotal || 0) + signed * Number(amount || 0)

  async function submit(event) {
    event.preventDefault()
    if (!reason.trim() || !isMoney(amount) || Number(amount) <= 0 || !Number.isFinite(newEffective) || newEffective < 0) {
      setError('Nhập số tiền hợp lệ và lý do; tổng phải thu không được âm.')
      return
    }
    setBusy(true)
    setError('')
    try {
      await billingApi.addAdjustment(invoiceId, { type, amount: Number(amount), reason: reason.trim(), idempotencyKey })
      setAmount('')
      setReason('')
      setIdempotencyKey(crypto.randomUUID())
      onChanged('Đã ghi khoản cộng/trừ và cập nhật số dư.')
    } catch (cause) { setError(cause.message || 'Không thể ghi điều chỉnh.') }
    finally { setBusy(false) }
  }

  return <section className="bill-card" aria-labelledby="bill-adjustment-title">
    <h2 id="bill-adjustment-title">Phụ thu và giảm trừ</h2>
    <p>Mỗi khoản phát sinh sau phát hành được ghi riêng cùng lý do.</p>
    <form className="bill-editor" onSubmit={submit}>
      <div className="bill-form-grid">
        <label>Loại điều chỉnh *<select value={type} onChange={event => setType(event.target.value)}><option value="AdditionalCharge">Thu thêm tiền</option><option value="Discount">Giảm trừ</option><option value="Correction">Điều chỉnh giảm do sai sót</option><option value="WriteOff">Xóa nợ</option></select></label>
        <label>Số tiền (₫) *<input required type="number" min="0.01" step="0.01" value={amount} onChange={event => setAmount(event.target.value)} /></label>
        <label>Lý do *<input required maxLength={500} value={reason} onChange={event => setReason(event.target.value)} placeholder="Ví dụ: phí sửa khóa cửa" /></label>
      </div>
      <p>Tổng hiện tại: <strong>{money(summary.effectiveTotal)}</strong> → sau điều chỉnh: <strong>{money(newEffective)}</strong></p>
      {error && <p className="bill-error" role="alert">{error}</p>}
      <button className="bill-button" type="submit" disabled={busy || !isMoney(amount) || Number(amount) <= 0 || !reason.trim() || !Number.isFinite(newEffective) || newEffective < 0}>{busy ? 'Đang lưu…' : 'Ghi khoản điều chỉnh'}</button>
    </form>
    <h3>Lịch sử điều chỉnh</h3>
    <InvoiceAdjustmentHistory adjustments={adjustments} />
  </section>
}
