import { useEffect, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'
import '../../styles/billing-live.css'

export default function AdminServices() {
  const [services, setServices] = useState([])
  const [form, setForm] = useState({ name: '', description: '', unit: '', defaultPrice: '' })
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  async function load() {
    setLoading(true)
    try { setServices(await billingApi.services()) }
    catch (cause) { setError(cause.message) }
    finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])
  async function submit(event) {
    event.preventDefault(); setBusy(true); setError(''); setSuccess('')
    try {
      await billingApi.createService({ ...form, defaultPrice: form.defaultPrice === '' ? null : Number(form.defaultPrice) })
      setForm({ name: '', description: '', unit: '', defaultPrice: '' })
      setSuccess('Đã tạo dịch vụ.'); await load()
    } catch (cause) { setError(cause.message) }
    finally { setBusy(false) }
  }
  return <div className="bill-page"><header className="bill-header"><div><span>QUẢN TRỊ</span><h1>Danh mục dịch vụ</h1><p>Tạo dịch vụ dùng chung để chủ trọ gán cho từng phòng.</p></div></header>
    <div className="bill-grid bill-grid--two"><section className="bill-card"><h2>Tạo dịch vụ</h2><form className="bill-form" onSubmit={submit}><label>Tên dịch vụ<input required maxLength="100" value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} /></label><label>Đơn vị<input maxLength="50" value={form.unit} onChange={event => setForm({ ...form, unit: event.target.value })} /></label><label>Giá mặc định<input type="number" min="0" step="0.01" value={form.defaultPrice} onChange={event => setForm({ ...form, defaultPrice: event.target.value })} /></label><label className="bill-field--wide">Mô tả<textarea maxLength="500" value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} /></label><button className="bill-button" disabled={busy}>Tạo dịch vụ</button></form></section><section className="bill-card"><h2>Dịch vụ hiện có</h2>{loading ? <p role="status">Đang tải...</p> : services.length ? <ul className="bill-list">{services.map(item => <li key={item.id}><strong>{item.name}</strong><small>{item.description || 'Không có mô tả'} · {item.defaultPrice == null ? 'Chưa có giá' : `${new Intl.NumberFormat('vi-VN').format(item.defaultPrice)} ₫`}/{item.unit || 'lần'}</small></li>)}</ul> : <p className="bill-empty">Chưa có dịch vụ.</p>}</section></div>
    {error && <p className="bill-error" role="alert">{error}</p>}{success && <p className="bill-success" role="status">{success}</p>}
  </div>
}
