import { useEffect, useState } from 'react'
import { billingApi } from '../../services/api/billingApi'

const today = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10)
const initialRate = { type: 'Electricity', unitPrice: '', unit: 'kWh', effectiveFrom: today, effectiveTo: '' }
const initialReading = { type: 'Electricity', year: new Date().getFullYear(), month: new Date().getMonth() + 1, previousValue: '', currentValue: '', note: '' }
const initialAssignment = { serviceId: '', customPrice: '', startDate: today, endDate: '' }
const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const typeName = type => type === 'Electricity' ? 'Điện' : 'Nước'
const errorOf = error => error?.message || 'Không thể hoàn tất thao tác.'

export default function BillingInputs({ propertyId, rooms, onChanged }) {
  const [roomId, setRoomId] = useState('')
  const [rates, setRates] = useState([])
  const [readings, setReadings] = useState([])
  const [services, setServices] = useState([])
  const [assignments, setAssignments] = useState([])
  const [rate, setRate] = useState(initialRate)
  const [reading, setReading] = useState(initialReading)
  const [assignment, setAssignment] = useState(initialAssignment)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [revision, setRevision] = useState(0)

  useEffect(() => { setRoomId(''); setRates([]); setReadings([]); setAssignments([]) }, [propertyId])
  useEffect(() => {
    if (!roomId || rooms.some(room => room.id === roomId)) return
    setRoomId('')
  }, [roomId, rooms])
  useEffect(() => {
    if (!propertyId) return
    const controller = new AbortController()
    setLoading(true)
    setError('')
    Promise.all([
      billingApi.rates(propertyId, controller.signal),
      billingApi.services(controller.signal),
      roomId ? billingApi.readings(propertyId, roomId, controller.signal) : Promise.resolve([]),
      roomId ? billingApi.roomServices(propertyId, roomId, controller.signal) : Promise.resolve([]),
    ]).then(([nextRates, nextServices, nextReadings, nextAssignments]) => {
      setRates(nextRates); setServices(nextServices); setReadings(nextReadings); setAssignments(nextAssignments)
    }).catch(cause => { if (cause.name !== 'AbortError') setError(errorOf(cause)) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [propertyId, roomId, revision])

  async function submit(event, action, clear, message) {
    event.preventDefault()
    setBusy(true); setError(''); setSuccess('')
    try {
      await action()
      clear()
      setSuccess(message)
      setRevision(value => value + 1)
      onChanged?.()
    } catch (cause) { setError(errorOf(cause)) }
    finally { setBusy(false) }
  }

  if (!propertyId) return <p className="bill-empty">Chọn khu trọ để nhập dữ liệu tính hóa đơn.</p>
  return <div className="bill-inputs">
    <div className="bill-grid bill-grid--two">
      <section className="bill-card">
        <h2>Giá điện nước</h2>
        <p>Giá áp dụng khi tính hóa đơn được lấy tại ngày đầu kỳ. Nếu chưa có ngày kết thúc, giá mới sẽ bị chặn do khoảng hiệu lực chồng nhau.</p>
        <form className="bill-form" onSubmit={event => submit(event, () => billingApi.createRate(propertyId, { ...rate, unitPrice: Number(rate.unitPrice), effectiveTo: rate.effectiveTo || null }), () => setRate(initialRate), 'Đã lưu đơn giá.')}>
          <label>Loại<select value={rate.type} onChange={event => setRate({ ...rate, type: event.target.value, unit: event.target.value === 'Electricity' ? 'kWh' : 'm³' })}><option value="Electricity">Điện</option><option value="Water">Nước</option></select></label>
          <label>Đơn giá<input type="number" min="0" step="0.01" required value={rate.unitPrice} onChange={event => setRate({ ...rate, unitPrice: event.target.value })} /></label>
          <label>Đơn vị<input required maxLength="50" value={rate.unit} onChange={event => setRate({ ...rate, unit: event.target.value })} /></label>
          <label>Từ ngày<input type="date" required value={rate.effectiveFrom} onChange={event => setRate({ ...rate, effectiveFrom: event.target.value })} /></label>
          <label>Đến ngày (để trống nếu chưa có)<input type="date" min={rate.effectiveFrom} value={rate.effectiveTo} onChange={event => setRate({ ...rate, effectiveTo: event.target.value })} /></label>
          <button className="bill-button" disabled={busy}>Lưu đơn giá</button>
        </form>
        <ul className="bill-list">{rates.map(item => <li key={item.id}><strong>{typeName(item.type)} · {money(item.unitPrice)}/{item.unit}</strong><small>{item.effectiveFrom} → {item.effectiveTo || 'Chưa kết thúc'}</small></li>)}</ul>
      </section>
      <section className="bill-card">
        <h2>Chọn phòng</h2>
        <label className="bill-field">Phòng<select value={roomId} onChange={event => setRoomId(event.target.value)}><option value="">Chọn phòng</option>{rooms.map(room => <option key={room.id} value={room.id}>{room.roomNumber || room.number || room.id}</option>)}</select></label>
        {roomId ? <>
          <h3>Chỉ số điện nước</h3>
          <form className="bill-form" onSubmit={event => submit(event, () => billingApi.createReading(propertyId, roomId, { ...reading, year: Number(reading.year), month: Number(reading.month), previousValue: Number(reading.previousValue), currentValue: Number(reading.currentValue) }), () => setReading(initialReading), 'Đã lưu chỉ số.')}>
            <label>Loại<select value={reading.type} onChange={event => setReading({ ...reading, type: event.target.value })}><option value="Electricity">Điện</option><option value="Water">Nước</option></select></label>
            <label>Năm<input type="number" min="2000" max="2100" required value={reading.year} onChange={event => setReading({ ...reading, year: event.target.value })} /></label>
            <label>Tháng<input type="number" min="1" max="12" required value={reading.month} onChange={event => setReading({ ...reading, month: event.target.value })} /></label>
            <label>Chỉ số đầu kỳ<input type="number" min="0" step="0.01" required value={reading.previousValue} onChange={event => setReading({ ...reading, previousValue: event.target.value })} /></label>
            <label>Chỉ số cuối kỳ<input type="number" min={reading.previousValue || 0} step="0.01" required value={reading.currentValue} onChange={event => setReading({ ...reading, currentValue: event.target.value })} /></label>
            <label className="bill-field--wide">Ghi chú<input maxLength="500" value={reading.note} onChange={event => setReading({ ...reading, note: event.target.value })} /></label>
            <button className="bill-button" disabled={busy}>Lưu chỉ số</button>
          </form>
          <ul className="bill-list">{readings.map(item => <li key={item.id}><strong>{typeName(item.type)} · {item.month}/{item.year}: {item.used} đơn vị</strong><small>{item.previousValue} → {item.currentValue}</small></li>)}</ul>
        </> : <p className="bill-empty">Chọn phòng để nhập chỉ số và gán dịch vụ.</p>}
      </section>
    </div>
    {roomId && <section className="bill-card">
      <h2>Dịch vụ của phòng</h2><p>Danh mục dịch vụ do quản trị viên tạo. Giá riêng để trống sẽ dùng giá mặc định.</p>
      <form className="bill-form bill-form--inline" onSubmit={event => submit(event, () => billingApi.assignService(propertyId, roomId, { serviceId: assignment.serviceId, customPrice: assignment.customPrice === '' ? null : Number(assignment.customPrice), startDate: assignment.startDate, endDate: assignment.endDate || null }), () => setAssignment(initialAssignment), 'Đã gán dịch vụ cho phòng.')}>
        <label>Dịch vụ<select required value={assignment.serviceId} onChange={event => setAssignment({ ...assignment, serviceId: event.target.value })}><option value="">Chọn dịch vụ</option>{services.filter(item => item.isActive).map(item => <option key={item.id} value={item.id}>{item.name} · {item.defaultPrice == null ? 'Chưa có giá' : money(item.defaultPrice)}</option>)}</select></label>
        <label>Giá riêng<input type="number" min="0" step="0.01" value={assignment.customPrice} onChange={event => setAssignment({ ...assignment, customPrice: event.target.value })} /></label>
        <label>Từ ngày<input type="date" required value={assignment.startDate} onChange={event => setAssignment({ ...assignment, startDate: event.target.value })} /></label>
        <label>Đến ngày<input type="date" min={assignment.startDate} value={assignment.endDate} onChange={event => setAssignment({ ...assignment, endDate: event.target.value })} /></label>
        <button className="bill-button" disabled={busy}>Gán dịch vụ</button>
      </form>
      <ul className="bill-list">{assignments.map(item => <li key={item.id}><strong>{item.serviceName} · {item.unitPrice == null ? 'Chưa có giá' : money(item.unitPrice)}</strong><small>{item.startDate} → {item.endDate || 'Chưa kết thúc'}</small></li>)}</ul>
    </section>}
    {loading && <p role="status">Đang tải dữ liệu...</p>}
    {error && <p className="bill-error" role="alert">{error}</p>}
    {success && <p className="bill-success" role="status">{success}</p>}
  </div>
}
