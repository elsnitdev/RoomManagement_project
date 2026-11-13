import { useState } from 'react'
import { billingApi } from '../../services/api/billingApi'

const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const today = () => new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10)
const newItem = () => ({ key: crypto.randomUUID(), description: '', quantity: '1', unitPrice: '' })

export default function OneOffInvoiceForm({ propertyId, contracts, roomName, onCreated }) {
  const [open, setOpen] = useState(false)
  const [contractId, setContractId] = useState('')
  const [type, setType] = useState('Other')
  const [dueDate, setDueDate] = useState(today)
  const [note, setNote] = useState('')
  const [items, setItems] = useState([newItem()])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const eligible = contracts.filter(item => ['Active', 'PendingMoveOut', 'PendingSettlement'].includes(item.status))
  const total = items.reduce((sum, item) => sum + Number(item.quantity || 0) * Number(item.unitPrice || 0), 0)

  function updateItem(key, field, value) {
    setItems(current => current.map(item => item.key === key ? { ...item, [field]: value } : item))
  }

  async function submit(event) {
    event.preventDefault()
    if (!contractId || !items.length || items.some(item => !item.description.trim() || Number(item.quantity) <= 0 || Number(item.unitPrice) < 0 || !item.unitPrice) || total <= 0) {
      setError('Chọn hợp đồng và nhập ít nhất một khoản thu hợp lệ.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const result = await billingApi.createOneOff(propertyId, {
        contractId, type, dueDate, note: note.trim() || null,
        items: items.map(item => ({ type: type === 'Damage' ? 'Damage' : 'Other', description: item.description.trim(), quantity: Number(item.quantity), unitPrice: Number(item.unitPrice) })),
      })
      onCreated(result)
    } catch (cause) {
      setError(cause.message || 'Không thể tạo hóa đơn riêng.')
    } finally {
      setBusy(false)
    }
  }

  return <section className="bill-card" aria-labelledby="bill-one-off-title">
    <div className="bill-list-heading"><div><h2 id="bill-one-off-title">Hóa đơn riêng lẻ</h2><p>Thu phí sửa chữa hoặc khoản phát sinh ngoài hóa đơn tháng.</p></div>
      <button type="button" className="bill-button bill-button--secondary" aria-expanded={open} onClick={() => setOpen(value => !value)}>{open ? 'Đóng biểu mẫu' : 'Tạo hóa đơn riêng'}</button>
    </div>
    {open && <form className="bill-editor" onSubmit={submit}>
      <div className="bill-form-grid">
        <label>Hợp đồng *<select required value={contractId} onChange={event => setContractId(event.target.value)}><option value="">Chọn hợp đồng</option>{eligible.map(item => <option value={item.id} key={item.id}>{item.contractCode} · Phòng {roomName(item.roomId)}</option>)}</select></label>
        <label>Mục đích *<select value={type} onChange={event => setType(event.target.value)}><option value="Other">Thu thêm tiền / khoản khác</option><option value="Damage">Phí sửa chữa, bồi thường</option></select></label>
        <label>Hạn thanh toán *<input type="date" required min={today()} value={dueDate} onChange={event => setDueDate(event.target.value)} /></label>
        <label>Ghi chú<input maxLength={1000} value={note} onChange={event => setNote(event.target.value)} placeholder="Thông tin để người thuê hiểu khoản thu" /></label>
      </div>
      <h3>Các khoản thu</h3>
      {items.map((item, index) => <div className="bill-editor-row" key={item.key}>
        <label>Khoản {index + 1} *<input required maxLength={255} value={item.description} onChange={event => updateItem(item.key, 'description', event.target.value)} placeholder="Ví dụ: sửa khóa cửa" /></label>
        <label>Số lượng *<input required type="number" min="0.01" step="0.01" value={item.quantity} onChange={event => updateItem(item.key, 'quantity', event.target.value)} /></label>
        <label>Đơn giá (₫) *<input required type="number" min="0" step="0.01" value={item.unitPrice} onChange={event => updateItem(item.key, 'unitPrice', event.target.value)} /></label>
        <button type="button" className="bill-link" disabled={items.length === 1} onClick={() => setItems(current => current.filter(row => row.key !== item.key))}>Bỏ khoản</button>
      </div>)}
      <div className="bill-section-actions"><button type="button" className="bill-button bill-button--secondary" onClick={() => setItems(current => [...current, newItem()])}>Thêm khoản thu</button><strong>Tổng dự kiến: {money(total)}</strong></div>
      {error && <p className="bill-error" role="alert">{error}</p>}
      <button type="submit" className="bill-button" disabled={busy || total <= 0}>{busy ? 'Đang tạo…' : 'Tạo bản nháp'}</button>
    </form>}
  </section>
}
