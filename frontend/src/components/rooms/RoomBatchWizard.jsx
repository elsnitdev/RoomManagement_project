import { useEffect, useMemo, useState } from 'react'
import { ArrowLeft, ArrowRight, Building2, Check, Edit3, Layers3, Plus, RefreshCw, Save, Trash2, X } from 'lucide-react'
import { createRoomBatch, createRoomRow, listRoomRows, previewRoomBatch } from '../../services/api'
import { IconButton, moneyText, messageOf } from '../property/ManagementUi'

const blankValues = () => ({ roomName: '', area: '', basePrice: '', maxOccupants: '2', description: '' })
const newGroup = floor => ({ clientKey: crypto.randomUUID(), floor: String(floor), count: '4' })
const keyOf = item => `${item.clientKey}:${item.ordinal}`
const toInput = value => ({ roomName: value.roomName || '', area: value.area ?? '', basePrice: value.basePrice ?? '', maxOccupants: value.maxOccupants ?? '', description: value.description || '' })
const toValues = value => ({ roomName: value.roomName.trim() || null, area: value.area === '' ? null : Number(value.area), basePrice: Number(value.basePrice), maxOccupants: Number(value.maxOccupants), description: value.description.trim() || null })
const steps = ['Chọn dãy', 'Thông tin chung', 'Chỉnh từng phòng', 'Rà soát']

function StepButton({ icon: Icon, children, tone = 'secondary', ...props }) {
  return <button type="button" className={`pm-batch-button pm-batch-button--${tone}`} {...props}><Icon size={17} aria-hidden="true" />{children}</button>
}

function RoomValuesFields({ values, onChange, prefix = '' }) {
  const field = (name, value) => onChange({ ...values, [name]: value })
  return <div className="pm-batch-fields">
    <label className="field"><span>Tên phòng</span><input maxLength={100} value={values.roomName} onChange={event => field('roomName', event.target.value)} placeholder="Có thể để trống" /></label>
    <label className="field"><span>Diện tích (m²)</span><input type="number" min="0.01" step="0.01" value={values.area} onChange={event => field('area', event.target.value)} placeholder="Tùy chọn" /></label>
    <label className="field"><span>Giá thuê (VND) *</span><input type="number" min="0" step="0.01" required value={values.basePrice} onChange={event => field('basePrice', event.target.value)} /></label>
    <label className="field"><span>Số người tối đa *</span><input type="number" min="1" step="1" required value={values.maxOccupants} onChange={event => field('maxOccupants', event.target.value)} /></label>
    <label className="field pm-batch-wide"><span>Mô tả {prefix}</span><textarea rows="2" maxLength={1000} value={values.description} onChange={event => field('description', event.target.value)} placeholder="Đặc điểm hoặc ghi chú" /></label>
  </div>
}

