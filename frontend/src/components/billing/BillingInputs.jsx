import { useEffect, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'

const today = () => new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10)
const nextMonth = () => { const [y, m] = today().split('-').map(Number); return new Date(Date.UTC(y, m, 1)).toISOString().slice(0, 10) }
const monthAfter = date => { const [y, m] = date.split('-').map(Number); return new Date(Date.UTC(y, m, 1)).toISOString().slice(0, 10) }
const rateDraft = type => ({ type, unitPrice: '', unit: type === 'Electricity' ? 'kWh' : 'm³', effectiveFrom: nextMonth() })
const readingDraft = () => ({ type: 'Electricity', year: Number(today().slice(0, 4)), month: Number(today().slice(5, 7)), previousValue: '', currentValue: '', note: '' })
const serviceDraft = () => ({ name: '', description: '', unit: 'tháng', defaultPrice: '', isActive: true })
const assignmentDraft = () => ({ serviceId: '', customPrice: '', quantity: '1', startDate: nextMonth() })
const money = n => new Intl.NumberFormat('vi-VN').format(Number(n || 0)) + ' ₫'
const nameOf = type => type === 'Electricity' ? 'Điện' : 'Nước'
const activeRate = (rates, type) => rates.filter(r => r.type === type && r.effectiveFrom <= today() && (!r.effectiveTo || r.effectiveTo >= today())).sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))[0]
const futureRate = (rates, type) => rates.filter(r => r.type === type && r.effectiveFrom > today()).sort((a, b) => a.effectiveFrom.localeCompare(b.effectiveFrom))[0]

