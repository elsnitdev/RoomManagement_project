import { useState } from 'react'
import { Save, X } from 'lucide-react'
import { createRoom, updateRoom } from '../../services/api'
import { IconButton, Status, messageOf } from '../property/ManagementUi'

const emptyRoom = {
  roomNumber: '', roomName: '', floor: '', area: '', basePrice: '',
  maxOccupants: '1', description: '', status: 'Available',
}

export default function RoomForm({ propertyId, initial, onCancel, onSaved }) {
  const [draft, setDraft] = useState(initial ? {
    roomNumber: initial.roomNumber,
    roomName: initial.roomName || '',
    floor: initial.floor ?? '',
    area: initial.area ?? '',
    basePrice: initial.basePrice,
    maxOccupants: initial.maxOccupants,
    description: initial.description || '',
    status: initial.status === 'Occupied' ? 'Available' : initial.status,
  } : emptyRoom)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const editing = Boolean(initial)

  async function submit(event) {
    event.preventDefault()
    if (!draft.roomNumber.trim()) {
      setError('Vui lòng nhập mã phòng.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const payload = {
        roomNumber: draft.roomNumber.trim(),
        roomName: draft.roomName.trim() || null,
        floor: draft.floor === '' ? null : Number(draft.floor),
        area: draft.area === '' ? null : Number(draft.area),
        basePrice: Number(draft.basePrice),
        maxOccupants: Number(draft.maxOccupants),
        description: draft.description.trim() || null,
      }
      const saved = editing
        ? await updateRoom(propertyId, initial.id, { ...payload, status: draft.status, rowVersion: initial.rowVersion })
        : await createRoom(propertyId, payload)
      onSaved(saved)
    } catch (cause) {
      setError(messageOf(cause))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="pm-form-card content-card" aria-labelledby="room-form-title">
      <div className="pm-section-heading">
        <div><span className="pm-eyebrow">THÔNG TIN PHÒNG</span><h2 id="room-form-title">{editing ? `Chỉnh sửa phòng ${initial.roomNumber}` : 'Tạo phòng mới'}</h2></div>
        <IconButton label="Đóng biểu mẫu phòng" icon={X} onClick={onCancel} disabled={busy} />
      </div>
      <form className="pm-form-grid" onSubmit={submit}>
        <label className="field"><span>Mã phòng *</span><input autoFocus required maxLength={50} value={draft.roomNumber} onChange={event => setDraft({ ...draft, roomNumber: event.target.value })} placeholder="Ví dụ: P101" /></label>
        <label className="field"><span>Tên phòng</span><input maxLength={100} value={draft.roomName} onChange={event => setDraft({ ...draft, roomName: event.target.value })} placeholder="Ví dụ: Phòng góc tầng 1" /></label>
        <label className="field"><span>Tầng</span><input type="number" step="1" value={draft.floor} onChange={event => setDraft({ ...draft, floor: event.target.value })} /></label>
        <label className="field"><span>Diện tích (m²)</span><input type="number" min="0.01" step="0.01" value={draft.area} onChange={event => setDraft({ ...draft, area: event.target.value })} /></label>
        <label className="field"><span>Giá phòng (VND/tháng) *</span><input type="number" min="0" step="0.01" required value={draft.basePrice} onChange={event => setDraft({ ...draft, basePrice: event.target.value })} /></label>
        <label className="field"><span>Số người tối đa *</span><input type="number" min="1" step="1" required value={draft.maxOccupants} onChange={event => setDraft({ ...draft, maxOccupants: event.target.value })} /></label>
        {editing && (initial.status === 'Occupied'
          ? <div className="field"><span>Trạng thái</span><div className="pm-readonly-status"><Status roomStatus="Occupied" /></div><small>Phòng đang có hợp đồng; hệ thống quản lý trạng thái này.</small></div>
          : <label className="field"><span>Trạng thái vận hành</span><select value={draft.status} onChange={event => setDraft({ ...draft, status: event.target.value })}><option value="Available">Còn trống</option><option value="Maintenance">Bảo trì</option><option value="Inactive">Ngừng sử dụng</option></select></label>)}
        <label className="field pm-span"><span>Mô tả</span><textarea rows="3" maxLength={1000} value={draft.description} onChange={event => setDraft({ ...draft, description: event.target.value })} placeholder="Ghi chú về tiện nghi hoặc đặc điểm phòng" /></label>
        {error && <p className="form-error pm-span" role="alert">{error}</p>}
        <div className="pm-form-actions pm-span">
          <IconButton label={editing ? 'Hủy chỉnh sửa phòng' : 'Hủy tạo phòng'} icon={X} onClick={onCancel} disabled={busy} />
          <IconButton type="submit" label={busy ? 'Đang lưu phòng' : editing ? 'Lưu thay đổi phòng' : 'Tạo phòng'} icon={Save} tone="primary" disabled={busy} />
        </div>
      </form>
    </section>
  )
}