export default function RoomBatchWizard({ propertyId, onCancel, onSaved, notify }) {
  const [step, setStep] = useState(1)
  const [rows, setRows] = useState([])
  const [selectedRow, setSelectedRow] = useState(null) // 'floor' hoặc ID dãy
  const [creatingRow, setCreatingRow] = useState(false)
  const [rowCode, setRowCode] = useState('')
  const [rowName, setRowName] = useState('')
  const [groups, setGroups] = useState(() => [newGroup(1)])
  const [common, setCommon] = useState(blankValues)
  const [overrides, setOverrides] = useState({})
  const [preview, setPreview] = useState(null)
  const [editing, setEditing] = useState(null)
  const [editValues, setEditValues] = useState(blankValues)
  const [page, setPage] = useState(1)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())

  useEffect(() => {
    let active = true
    listRoomRows(propertyId).then(data => { if (active) setRows(data) })
      .catch(cause => { if (active) setError(messageOf(cause)) })
    return () => { active = false }
  }, [propertyId])

  const selectedRowData = rows.find(row => row.id === selectedRow)
  const locationName = selectedRowData ? `${selectedRowData.code} · ${selectedRowData.name}` : 'Theo tầng, không dùng dãy'
  const total = useMemo(() => groups.reduce((sum, group) => sum + (Number(group.count) || 0), 0), [groups])
  const pages = Math.max(1, Math.ceil((preview?.rooms.length || 0) / 12))
  const visibleRooms = preview?.rooms.slice((page - 1) * 12, page * 12) || []
  const specialCount = Object.keys(overrides).length

  function chooseRow(value) {
    if (value === selectedRow) return
    setSelectedRow(value)
    setPreview(null)
    setOverrides({})
    setEditing(null)
    setError('')
  }

  function updateGroup(index, patch) {
    const target = groups[index]
    if ('count' in patch) setOverrides(current => Object.fromEntries(Object.entries(current).filter(([, item]) => item.groupKey !== target.clientKey || item.ordinal <= Number(patch.count))))
    setGroups(current => current.map((group, position) => position === index ? { ...group, ...patch } : group))
    setPreview(null)
  }

  function removeGroup(key) {
    setGroups(current => current.filter(group => group.clientKey !== key))
    setOverrides(current => Object.fromEntries(Object.entries(current).filter(([, item]) => item.groupKey !== key)))
    setPreview(null)
  }

  function buildPlan(chosenOverrides = overrides) {
    return {
      groups: groups.map(group => ({ clientKey: group.clientKey, rowId: selectedRow === 'floor' ? null : selectedRow,
        floor: group.floor === '' ? null : Number(group.floor), count: Number(group.count), defaults: toValues(common) })),
      overrides: Object.values(chosenOverrides).map(item => ({ groupKey: item.groupKey, ordinal: item.ordinal, values: toValues(item.values) })),
    }
  }

  async function loadPreview(chosenOverrides = overrides, resetPage = false) {
    if (common.basePrice === '' || common.maxOccupants === '') { setError('Vui lòng nhập giá thuê và số người tối đa.'); return null }
    setBusy(true)
    setError('')
    try {
      const result = await previewRoomBatch(propertyId, buildPlan(chosenOverrides))
      setPreview(result)
      setStep(3)
      if (resetPage) setPage(1)
      return result
    } catch (cause) { setError(messageOf(cause)); return null }
    finally { setBusy(false) }
  }

  async function addRow(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const row = await createRoomRow(propertyId, { code: rowCode.trim(), name: rowName.trim() })
      setRows(current => [...current, row].sort((a, b) => a.code.localeCompare(b.code)))
      chooseRow(row.id)
      setCreatingRow(false)
      setRowCode('')
      setRowName('')
      notify(`Đã tạo dãy ${row.code}.`)
    } catch (cause) { setError(messageOf(cause)) }
    finally { setBusy(false) }
  }

  function openEditor(item) { setEditing(item); setEditValues(toInput(item.values)) }

  async function saveSpecial(event) {
    event.preventDefault()
    const next = { ...overrides, [keyOf(editing)]: { groupKey: editing.clientKey, ordinal: editing.ordinal, values: editValues } }
    if (await loadPreview(next)) { setOverrides(next); setEditing(null) }
  }

  async function removeSpecial(item) {
    const next = { ...overrides }
    delete next[keyOf(item)]
    if (await loadPreview(next)) { setOverrides(next); setEditing(null) }
  }

  async function submit() {
    if (!preview || busy) return
    setBusy(true)
    setError('')
    try {
      const result = await createRoomBatch(propertyId, { ...buildPlan(), previewFingerprint: preview.previewFingerprint, idempotencyKey })
      setIdempotencyKey(crypto.randomUUID())
      onSaved(result.rooms)
    } catch (cause) {
      if (cause.status === 409 && cause.details?.code === 'ROOM_PREVIEW_CHANGED') {
        setPreview(null)
        setStep(2)
        setError('Số phòng đã thay đổi. Hãy xem trước lại rồi kiểm tra các phòng đặc biệt.')
      } else setError(messageOf(cause))
    } finally { setBusy(false) }
  }

  return <section className="pm-form-card content-card pm-batch-wizard" aria-labelledby="batch-title">
    <header className="pm-batch-header">
      <div><span className="pm-eyebrow">TẠO NHIỀU PHÒNG</span><h2 id="batch-title">{steps[step - 1]}</h2><p>Mỗi bước chỉ hiển thị thông tin cần xử lý.</p></div>
      <IconButton label="Đóng trang tạo phòng" icon={X} onClick={onCancel} disabled={busy} />
    </header>
    <ol className="pm-batch-stepper" aria-label="Các bước tạo phòng">{steps.map((label, index) => <li key={label} className={step === index + 1 ? 'is-current' : step > index + 1 ? 'is-done' : ''} aria-current={step === index + 1 ? 'step' : undefined}><span>{step > index + 1 ? <Check size={15} aria-hidden="true" /> : index + 1}</span><strong>{label}</strong></li>)}</ol>

    {step === 1 && <div className="pm-batch-stage pm-batch-location">
      <div className="pm-batch-stage-heading"><div><span className="pm-eyebrow">BƯỚC 1 / 4</span><h3>Phòng thuộc dãy nào?</h3><p>Chọn một dãy cho lượt tạo này. Nếu khu trọ không chia dãy, chọn tạo theo tầng.</p></div></div>
      <div className="pm-batch-location-grid">
        <button type="button" className={`pm-batch-location-card ${selectedRow === 'floor' ? 'is-selected' : ''}`} aria-pressed={selectedRow === 'floor'} onClick={() => chooseRow('floor')}><Layers3 size={22} aria-hidden="true" /><strong>Không dùng dãy</strong><span>Đánh số theo tầng: 101, 102…</span></button>
        {rows.filter(row => row.isActive).map(row => <button type="button" key={row.id} className={`pm-batch-location-card ${selectedRow === row.id ? 'is-selected' : ''}`} aria-pressed={selectedRow === row.id} onClick={() => chooseRow(row.id)}><Building2 size={22} aria-hidden="true" /><strong>Dãy {row.code}</strong><span>{row.name}</span></button>)}
        <button type="button" className="pm-batch-location-card pm-batch-location-card--new" onClick={() => setCreatingRow(value => !value)} aria-expanded={creatingRow}><Plus size={22} aria-hidden="true" /><strong>Tạo dãy mới</strong><span>Chưa có dãy phù hợp</span></button>
      </div>
      {creatingRow && <form className="pm-batch-create-row" onSubmit={addRow}><label className="field"><span>Mã dãy *</span><input autoFocus value={rowCode} maxLength={20} required placeholder="A" onChange={event => setRowCode(event.target.value.toUpperCase())} /></label><label className="field"><span>Tên dãy *</span><input value={rowName} maxLength={100} required placeholder="Dãy A" onChange={event => setRowName(event.target.value)} /></label><StepButton icon={Plus} tone="primary" type="submit" disabled={busy}>Tạo và chọn dãy</StepButton></form>}
      <div className="pm-batch-footer"><span>{selectedRow ? `Đã chọn: ${locationName}` : 'Hãy chọn dãy hoặc phương án không dùng dãy.'}</span><StepButton icon={ArrowRight} tone="primary" disabled={!selectedRow || busy} onClick={() => { setError(''); setStep(2) }}>Tiếp tục</StepButton></div>
    </div>}

    {step === 2 && <div className="pm-batch-stage">
      <div className="pm-batch-stage-heading"><div><span className="pm-eyebrow">BƯỚC 2 / 4 · {locationName}</span><h3>Thông tin dùng chung</h3><p>Giá trị này áp dụng cho mọi phòng. Bạn có thể sửa riêng từng phòng ở bước sau.</p></div><strong className="pm-batch-count">{total} phòng</strong></div>
      <div className="pm-batch-config-layout"><div className="pm-batch-config-card"><h4>Phân bổ theo tầng</h4><p>{selectedRow === 'floor' ? 'Mỗi nhóm cần chọn một tầng để cấp số 101, 201…' : 'Để trống tầng nếu dãy không chia tầng.'}</p><div className="pm-batch-group-list">{groups.map((group, index) => <div className="pm-batch-group-line" key={group.clientKey}><span className="pm-batch-group-index">{index + 1}</span><label className="field"><span>Tầng</span><input type="number" min="1" step="1" value={group.floor} onChange={event => updateGroup(index, { floor: event.target.value })} placeholder="Không có" /></label><label className="field"><span>Số phòng</span><input type="number" min="1" max="99" step="1" value={group.count} onChange={event => updateGroup(index, { count: event.target.value })} /></label><IconButton label={`Xóa nhóm ${index + 1}`} icon={Trash2} disabled={busy || groups.length === 1} onClick={() => removeGroup(group.clientKey)} /></div>)}</div><StepButton icon={Plus} onClick={() => { setGroups(current => [...current, newGroup(current.length + 1)]); setPreview(null) }} disabled={busy}>Thêm tầng</StepButton></div>
        <div className="pm-batch-config-card"><h4>Thông tin phòng mặc định</h4><RoomValuesFields values={common} onChange={value => { setCommon(value); setPreview(null) }} prefix="chung" /></div></div>
      <div className="pm-batch-footer"><StepButton icon={ArrowLeft} onClick={() => setStep(1)} disabled={busy}>Chọn lại dãy</StepButton><div><span>{groups.length} nhóm · {total}/200 phòng</span><StepButton icon={ArrowRight} tone="primary" onClick={() => loadPreview(overrides, true)} disabled={busy || total < 1 || total > 200}>Xem danh sách phòng</StepButton></div></div>
    </div>}

    {step === 3 && preview && <div className="pm-batch-stage">
      <div className="pm-batch-stage-heading"><div><span className="pm-eyebrow">BƯỚC 3 / 4 · {locationName}</span><h3>Danh sách phòng dự kiến</h3><p>Chọn phòng cần sửa. Các phòng khác giữ thông tin chung.</p></div><strong className="pm-batch-count">{preview.rooms.length} phòng · {specialCount} riêng</strong></div>
      <div className={`pm-batch-preview-layout ${editing ? 'has-editor' : ''}`}><div className="pm-batch-list-panel"><div className="pm-batch-table-wrap"><table className="pm-batch-table"><thead><tr><th>Mã phòng</th><th>Vị trí</th><th>Diện tích</th><th>Giá thuê</th><th>Sức chứa</th><th>Thao tác</th></tr></thead><tbody>{visibleRooms.map(item => <tr key={keyOf(item)} className={overrides[keyOf(item)] ? 'pm-batch-special' : ''}><td><strong>{item.roomNumber}</strong>{overrides[keyOf(item)] && <small>Phòng riêng</small>}</td><td>{item.floor == null ? 'Không chia tầng' : `Tầng ${item.floor}`}</td><td>{item.values.area == null ? '—' : `${item.values.area} m²`}</td><td>{moneyText(item.values.basePrice)}</td><td>{item.values.maxOccupants}</td><td><IconButton label={`Chỉnh phòng ${item.roomNumber}`} icon={Edit3} onClick={() => openEditor(item)} disabled={busy} />{overrides[keyOf(item)] && <IconButton label={`Bỏ chỉnh riêng phòng ${item.roomNumber}`} icon={Trash2} onClick={() => removeSpecial(item)} disabled={busy} />}</td></tr>)}</tbody></table></div><div className="pm-batch-list-footer"><span>Trang {page}/{pages}</span><div><IconButton label="Trang trước" icon={ArrowLeft} onClick={() => setPage(value => value - 1)} disabled={page <= 1 || busy} /><IconButton label="Trang sau" icon={ArrowRight} onClick={() => setPage(value => value + 1)} disabled={page >= pages || busy} /></div></div></div>
        <aside className="pm-batch-edit-panel" aria-label={editing ? `Chỉnh phòng ${editing.roomNumber}` : 'Hướng dẫn chỉnh phòng'}>{editing ? <form onSubmit={saveSpecial}><div className="pm-batch-edit-heading"><div><span className="pm-eyebrow">PHÒNG ĐẶC BIỆT</span><h4>{editing.roomNumber}</h4></div><IconButton label="Đóng biểu mẫu chỉnh phòng" icon={X} onClick={() => setEditing(null)} disabled={busy} /></div><RoomValuesFields values={editValues} onChange={setEditValues} /><StepButton icon={Save} tone="primary" type="submit" disabled={busy}>Lưu thay đổi phòng</StepButton></form> : <div className="pm-batch-edit-empty"><Edit3 size={27} aria-hidden="true" /><strong>Chỉnh riêng một phòng</strong><p>Chọn biểu tượng chỉnh sửa ở một dòng. Thay đổi sẽ được xem trước lại trước khi xác nhận.</p></div>}</aside></div>
      <div className="pm-batch-footer"><StepButton icon={ArrowLeft} onClick={() => setStep(2)} disabled={busy || Boolean(editing)}>Thông tin chung</StepButton><div><StepButton icon={RefreshCw} onClick={() => loadPreview()} disabled={busy || Boolean(editing)}>Làm mới số phòng</StepButton><StepButton icon={ArrowRight} tone="primary" onClick={() => setStep(4)} disabled={busy || Boolean(editing)}>Rà soát toàn bộ</StepButton></div></div>
    </div>}

    {step === 4 && preview && <div className="pm-batch-stage">
      <div className="pm-batch-stage-heading"><div><span className="pm-eyebrow">BƯỚC 4 / 4 · KIỂM TRA TRƯỚC KHI LƯU</span><h3>Rà soát toàn bộ phòng</h3><p>Thông tin dưới đây là giá trị cuối cùng sẽ được lưu cho từng phòng.</p></div><strong className="pm-batch-count">{preview.rooms.length} phòng · {specialCount} riêng</strong></div>
      <div className="pm-batch-review-common"><div><span>Vị trí</span><strong>{locationName}</strong></div><div><span>Phân bổ</span><strong>{groups.map(group => `${group.floor ? `Tầng ${group.floor}` : 'Không tầng'}: ${group.count} phòng`).join(' · ')}</strong></div><div><span>Diện tích chung</span><strong>{common.area ? `${common.area} m²` : 'Chưa đặt'}</strong></div><div><span>Giá thuê chung</span><strong>{moneyText(Number(common.basePrice))}</strong></div><div><span>Sức chứa chung</span><strong>{common.maxOccupants} người</strong></div><div><span>Tên chung</span><strong>{common.roomName || 'Không có'}</strong></div><div><span>Mô tả chung</span><strong>{common.description || 'Không có'}</strong></div></div>
      <div className="pm-batch-review-table-wrap"><table className="pm-batch-table pm-batch-review-table"><thead><tr><th>Mã phòng</th><th>Dãy / tầng</th><th>Tên phòng</th><th>Diện tích</th><th>Giá thuê</th><th>Sức chứa</th><th>Mô tả</th><th>Trạng thái</th></tr></thead><tbody>{preview.rooms.map(item => <tr key={keyOf(item)} className={overrides[keyOf(item)] ? 'pm-batch-special' : ''}><td><strong>{item.roomNumber}</strong>{overrides[keyOf(item)] && <small>Đã chỉnh riêng</small>}</td><td>{selectedRowData ? `Dãy ${selectedRowData.code}` : 'Không dãy'} · {item.floor == null ? 'Không tầng' : `Tầng ${item.floor}`}</td><td>{item.values.roomName || '—'}</td><td>{item.values.area == null ? '—' : `${item.values.area} m²`}</td><td>{moneyText(item.values.basePrice)}</td><td>{item.values.maxOccupants} người</td><td>{item.values.description || '—'}</td><td>Còn trống</td></tr>)}</tbody></table></div>
      <div className="pm-batch-footer"><StepButton icon={ArrowLeft} onClick={() => setStep(3)} disabled={busy}>Chỉnh phòng</StepButton><StepButton icon={Check} tone="primary" onClick={submit} disabled={busy}>Xác nhận tạo {preview.rooms.length} phòng</StepButton></div>
    </div>}
    {error && <p className="form-error pm-batch-error" role="alert">{error}</p>}
    {busy && <p className="pm-batch-loading" role="status">Đang xử lý...</p>}
  </section>
}

