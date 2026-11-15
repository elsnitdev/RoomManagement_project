import { useEffect, useMemo, useState } from 'react'
import { Building2, FileText, Plus, Trash2 } from 'lucide-react'
import { billingApi } from '../../services/api/billingApi'
import { listRoomRows } from '../../services/api/RoomRowApi'
import { useDialogClose } from './InvoiceActionDialog'

const money = value => new Intl.NumberFormat('vi-VN').format(Number(value || 0)) + ' ₫'
const today = () => new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10)
const newItem = () => ({ key: crypto.randomUUID(), description: '', quantity: '1', unitPrice: '' })
const ELIGIBLE_STATUSES = ['Active', 'PendingMoveOut', 'PendingSettlement']

export default function OneOffInvoiceForm({
  propertyId,
  properties: initialProperties = [],
  rooms: initialRooms = [],
  contracts: initialContracts = [],
  onCreated,
  onCancel,
}) {
  const dialogClose = useDialogClose()
  const handleCancel = onCancel || dialogClose

  const [properties, setProperties] = useState(initialProperties)
  const [selectedPropertyId, setSelectedPropertyId] = useState(propertyId || initialProperties[0]?.id || '')
  const [rooms, setRooms] = useState(selectedPropertyId === propertyId ? initialRooms : [])
  const [roomRows, setRoomRows] = useState([])
  const [contracts, setContracts] = useState(selectedPropertyId === propertyId ? initialContracts : [])
  const [loadingScope, setLoadingScope] = useState(false)

  const [selectedGroup, setSelectedGroup] = useState('')
  const [selectedRoomId, setSelectedRoomId] = useState('')
  const [contractId, setContractId] = useState('')

  const [type, setType] = useState('Other')
  const [dueDate, setDueDate] = useState(today)
  const [note, setNote] = useState('')
  const [items, setItems] = useState([newItem()])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  // Load properties list if not provided
  useEffect(() => {
    if (initialProperties.length) {
      setProperties(initialProperties)
      return
    }
    const controller = new AbortController()
    billingApi.properties(controller.signal)
      .then(res => {
        const list = res.data || []
        setProperties(list)
        setSelectedPropertyId(cur => cur || list[0]?.id || '')
      })
      .catch(() => {})
    return () => controller.abort()
  }, [initialProperties])

  // Load rooms, room rows, and contracts whenever selectedPropertyId changes
  useEffect(() => {
    if (!selectedPropertyId) {
      setRooms([])
      setRoomRows([])
      setContracts([])
      return
    }
    const controller = new AbortController()
    const isInitialProp = selectedPropertyId === propertyId && initialRooms.length > 0
    if (isInitialProp) {
      setRooms(initialRooms)
      setContracts(initialContracts)
    } else {
      setLoadingScope(true)
    }

    Promise.all([
      listRoomRows(selectedPropertyId).catch(() => []),
      isInitialProp ? Promise.resolve({ data: initialRooms }) : billingApi.rooms(selectedPropertyId, controller.signal),
      isInitialProp ? Promise.resolve({ data: initialContracts }) : billingApi.contracts(selectedPropertyId, controller.signal),
    ])
      .then(([rowsData, roomsRes, contractsRes]) => {
        if (controller.signal.aborted) return
        setRoomRows(Array.isArray(rowsData) ? rowsData : [])
        setRooms(roomsRes?.data || [])
        setContracts(contractsRes?.data || [])
      })
      .catch(() => {})
      .finally(() => {
        if (!controller.signal.aborted) setLoadingScope(false)
      })

    return () => controller.abort()
  }, [selectedPropertyId, propertyId, initialRooms, initialContracts])

  const eligibleContracts = useMemo(
    () => contracts.filter(item => ELIGIBLE_STATUSES.includes(item.status)),
    [contracts],
  )

  const contractsByRoom = useMemo(() => {
    const map = new Map()
    for (const c of eligibleContracts) {
      const list = map.get(c.roomId) || []
      list.push(c)
      map.set(c.roomId, list)
    }
    return map
  }, [eligibleContracts])

  // Build Dãy / Tầng options from roomRows and rooms.floor
  const groupOptions = useMemo(() => {
    const rowOptions = []
    const seenRowIds = new Set()
    for (const row of roomRows) {
      if (rooms.some(r => r.rowId === row.id)) {
        seenRowIds.add(row.id)
        rowOptions.push({ value: `row:${row.id}`, label: `Dãy ${row.name || row.code}` })
      }
    }
    for (const r of rooms) {
      if (r.rowId && !seenRowIds.has(r.rowId)) {
        seenRowIds.add(r.rowId)
        rowOptions.push({ value: `row:${r.rowId}`, label: 'Dãy phòng' })
      }
    }
    const floors = [...new Set(rooms.map(r => r.floor).filter(f => f !== null && f !== undefined))].sort((a, b) => a - b)
    const floorOptions = floors.map(f => ({ value: `floor:${f}`, label: `Tầng ${f}` }))
    return { rowOptions, floorOptions }
  }, [roomRows, rooms])

  // Filter rooms by selected Dãy / Tầng
  const filteredRooms = useMemo(() => {
    return rooms.filter(room => {
      if (!selectedGroup) return true
      if (selectedGroup.startsWith('row:')) return room.rowId === selectedGroup.slice(4)
      if (selectedGroup.startsWith('floor:')) return String(room.floor) === selectedGroup.slice(6)
      return true
    })
  }, [rooms, selectedGroup])

  const roomEligibleContracts = useMemo(
    () => (selectedRoomId ? contractsByRoom.get(selectedRoomId) || [] : []),
    [selectedRoomId, contractsByRoom],
  )

  const selectedContract = useMemo(
    () => eligibleContracts.find(c => c.id === contractId) || null,
    [eligibleContracts, contractId],
  )

  const total = items.reduce((sum, item) => sum + Number(item.quantity || 0) * Number(item.unitPrice || 0), 0)

  function handlePropertyChange(nextPropertyId) {
    setSelectedPropertyId(nextPropertyId)
    setSelectedGroup('')
    setSelectedRoomId('')
    setContractId('')
    setError('')
  }

  function handleGroupChange(nextGroup) {
    setSelectedGroup(nextGroup)
    setSelectedRoomId('')
    setContractId('')
    setError('')
  }

  function handleRoomChange(nextRoomId) {
    setSelectedRoomId(nextRoomId)
    const roomContracts = contractsByRoom.get(nextRoomId) || []
    setContractId(roomContracts[0]?.id || '')
    setError('')
  }

  function updateItem(key, field, value) {
    setItems(current => current.map(item => item.key === key ? { ...item, [field]: value } : item))
  }

  async function submit(event) {
    event.preventDefault()
    if (!selectedPropertyId || !selectedRoomId || !contractId) {
      setError('Vui lòng chọn khu trọ, dãy/tầng và phòng có hợp đồng hiệu lực.')
      return
    }
    if (!items.length || items.some(item => !item.description.trim() || Number(item.quantity) <= 0 || Number(item.unitPrice) < 0 || !item.unitPrice) || total <= 0) {
      setError('Vui lòng nhập ít nhất một khoản thu hợp lệ.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const result = await billingApi.createOneOff(selectedPropertyId, {
        contractId,
        type,
        dueDate,
        note: note.trim() || null,
        items: items.map(item => ({
          type: type === 'Damage' ? 'Damage' : 'Other',
          description: item.description.trim(),
          quantity: Number(item.quantity),
          unitPrice: Number(item.unitPrice),
        })),
      })
      onCreated(result)
    } catch (cause) {
      setError(cause.message || 'Không thể tạo hóa đơn riêng.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="bill-oneoff-form" onSubmit={submit}>
      <div className="bill-oneoff-sections">
        {/* Bước 1: Chọn Khu trọ -> Dãy/Tầng -> Phòng */}
        <div className="bill-oneoff-section">
          <div className="bill-oneoff-section__head">
            <h3 className="bill-oneoff-section__title">1. Chọn phòng lập hóa đơn</h3>
            {loadingScope && <span className="bill-oneoff-count">Đang tải…</span>}
          </div>
          <div className="bill-oneoff-target">
            <div className="bill-oneoff-target-grid">
              <label className="bill-oneoff-field">
                <span>Khu trọ *</span>
                <select
                  required
                  value={selectedPropertyId}
                  onChange={event => handlePropertyChange(event.target.value)}
                >
                  <option value="">Chọn khu trọ</option>
                  {properties.map(p => (
                    <option value={p.id} key={p.id}>{p.name}</option>
                  ))}
                </select>
              </label>

              <label className="bill-oneoff-field">
                <span>Dãy / Tầng</span>
                <select
                  value={selectedGroup}
                  disabled={!selectedPropertyId || loadingScope}
                  onChange={event => handleGroupChange(event.target.value)}
                >
                  <option value="">Tất cả dãy / tầng</option>
                  {groupOptions.rowOptions.length > 0 && (
                    <optgroup label="Theo dãy">
                      {groupOptions.rowOptions.map(opt => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </optgroup>
                  )}
                  {groupOptions.floorOptions.length > 0 && (
                    <optgroup label="Theo tầng">
                      {groupOptions.floorOptions.map(opt => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </optgroup>
                  )}
                </select>
              </label>

              <label className="bill-oneoff-field">
                <span>Phòng *</span>
                <select
                  required
                  value={selectedRoomId}
                  disabled={!selectedPropertyId || loadingScope}
                  onChange={event => handleRoomChange(event.target.value)}
                >
                  <option value="">Chọn phòng</option>
                  {filteredRooms.map(room => {
                    const roomContracts = contractsByRoom.get(room.id) || []
                    const hasContract = roomContracts.length > 0
                    return (
                      <option key={room.id} value={room.id} disabled={!hasContract}>
                        Phòng {room.roomNumber}{hasContract ? ` (${roomContracts[0].contractCode})` : ' — Chưa có hợp đồng'}
                      </option>
                    )
                  })}
                </select>
              </label>
            </div>

            {selectedRoomId && roomEligibleContracts.length > 1 && (
              <label className="bill-oneoff-field" style={{ marginTop: '4px' }}>
                <span>Hợp đồng áp dụng *</span>
                <select required value={contractId} onChange={event => setContractId(event.target.value)}>
                  {roomEligibleContracts.map(c => (
                    <option key={c.id} value={c.id}>{c.contractCode}</option>
                  ))}
                </select>
              </label>
            )}

            {selectedContract ? (
              <div className="bill-oneoff-contract-chip">
                <FileText size={15} aria-hidden="true" />
                <span>Hợp đồng áp dụng: <strong>{selectedContract.contractCode}</strong></span>
              </div>
            ) : (
              <div className="bill-oneoff-contract-hint">
                <Building2 size={15} aria-hidden="true" />
                <span>Chọn khu trọ, lọc dãy/tầng (nếu cần) và chọn phòng đang có hợp đồng để nhập khoản thu.</span>
              </div>
            )}
          </div>
        </div>

        {/* Bước 2: Thông tin hóa đơn */}
        <div className={`bill-oneoff-section${!contractId ? ' bill-oneoff-section--locked' : ''}`}>
          <h3 className="bill-oneoff-section__title">2. Thông tin hóa đơn</h3>
          <div className="bill-oneoff-meta">
            <label className="bill-oneoff-field">
              <span>Mục đích *</span>
              <select value={type} disabled={!contractId} onChange={event => setType(event.target.value)}>
                <option value="Other">Thu thêm tiền / khoản khác</option>
                <option value="Damage">Phí sửa chữa, bồi thường</option>
              </select>
            </label>
            <label className="bill-oneoff-field">
              <span>Hạn thanh toán *</span>
              <input
                type="date"
                required
                min={today()}
                disabled={!contractId}
                value={dueDate}
                onChange={event => setDueDate(event.target.value)}
              />
            </label>
            <label className="bill-oneoff-field bill-oneoff-field--wide">
              <span>Ghi chú</span>
              <input
                maxLength={1000}
                disabled={!contractId}
                value={note}
                onChange={event => setNote(event.target.value)}
                placeholder="Thông tin để người thuê hiểu khoản thu"
              />
            </label>
          </div>
        </div>

        {/* Bước 3: Danh sách các khoản thu */}
        <div className={`bill-oneoff-section${!contractId ? ' bill-oneoff-section--locked' : ''}`}>
          <div className="bill-oneoff-section__head">
            <h3 className="bill-oneoff-section__title">3. Danh sách các khoản thu</h3>
            <span className="bill-oneoff-count">{items.length} khoản</span>
          </div>
          <div className="bill-oneoff-items">
            {items.map((item, index) => {
              const lineTotal = Number(item.quantity || 0) * Number(item.unitPrice || 0)
              return (
                <div className="bill-oneoff-item-card" key={item.key}>
                  <div className="bill-oneoff-item-card__head">
                    <span className="bill-oneoff-item-card__index">Khoản {index + 1}</span>
                    <button
                      type="button"
                      className="bill-oneoff-remove"
                      disabled={!contractId || items.length === 1}
                      aria-label={`Bỏ khoản ${index + 1}`}
                      onClick={() => setItems(current => current.filter(row => row.key !== item.key))}
                    >
                      <Trash2 size={15} aria-hidden="true" />
                      <span>Bỏ khoản</span>
                    </button>
                  </div>

                  <label className="bill-oneoff-field bill-oneoff-item-card__desc">
                    <span>Mô tả khoản thu *</span>
                    <input
                      required
                      maxLength={255}
                      disabled={!contractId}
                      value={item.description}
                      onChange={event => updateItem(item.key, 'description', event.target.value)}
                      placeholder="Ví dụ: Sửa khóa cửa phòng"
                    />
                  </label>

                  <div className="bill-oneoff-item-nums">
                    <label className="bill-oneoff-field">
                      <span>Số lượng *</span>
                      <input
                        required
                        type="number"
                        min="0.01"
                        step="0.01"
                        disabled={!contractId}
                        value={item.quantity}
                        onChange={event => updateItem(item.key, 'quantity', event.target.value)}
                      />
                    </label>
                    <label className="bill-oneoff-field">
                      <span>Đơn giá (₫) *</span>
                      <input
                        required
                        type="number"
                        min="0"
                        step="0.01"
                        disabled={!contractId}
                        value={item.unitPrice}
                        onChange={event => updateItem(item.key, 'unitPrice', event.target.value)}
                        placeholder="0"
                      />
                    </label>
                  </div>

                  <div className="bill-oneoff-item-card__subtotal">
                    <span>Thành tiền:</span>
                    <strong>{money(lineTotal)}</strong>
                  </div>
                </div>
              )
            })}
          </div>

          <button
            type="button"
            className="bill-button bill-button--secondary bill-oneoff-add"
            disabled={!contractId}
            onClick={() => setItems(current => [...current, newItem()])}
          >
            <Plus size={15} aria-hidden="true" />
            <span>Thêm khoản thu</span>
          </button>
        </div>

        {error && <p className="bill-error" role="alert">{error}</p>}
      </div>

      {/* Sticky Modal Footer */}
      <footer className="bill-dialog__footer bill-dialog__footer--sticky">
        <div className="bill-dialog__total">
          <span>Tổng dự kiến</span>
          <strong>{money(total)}</strong>
        </div>
        <div className="bill-dialog__footer-actions">
          {handleCancel && (
            <button
              type="button"
              className="bill-button bill-button--secondary"
              disabled={busy}
              onClick={handleCancel}
            >
              Hủy
            </button>
          )}
          <button
            type="submit"
            className="bill-button"
            disabled={busy || !contractId || total <= 0}
          >
            {busy ? 'Đang tạo…' : 'Tạo bản nháp'}
          </button>
        </div>
      </footer>
    </form>
  )
}
