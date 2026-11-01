import { useState } from 'react'
import { Check, Pencil, Plus, Power, PowerOff, X } from 'lucide-react'
import { createRoomRow, updateRoomRow } from '../../services/api'
import { Feedback, IconButton, messageOf } from '../property/ManagementUi'

export default function RoomRowManager({ propertyId, rows, queryError, onRetry, onChanged, notify, disabled = false }) {
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [editing, setEditing] = useState(null)
  const [draftName, setDraftName] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function addRow(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const row = await createRoomRow(propertyId, { code: code.trim(), name: name.trim() })
      setCode('')
      setName('')
      onChanged()
      notify(`Đã tạo dãy ${row.code}.`)
    } catch (cause) { setError(messageOf(cause)) }
    finally { setBusy(false) }
  }

  async function saveRow(row, nextName, isActive) {
    setBusy(true)
    setError('')
    try {
      await updateRoomRow(propertyId, row.id, { name: nextName.trim(), isActive })
      setEditing(null)
      onChanged()
      notify(`Đã cập nhật dãy ${row.code}.`)
    } catch (cause) { setError(messageOf(cause)) }
    finally { setBusy(false) }
  }

  return (
    <section className="content-card pm-room-row-manager" aria-labelledby="room-rows-title">
      <div className="pm-section-heading"><div><span className="pm-eyebrow">CẤU TRÚC KHU TRỌ</span><h2 id="room-rows-title">Quản lý dãy trọ</h2><p>Mã dãy không đổi sau khi tạo. Ngừng dùng dãy sẽ chặn tạo phòng mới trong dãy đó.</p></div></div>
      {queryError ? <Feedback error={queryError} onRetry={onRetry} /> : <div className="pm-room-row-list">
        {rows.length === 0 && <p>Chưa có dãy. Bạn vẫn có thể tạo phòng theo tầng.</p>}
        {rows.map(row => <div className="pm-room-row-item" key={row.id}>
          <strong>{row.code}</strong>
          {editing === row.id
            ? <form onSubmit={event => { event.preventDefault(); saveRow(row, draftName, row.isActive) }}>
              <input aria-label={`Tên dãy ${row.code}`} value={draftName} maxLength={100} required onChange={event => setDraftName(event.target.value)} />
              <IconButton type="submit" label={`Lưu tên dãy ${row.code}`} icon={Check} disabled={busy || disabled} />
              <IconButton label="Hủy sửa tên dãy" icon={X} onClick={() => setEditing(null)} disabled={busy} />
            </form>
            : <><span>{row.name}</span><small>{row.isActive ? 'Đang dùng' : 'Ngừng dùng'}</small><div className="pm-room-row-actions">
              <IconButton label={`Sửa tên dãy ${row.code}`} icon={Pencil} onClick={() => { setEditing(row.id); setDraftName(row.name) }} disabled={busy || disabled} />
              <IconButton label={row.isActive ? `Ngừng dùng dãy ${row.code}` : `Dùng lại dãy ${row.code}`} icon={row.isActive ? PowerOff : Power} onClick={() => saveRow(row, row.name, !row.isActive)} disabled={busy || disabled} />
            </div></>}
        </div>)}
      </div>}
      <form className="pm-batch-row-form" onSubmit={addRow}>
        <label className="field"><span>Mã dãy</span><input value={code} maxLength={20} required placeholder="A" onChange={event => setCode(event.target.value.toUpperCase())} /></label>
        <label className="field"><span>Tên dãy</span><input value={name} maxLength={100} required placeholder="Dãy A" onChange={event => setName(event.target.value)} /></label>
        <IconButton type="submit" label="Tạo dãy trọ" icon={Plus} tone="soft" disabled={busy || disabled} />
      </form>
      {error && <p className="form-error" role="alert">{error}</p>}
    </section>
  )
}
