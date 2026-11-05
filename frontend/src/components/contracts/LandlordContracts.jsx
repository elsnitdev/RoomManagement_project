import { useEffect, useMemo, useRef, useState } from 'react'
import { AlertCircle, CalendarDays, Check, FileText, House, Plus, Search, Users, Wallet, X } from 'lucide-react'
import { billingApi } from '../../services/api/billingApi'
import { contractApi } from '../../services/api/contractApi'
import './contracts.css'

const money = value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(Number(value) || 0)
const date = value => value ? new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T12:00:00`)) : 'Không xác định thời hạn'
const today = () => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())
const errorOf = error => error?.message || 'Không thể thực hiện thao tác. Vui lòng thử lại.'
const statusText = { Draft: 'Bản nháp', Active: 'Đang hiệu lực', Expired: 'Hết hạn', Terminated: 'Đã kết thúc', Cancelled: 'Đã hủy' }
const isUpcoming = c => c.status === 'Active' && c.startDate > today()
const needsSettlement = c => ['Expired', 'Terminated'].includes(c.status) && (c.outstandingAmount > 0 || c.depositHeld > 0 || c.hasDraftInvoices || !c.hasSettlement)
const newForm = () => ({ roomId: '', contractCode: '', startDate: today(), endDate: '', monthlyRent: '', depositAmount: '0', paymentDueDay: '', note: '', email: '' })

function Status({ contract }) { const upcoming = isUpcoming(contract); return <span className={`contract-status contract-status--${upcoming ? 'upcoming' : contract.status?.toLowerCase()}`}>{upcoming ? 'Sắp bắt đầu' : statusText[contract.status] || contract.status}</span> }
function Fact({ label, value }) { return <div><dt>{label}</dt><dd>{value || '—'}</dd></div> }

export default function LandlordContracts({ notify, initialPropertyId, initialContractId }) {
  const [properties, setProperties] = useState([])
  const [propertyId, setPropertyId] = useState(initialPropertyId || '')
  const [rooms, setRooms] = useState([])
  const [contracts, setContracts] = useState([])
  const [detail, setDetail] = useState(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState(newForm)
  const [filter, setFilter] = useState('all')
  const [search, setSearch] = useState('')
  const [extraEmail, setExtraEmail] = useState('')
  const [extraJoinDate, setExtraJoinDate] = useState(today)
  const [confirm, setConfirm] = useState(null)
  const [actionDate, setActionDate] = useState(today)
  const [reason, setReason] = useState('')
  const [revision, setRevision] = useState(0)
  const detailRef = useRef(null)
  const confirmRef = useRef(null)
  const confirmTriggerRef = useRef(null)

  useEffect(() => {
    const controller = new AbortController()
    billingApi.properties(controller.signal).then(response => {
      const items = response.data || []
      setProperties(items)
      setPropertyId(current => current && items.some(item => item.id === current) ? current : items[0]?.id || '')
    }).catch(cause => { if (!controller.signal.aborted) setError(errorOf(cause)) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [revision])

  useEffect(() => {
    if (!propertyId) { setRooms([]); setContracts([]); setDetail(null); return }
    const controller = new AbortController()
    setLoading(true); setError(''); setDetail(null); setConfirm(null); setRooms([]); setContracts([])
    Promise.all([billingApi.rooms(propertyId, controller.signal), contractApi.list(propertyId, controller.signal)])
      .then(async ([roomResponse, contractResponse]) => {
        if (controller.signal.aborted) return
        setRooms(roomResponse.data || []); setContracts(contractResponse.data || [])
        if (initialContractId && propertyId === initialPropertyId) {
          try { setDetail(await contractApi.detail(propertyId, initialContractId, controller.signal)) }
          catch (cause) { if (!controller.signal.aborted) setError(errorOf(cause)) }
        }
      }).catch(cause => { if (!controller.signal.aborted) setError(errorOf(cause)) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [propertyId, initialContractId, initialPropertyId, revision])

  useEffect(() => {
    if (detail) setExtraJoinDate(detail.startDate > today() ? detail.startDate : today())
  }, [detail?.id, detail?.startDate])

  useEffect(() => {
    if (confirm) requestAnimationFrame(() => (confirmRef.current?.querySelector('input, textarea') || confirmRef.current)?.focus())
    else if (confirmTriggerRef.current) requestAnimationFrame(() => {
      if (confirmTriggerRef.current?.isConnected) confirmTriggerRef.current.focus()
      else detailRef.current?.focus()
      confirmTriggerRef.current = null
    })
  }, [confirm])

  const shown = useMemo(() => contracts.filter(c => {
    if (filter === 'ended' ? !['Expired', 'Terminated', 'Cancelled'].includes(c.status) :
      filter === 'upcoming' ? !isUpcoming(c) : filter === 'Active' ? c.status !== 'Active' || isUpcoming(c) :
        filter !== 'all' && c.status !== filter) return false
    const haystack = `${c.contractCode} ${c.roomNumber || rooms.find(r => r.id === c.roomId)?.roomNumber || ''} ${c.tenants?.map(t => t.fullName).join(' ') || ''}`.toLocaleLowerCase('vi-VN')
    return haystack.includes(search.trim().toLocaleLowerCase('vi-VN'))
  }), [contracts, rooms, filter, search])
  const alertContracts = contracts.filter(needsSettlement)
  function openConfirm(next, trigger) { confirmTriggerRef.current = trigger; setConfirm(next) }

  async function reload(id = detail?.id) {
    const [roomResponse, contractResponse] = await Promise.all([billingApi.rooms(propertyId), contractApi.list(propertyId)])
    setRooms(roomResponse.data || []); setContracts(contractResponse.data || [])
    if (id) setDetail(await contractApi.detail(propertyId, id))
  }
  async function open(id) {
    setError(''); setBusy(true); setConfirm(null)
    try { setDetail(await contractApi.detail(propertyId, id)); requestAnimationFrame(() => detailRef.current?.focus()) }
    catch (cause) { setError(errorOf(cause)) }
    finally { setBusy(false) }
  }
  async function run(request, success) {
    setBusy(true); setError('')
    try { await request(); await reload(); setConfirm(null); setReason(''); notify?.(success) }
    catch (cause) { setError(errorOf(cause)) }
    finally { setBusy(false) }
  }
  async function create(event) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      const created = await contractApi.create(propertyId, { roomId: form.roomId, contractCode: form.contractCode.trim(), startDate: form.startDate,
        endDate: form.endDate || null, monthlyRent: Number(form.monthlyRent), depositAmount: Number(form.depositAmount),
        paymentDueDay: form.paymentDueDay ? Number(form.paymentDueDay) : null, note: form.note.trim() || null })
      if (form.email.trim()) {
        try { await contractApi.addUser(propertyId, created.id, { email: form.email.trim(), joinDate: form.startDate }) }
        catch (cause) { setError(`Đã tạo bản nháp, nhưng chưa thêm được người thuê: ${errorOf(cause)}`) }
      }
      await reload(created.id); setForm(newForm()); setShowCreate(false)
      notify?.('Đã tạo hợp đồng nháp.'); requestAnimationFrame(() => detailRef.current?.focus())
    } catch (cause) { setError(errorOf(cause)) }
    finally { setBusy(false) }
  }
  function submitConfirm(event) {
    event.preventDefault()
    if (confirm.type === 'leave') run(() => contractApi.leave(propertyId, detail.id, confirm.tenant.id, actionDate), 'Đã ghi nhận ngày rời.')
    if (confirm.type === 'terminate') run(() => contractApi.terminate(propertyId, detail.id, reason.trim()), 'Đã kết thúc hợp đồng.')
    if (confirm.type === 'cancel') run(() => contractApi.cancel(propertyId, detail.id), 'Đã hủy bản nháp.')
  }

  return <div className="contract-page">
    <header className="contract-header"><div><span className="contract-eyebrow"><FileText size={16} aria-hidden="true" /> QUẢN LÝ CHO THUÊ</span><h1>Hợp đồng thuê</h1><p>Theo dõi thỏa thuận, người ở và các khoản cần xử lý tại một nơi.</p></div><div className="contract-header-actions"><label htmlFor="contract-property">Khu trọ</label><select id="contract-property" value={propertyId} onChange={e => { setRooms([]); setContracts([]); setDetail(null); setError(''); setShowCreate(false); setLoading(true); setPropertyId(e.target.value) }} disabled={busy}><option value="">Chọn khu trọ</option>{properties.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select><button className="contract-button contract-button--primary" type="button" disabled={!propertyId || busy} aria-expanded={showCreate} aria-controls="contract-create-panel" onClick={() => { setShowCreate(value => !value); setError('') }}><Plus size={17} aria-hidden="true" /> Tạo hợp đồng</button></div></header>
    {error && <div className="contract-message contract-message--error" role="alert"><AlertCircle size={18} aria-hidden="true" /><span>{error}</span><button type="button" onClick={() => setRevision(value => value + 1)}>Thử lại</button><button type="button" aria-label="Đóng thông báo" onClick={() => setError('')}><X size={16} /></button></div>}
    {!propertyId && !loading ? <div className="contract-empty"><House size={30} aria-hidden="true" /><h2>Chưa có khu trọ</h2><p>Tạo khu trọ và phòng trước khi thiết lập hợp đồng.</p></div> : <>
      <div className="contract-stats"><div><span><FileText size={20} /></span><small>Tổng hợp đồng</small><strong>{contracts.length}</strong></div><div><span><Check size={20} /></span><small>Đang hiệu lực</small><strong>{contracts.filter(c => c.status === 'Active' && !isUpcoming(c)).length}</strong></div><div><span><Plus size={20} /></span><small>Bản nháp</small><strong>{contracts.filter(c => c.status === 'Draft').length}</strong></div><div><span><Wallet size={20} /></span><small>Cần đối chiếu</small><strong>{alertContracts.length}</strong></div></div>
      {alertContracts.length > 0 && <div className="contract-message contract-message--warning" role="status"><AlertCircle size={19} aria-hidden="true" /><span><strong>{alertContracts.length} hợp đồng cần đối chiếu.</strong> Kiểm tra công nợ, cọc và bản ghi tất toán.</span><button type="button" onClick={() => { setFilter('ended'); open(alertContracts[0].id) }}>Xem ngay</button></div>}
      {showCreate && <section id="contract-create-panel" className="contract-panel contract-create" aria-labelledby="contract-create-title"><div className="contract-panel-head"><div><span className="contract-eyebrow">THIẾT LẬP HỢP ĐỒNG</span><h2 id="contract-create-title">Tạo bản nháp mới</h2><p>Điền thông tin thuê, sau đó kiểm tra và kích hoạt hợp đồng.</p></div><button className="contract-icon-button" type="button" aria-label="Đóng biểu mẫu" onClick={() => setShowCreate(false)}><X size={18} /></button></div><form onSubmit={create} className="contract-create-form"><fieldset><legend>Phòng và thời hạn</legend><div className="contract-fields"><label>Phòng <select required value={form.roomId} onChange={e => setForm({ ...form, roomId: e.target.value })}><option value="">Chọn phòng</option>{rooms.filter(r => r.status === 'Available').map(r => <option key={r.id} value={r.id}>{r.roomNumber}{r.roomName ? ` · ${r.roomName}` : ''}</option>)}</select></label><label>Mã hợp đồng <input required maxLength={50} placeholder="VD: HD-A101-2026" value={form.contractCode} onChange={e => setForm({ ...form, contractCode: e.target.value })} /></label><label>Ngày bắt đầu <input required type="date" value={form.startDate} onChange={e => setForm({ ...form, startDate: e.target.value })} /></label><label>Ngày kết thúc <input type="date" min={form.startDate} value={form.endDate} onChange={e => setForm({ ...form, endDate: e.target.value })} /></label></div></fieldset><fieldset><legend>Tài chính và người thuê</legend><div className="contract-fields"><label>Tiền thuê mỗi tháng <input required type="number" min="0" step="1000" value={form.monthlyRent} onChange={e => setForm({ ...form, monthlyRent: e.target.value })} /></label><label>Tiền cọc <input required type="number" min="0" step="1000" value={form.depositAmount} onChange={e => setForm({ ...form, depositAmount: e.target.value })} /></label><label>Ngày hạn thanh toán <input type="number" min="1" max="31" placeholder="VD: 5" value={form.paymentDueDay} onChange={e => setForm({ ...form, paymentDueDay: e.target.value })} /></label><label>Email người thuê đầu tiên <input type="email" placeholder="nguoi-thue@example.com" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} /><small>Có thể thêm người ở cùng sau khi tạo.</small></label></div></fieldset><label>Ghi chú <textarea rows={3} maxLength={1000} value={form.note} onChange={e => setForm({ ...form, note: e.target.value })} /></label><div className="contract-form-footer"><small>Người thuê xem được hợp đồng sau khi kích hoạt và đến ngày bắt đầu.</small><div><button className="contract-button contract-button--ghost" type="button" onClick={() => setShowCreate(false)}>Đóng</button><button className="contract-button contract-button--primary" type="submit" disabled={busy || !form.roomId}>{busy ? 'Đang lưu…' : 'Lưu bản nháp'}</button></div></div></form></section>}
      <div className="contract-workspace"><section className="contract-panel contract-list-panel" aria-labelledby="contract-list-title"><div className="contract-panel-head"><div><span className="contract-eyebrow">DANH SÁCH</span><h2 id="contract-list-title">Hợp đồng của khu trọ</h2><p>{loading ? 'Đang tải…' : `${shown.length} hợp đồng hiển thị`}</p></div></div><div className="contract-search"><Search size={18} aria-hidden="true" /><input type="search" aria-label="Tìm hợp đồng" placeholder="Tìm mã, phòng, người thuê…" value={search} onChange={e => setSearch(e.target.value)} /></div><div className="contract-filters" role="group" aria-label="Lọc hợp đồng">{[['all', 'Tất cả'], ['Active', 'Hiệu lực'], ['Draft', 'Bản nháp'], ['upcoming', 'Sắp bắt đầu'], ['ended', 'Đã kết thúc']].map(([key, label]) => <button key={key} type="button" aria-pressed={filter === key} className={filter === key ? 'is-active' : ''} onClick={() => setFilter(key)}>{label}</button>)}</div>{loading ? <p className="contract-loading" role="status">Đang tải hợp đồng…</p> : shown.length ? <ul className="contract-list">{shown.map(c => <li key={c.id}><button type="button" aria-pressed={detail?.id === c.id} className={detail?.id === c.id ? 'is-selected' : ''} onClick={() => open(c.id)}><span className="contract-list-top"><strong>{c.contractCode}</strong><Status contract={c} /></span><span className="contract-list-room"><House size={15} aria-hidden="true" /> Phòng {c.roomNumber || rooms.find(r => r.id === c.roomId)?.roomNumber || '—'}</span><span className="contract-list-meta">{c.tenants?.length ? c.tenants.map(t => t.fullName).join(', ') : 'Chưa có người thuê'}</span><span className="contract-list-bottom"><small>{date(c.startDate)} – {date(c.endDate)}</small><strong>{money(c.monthlyRent)}</strong></span>{needsSettlement(c) && <span className="contract-list-warning"><AlertCircle size={13} /> Cần đối chiếu</span>}</button></li>)}</ul> : <div className="contract-empty contract-empty--small"><FileText size={25} aria-hidden="true" /><strong>{contracts.length ? 'Không tìm thấy hợp đồng' : 'Chưa có hợp đồng'}</strong><p>{contracts.length ? 'Thử đổi bộ lọc hoặc từ khóa.' : 'Tạo bản nháp cho phòng đầu tiên.'}</p></div>}</section>
      <section className="contract-panel contract-detail-panel" ref={detailRef} tabIndex={-1} aria-labelledby={detail ? 'contract-detail-title' : undefined}>{!detail ? <div className="contract-empty contract-detail-empty"><FileText size={32} aria-hidden="true" /><h2>Chọn một hợp đồng</h2><p>Thông tin thuê, người ở và các thao tác sẽ hiển thị tại đây.</p></div> : <><div className="contract-detail-head"><div><span className="contract-eyebrow">CHI TIẾT HỢP ĐỒNG</span><h2 id="contract-detail-title">{detail.contractCode}</h2><p><House size={16} aria-hidden="true" /> Phòng {detail.roomNumber}</p></div><Status contract={detail} /></div><div className="contract-amounts"><div><small>TIỀN THUÊ / THÁNG</small><strong>{money(detail.monthlyRent)}</strong></div><div><small>TIỀN CỌC</small><strong>{money(detail.depositAmount)}</strong></div></div><dl className="contract-facts"><Fact label="Bắt đầu" value={date(detail.startDate)} /><Fact label="Kết thúc dự kiến" value={date(detail.endDate)} /><Fact label="Hạn thanh toán" value={detail.paymentDueDay ? `Ngày ${detail.paymentDueDay} hằng tháng` : 'Chưa thiết lập'} />{detail.closedOn && <Fact label="Kết thúc thực tế" value={date(detail.closedOn)} />}</dl>{detail.closureReason && <div className="contract-note"><strong>Lý do kết thúc</strong><p>{detail.closureReason}</p></div>}{detail.note && <div className="contract-note"><strong>Ghi chú</strong><p>{detail.note}</p></div>}<div className="contract-people-head"><Users size={19} aria-hidden="true" /><h3>Người thuê</h3><span>{detail.tenants.length}</span></div>{detail.tenants.length ? <ul className="contract-people">{detail.tenants.map(t => <li key={t.id}><span className="contract-avatar" aria-hidden="true">{t.fullName?.trim().slice(0, 1).toLocaleUpperCase('vi-VN') || 'N'}</span><div><strong>{t.fullName}</strong><small>{t.email || 'Chưa có email'} · Vào {date(t.joinDate)}{t.leaveDate ? ` · Rời ${date(t.leaveDate)}` : ''}</small></div>{detail.status === 'Active' && !t.leaveDate && <button className="contract-text-button" type="button" disabled={busy} onClick={e => { openConfirm({ type: 'leave', tenant: t }, e.currentTarget); setActionDate(today()) }}>Ghi ngày rời</button>}</li>)}</ul> : <p className="contract-inline-empty">Thêm ít nhất một người thuê trước khi kích hoạt.</p>}
      {['Draft', 'Active'].includes(detail.status) && <form className="contract-add-person" onSubmit={e => { e.preventDefault(); run(async () => { await contractApi.addUser(propertyId, detail.id, { email: extraEmail.trim(), joinDate: extraJoinDate }); setExtraEmail('') }, 'Đã thêm người thuê.') }}><label>Email người ở cùng<input type="email" required placeholder="Email tài khoản người thuê" value={extraEmail} onChange={e => setExtraEmail(e.target.value)} /></label><label>Ngày vào ở<input type="date" required min={detail.startDate} max={detail.endDate || undefined} value={extraJoinDate} onChange={e => setExtraJoinDate(e.target.value)} /></label><button className="contract-button contract-button--secondary" type="submit" disabled={busy || !extraEmail.trim()}><Plus size={16} aria-hidden="true" /> Thêm người</button></form>}
      {needsSettlement(detail) && <div className="contract-message contract-message--warning" role="status"><AlertCircle size={19} aria-hidden="true" /><span><strong>Cần đối chiếu tất toán.</strong> Còn nợ {money(detail.outstandingAmount)} · Cọc đang giữ {money(detail.depositHeld)}{detail.hasDraftInvoices ? ' · Có hóa đơn nháp' : ''}{!detail.hasSettlement ? ' · Chưa có bản ghi tất toán' : ''}</span></div>}
      {confirm && <form className="contract-confirm" ref={confirmRef} tabIndex={-1} onSubmit={submitConfirm}><div><strong>{confirm.type === 'leave' ? `Ngày rời của ${confirm.tenant.fullName}` : confirm.type === 'terminate' ? 'Kết thúc hợp đồng sớm' : 'Hủy bản nháp?'}</strong><button className="contract-icon-button" type="button" aria-label="Đóng xác nhận" onClick={() => setConfirm(null)}><X size={16} /></button></div>{confirm.type === 'leave' && <label>Ngày rời<input type="date" required min={confirm.tenant.joinDate} max={detail.endDate || undefined} value={actionDate} onChange={e => setActionDate(e.target.value)} /></label>}{confirm.type === 'terminate' && <label>Lý do kết thúc<textarea required rows={3} maxLength={1000} placeholder="Nêu lý do để lưu cùng lịch sử hợp đồng" value={reason} onChange={e => setReason(e.target.value)} /></label>}{confirm.type === 'cancel' && <p>Hợp đồng nháp sẽ chuyển sang trạng thái đã hủy.</p>}<div className="contract-confirm-actions"><button className="contract-button contract-button--ghost" type="button" onClick={() => setConfirm(null)}>Quay lại</button><button className="contract-button contract-button--danger" type="submit" disabled={busy}>{busy ? 'Đang xử lý…' : 'Xác nhận'}</button></div></form>}
      <div className="contract-detail-actions">{detail.status === 'Draft' && <><button className="contract-button contract-button--primary" type="button" disabled={busy || !detail.tenants.length} onClick={() => run(() => contractApi.activate(propertyId, detail.id), 'Đã kích hoạt hợp đồng.')}><Check size={16} aria-hidden="true" /> Kích hoạt hợp đồng</button><button className="contract-button contract-button--ghost" type="button" disabled={busy} onClick={e => openConfirm({ type: 'cancel' }, e.currentTarget)}>Hủy bản nháp</button></>}{detail.status === 'Active' && <button className="contract-button contract-button--ghost" type="button" disabled={busy} onClick={e => { openConfirm({ type: 'terminate' }, e.currentTarget); setReason('') }}>Kết thúc sớm</button>}</div></>}</section></div>
    </>}
  </div>
}