export default function BillingInputs({ propertyId, rooms = [], onChanged }) {
  const [roomId, setRoomId] = useState('')
  const [rates, setRates] = useState([])
  const [readings, setReadings] = useState([])
  const [services, setServices] = useState([])
  const [assignments, setAssignments] = useState([])
  const [rateForm, setRateForm] = useState(null)
  const [reading, setReading] = useState(readingDraft)
  const [serviceForm, setServiceForm] = useState(null)
  const [assignmentForm, setAssignmentForm] = useState(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [revision, setRevision] = useState(0)

  useEffect(() => { setRoomId(''); setRates([]); setReadings([]); setServices([]); setAssignments([]); setRateForm(null); setServiceForm(null); setAssignmentForm(null) }, [propertyId])
  useEffect(() => { if (roomId && !rooms.some(r => String(r.id) === String(roomId))) setRoomId('') }, [roomId, rooms])
  useEffect(() => { setAssignmentForm(null) }, [roomId])
  useEffect(() => {
    if (!propertyId) return
    const controller = new AbortController()
    setLoading(true)
    Promise.all([
      billingApi.rates(propertyId, controller.signal),
      billingApi.propertyServices(propertyId, controller.signal),
      roomId ? billingApi.readings(propertyId, roomId, controller.signal) : Promise.resolve([]),
      roomId ? billingApi.roomServices(propertyId, roomId, controller.signal) : Promise.resolve([]),
    ]).then(([r, s, m, a]) => { setRates(r || []); setServices(s || []); setReadings(m || []); setAssignments(a || []) })
      .catch(e => { if (e.name !== 'AbortError') setError(e.message || 'Không tải được dữ liệu.') })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [propertyId, roomId, revision])

  async function submit(event, action, reset, message) {
    event.preventDefault(); setBusy(true); setError(''); setSuccess('')
    try { await action(); reset(); setSuccess(message); setRevision(v => v + 1); onChanged?.() }
    catch (e) { setError(e.message || 'Không thể hoàn tất thao tác.') }
    finally { setBusy(false) }
  }
  function editRate(type) {
    const existing = activeRate(rates, type) || futureRate(rates, type)
    setRateForm({ ...rateDraft(type), unitPrice: existing?.unitPrice ?? '', unit: existing?.unit || rateDraft(type).unit, initial: !rates.some(r => r.type === type), effectiveFrom: existing ? nextMonth() : today().slice(0, 7) + '-01' })
  }
  function saveRate(event) {
    const body = { type: rateForm.type, unitPrice: Number(rateForm.unitPrice), unit: rateForm.unit.trim(), effectiveFrom: rateForm.effectiveFrom, effectiveTo: null }
    submit(event, () => rateForm.initial ? billingApi.createRate(propertyId, body) : billingApi.reviseRate(propertyId, body), () => setRateForm(null), 'Đã lưu đơn giá.')
  }
  function saveService(event) {
    const body = { name: serviceForm.name.trim(), description: serviceForm.description.trim() || null, unit: serviceForm.unit.trim(), defaultPrice: serviceForm.defaultPrice === '' ? null : Number(serviceForm.defaultPrice), isActive: serviceForm.isActive }
    submit(event, () => serviceForm.id ? billingApi.updateService(propertyId, serviceForm.id, body) : billingApi.createPropertyService(propertyId, body), () => setServiceForm(null), 'Đã lưu dịch vụ của khu trọ.')
  }
  function saveAssignment(event) {
    const body = { serviceId: assignmentForm.serviceId, customPrice: assignmentForm.customPrice === '' ? null : Number(assignmentForm.customPrice), quantity: Number(assignmentForm.quantity), startDate: assignmentForm.startDate, endDate: null }
    submit(event, () => assignmentForm.id ? billingApi.reviseRoomService(propertyId, roomId, assignmentForm.id, body) : billingApi.assignService(propertyId, roomId, body), () => setAssignmentForm(null), 'Đã lưu dịch vụ. Thay đổi áp dụng từ tháng sau.')
  }
  const chosen = services.find(s => String(s.id) === String(assignmentForm?.serviceId))
  const unitPrice = assignmentForm?.customPrice !== '' ? Number(assignmentForm?.customPrice) : Number(chosen?.defaultPrice || 0)
  const quantity = Number(assignmentForm?.quantity || 0)
  const earliestAssignmentDate = assignmentForm?.earliestStartDate || nextMonth()
  if (!propertyId) return <p className="bill-empty">Chọn khu trọ để nhập dữ liệu tính hóa đơn.</p>
  return <div className="bill-inputs">
    <section className="bill-card">
      <h2>Giá điện nước hiện hành</h2><p>Giá được áp dụng theo khu trọ. Chỉ mở biểu mẫu khi muốn thay đổi.</p>
      <div className="bill-rate-grid">{['Electricity', 'Water'].map(type => {
        const current = activeRate(rates, type), upcoming = futureRate(rates, type)
        return <div className="bill-rate-card" key={type}><div><span>{nameOf(type)}</span><strong>{current ? money(current.unitPrice) + '/' + current.unit : 'Chưa có đơn giá hiện hành'}</strong>{current && <small>Từ {current.effectiveFrom}</small>}{upcoming && <small>Giá từ {upcoming.effectiveFrom}: {money(upcoming.unitPrice)}/{upcoming.unit}</small>}</div><button type="button" className="bill-button bill-button--secondary" onClick={() => editRate(type)} disabled={busy}>{rates.some(r => r.type === type) ? 'Chỉnh giá' : 'Thêm giá ban đầu'}</button></div>
      })}</div>
      {rateForm && <form className="bill-form bill-edit-panel" onSubmit={saveRate}>
        <div className="bill-field--wide"><h3>{rateForm.initial ? 'Thêm giá ban đầu' : 'Chỉnh giá ' + nameOf(rateForm.type).toLowerCase()}</h3><p className="bill-hint">{rateForm.initial ? 'Giá ban đầu có thể áp dụng từ đầu tháng hiện tại.' : 'Giá sửa đổi có hiệu lực từ ngày đầu tháng sau hoặc muộn hơn.'}</p></div>
        <label>Đơn giá<input type="number" min="0" step="0.01" required value={rateForm.unitPrice} onChange={e => setRateForm({ ...rateForm, unitPrice: e.target.value })} /></label>
        <label>Đơn vị<input required maxLength="50" value={rateForm.unit} onChange={e => setRateForm({ ...rateForm, unit: e.target.value })} /></label>
        <label>Ngày bắt đầu<input type="date" required min={rateForm.initial ? today().slice(0, 7) + '-01' : nextMonth()} value={rateForm.effectiveFrom} onChange={e => setRateForm({ ...rateForm, effectiveFrom: e.target.value })} /></label>
        <div className="bill-form-actions"><button className="bill-button" disabled={busy || (!rateForm.initial && !rateForm.effectiveFrom.endsWith('-01'))}>Lưu giá</button><button type="button" className="bill-button bill-button--secondary" onClick={() => setRateForm(null)}>Hủy</button></div>
      </form>}
    </section>
    <div className="bill-grid bill-grid--two">
      <section className="bill-card">
        <div className="bill-section-head"><div><h2>Danh mục dịch vụ của khu trọ</h2><p>Mỗi khu trọ có danh mục và giá riêng.</p></div><button type="button" className="bill-button bill-button--secondary" onClick={() => setServiceForm(serviceDraft())}>Thêm dịch vụ</button></div>
        {serviceForm && <form className="bill-form bill-edit-panel" onSubmit={saveService}>
          <div className="bill-field--wide"><h3>{serviceForm.id ? 'Sửa dịch vụ' : 'Tạo dịch vụ'}</h3><p className="bill-hint">Thay đổi gán cho phòng bắt đầu từ tháng sau.</p></div>
          <label>Tên dịch vụ<input required maxLength="100" value={serviceForm.name} onChange={e => setServiceForm({ ...serviceForm, name: e.target.value })} /></label>
          <label>Đơn vị tính<input required maxLength="50" value={serviceForm.unit} onChange={e => setServiceForm({ ...serviceForm, unit: e.target.value })} placeholder="xe, người, tháng" /></label>
          <label>Giá mặc định<input type="number" min="0" step="0.01" value={serviceForm.defaultPrice} onChange={e => setServiceForm({ ...serviceForm, defaultPrice: e.target.value })} /></label>
          {serviceForm.id && <label className="bill-checkbox"><input type="checkbox" checked={serviceForm.isActive} onChange={e => setServiceForm({ ...serviceForm, isActive: e.target.checked })} /> Đang sử dụng</label>}
          <label className="bill-field--wide">Mô tả<textarea maxLength="500" value={serviceForm.description || ''} onChange={e => setServiceForm({ ...serviceForm, description: e.target.value })} /></label>
          <div className="bill-form-actions bill-field--wide"><button className="bill-button" disabled={busy}>Lưu dịch vụ</button><button type="button" className="bill-button bill-button--secondary" onClick={() => setServiceForm(null)}>Hủy</button></div>
        </form>}
        {services.length ? <ul className="bill-list">{services.map(s => <li key={s.id}><div><strong>{s.name} · {s.defaultPrice == null ? 'Chưa có giá' : money(s.defaultPrice) + '/' + s.unit}</strong><small>{s.isActive ? 'Đang sử dụng' : 'Ngừng sử dụng'}{s.description ? ' · ' + s.description : ''}</small></div><button type="button" className="bill-link" onClick={() => setServiceForm({ ...s, defaultPrice: s.defaultPrice ?? '' })}>Sửa {s.name}</button></li>)}</ul> : <p className="bill-empty">Khu trọ chưa có dịch vụ.</p>}
      </section>
      <section className="bill-card">
        <h2>Chỉ số điện nước</h2>
        <label className="bill-field">Phòng<select value={roomId} onChange={e => setRoomId(e.target.value)}><option value="">Chọn phòng</option>{rooms.map(r => <option key={r.id} value={r.id}>{r.roomNumber || r.number || r.id}</option>)}</select></label>
        {roomId ? <><form className="bill-form" onSubmit={e => submit(e, () => billingApi.createReading(propertyId, roomId, { ...reading, year: Number(reading.year), month: Number(reading.month), previousValue: Number(reading.previousValue), currentValue: Number(reading.currentValue) }), () => setReading(readingDraft()), 'Đã lưu chỉ số.')}>
          <label>Loại<select value={reading.type} onChange={e => setReading({ ...reading, type: e.target.value })}><option value="Electricity">Điện</option><option value="Water">Nước</option></select></label>
          <label>Năm<input type="number" min="2000" max="2100" required value={reading.year} onChange={e => setReading({ ...reading, year: e.target.value })} /></label>
          <label>Tháng<input type="number" min="1" max="12" required value={reading.month} onChange={e => setReading({ ...reading, month: e.target.value })} /></label>
          <label>Chỉ số đầu kỳ<input type="number" min="0" step="0.01" required value={reading.previousValue} onChange={e => setReading({ ...reading, previousValue: e.target.value })} /></label>
          <label>Chỉ số cuối kỳ<input type="number" min={reading.previousValue || 0} step="0.01" required value={reading.currentValue} onChange={e => setReading({ ...reading, currentValue: e.target.value })} /></label>
          <label className="bill-field--wide">Ghi chú<input maxLength="500" value={reading.note} onChange={e => setReading({ ...reading, note: e.target.value })} /></label>
          <button className="bill-button" disabled={busy}>Lưu chỉ số</button>
        </form><ul className="bill-list">{readings.map(r => <li key={r.id}><strong>{nameOf(r.type)} · {r.month}/{r.year}: {r.used} đơn vị</strong><small>{r.previousValue} → {r.currentValue}</small></li>)}</ul></> : <p className="bill-empty">Chọn phòng để nhập chỉ số và gán dịch vụ.</p>}
      </section>
    </div>
    {roomId && <section className="bill-card">
      <div className="bill-section-head"><div><h2>Dịch vụ của phòng</h2><p>Nhập số lượng theo phòng. Dịch vụ mới và thay đổi bắt đầu từ tháng sau.</p></div><button type="button" className="bill-button bill-button--secondary" disabled={!services.some(s => s.isActive)} onClick={() => setAssignmentForm(assignmentDraft())}>Gán dịch vụ</button></div>
      {assignmentForm && <form className="bill-form bill-form--inline bill-edit-panel" onSubmit={saveAssignment}>
        <div className="bill-field--wide"><h3>{assignmentForm.id ? 'Cập nhật dịch vụ' : 'Gán dịch vụ'}</h3><p className="bill-hint">Ngày bắt đầu phải là ngày 1 của tháng sau hoặc muộn hơn.</p></div>
        <label>Dịch vụ<select required disabled={Boolean(assignmentForm.id)} value={assignmentForm.serviceId} onChange={e => setAssignmentForm({ ...assignmentForm, serviceId: e.target.value })}><option value="">Chọn dịch vụ</option>{services.filter(s => s.isActive || String(s.id) === String(assignmentForm.serviceId)).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
        <label>Số lượng<input type="number" min="1" step="1" required value={assignmentForm.quantity} onChange={e => setAssignmentForm({ ...assignmentForm, quantity: e.target.value })} /></label>
        <label>Giá riêng (để trống dùng giá mặc định)<input type="number" min="0" step="0.01" value={assignmentForm.customPrice} onChange={e => setAssignmentForm({ ...assignmentForm, customPrice: e.target.value })} /></label>
        <label>Ngày bắt đầu<input type="date" min={earliestAssignmentDate} required value={assignmentForm.startDate} onChange={e => setAssignmentForm({ ...assignmentForm, startDate: e.target.value })} /></label>
        <p className="bill-assignment-total bill-field--wide" aria-live="polite">Dự kiến: <strong>{chosen && quantity > 0 ? quantity + ' × ' + money(unitPrice) + ' = ' + money(quantity * unitPrice) : 'Chọn dịch vụ và nhập số lượng'}</strong></p>
        <div className="bill-form-actions bill-field--wide"><button className="bill-button" disabled={busy || !assignmentForm.serviceId || (chosen?.defaultPrice == null && assignmentForm.customPrice === '') || !Number.isInteger(quantity) || quantity < 1 || assignmentForm.startDate < earliestAssignmentDate || !assignmentForm.startDate.endsWith('-01')}>Lưu dịch vụ</button><button type="button" className="bill-button bill-button--secondary" onClick={() => setAssignmentForm(null)}>Hủy</button></div>
      </form>}
      {assignments.length ? <ul className="bill-list">{assignments.map(a => <li key={a.id}><div><strong>{a.serviceName} · {Number(a.quantity || 1)} × {money(a.unitPrice)} = {money(Number(a.quantity || 1) * Number(a.unitPrice || 0))}</strong><small>{a.startDate} → {a.endDate || 'Chưa kết thúc'}</small></div>{!a.endDate && services.find(s => String(s.id) === String(a.serviceId))?.isActive && <button type="button" className="bill-link" onClick={() => { const earliestStartDate = monthAfter(a.startDate) > nextMonth() ? monthAfter(a.startDate) : nextMonth(); setAssignmentForm({ ...assignmentDraft(), id: a.id, serviceId: a.serviceId, quantity: String(a.quantity || 1), customPrice: a.unitPrice ?? '', startDate: earliestStartDate, earliestStartDate }) }}>Chỉnh số lượng / giá</button>}</li>)}</ul> : <p className="bill-empty">Phòng chưa được gán dịch vụ.</p>}
    </section>}
    {loading && <p role="status">Đang tải dữ liệu...</p>}
    {error && <p className="bill-error" role="alert">{error}</p>}
    {success && <p className="bill-success" role="status">{success}</p>}
  </div>
}
