import { useEffect, useMemo, useState } from 'react'
import {
  AlertCircle, ArrowRight, Bell, Building2, Check, CheckCheck, CheckCircle2,
  CircleHelp, ClipboardList, Copy, FileText, Home, MapPin, ReceiptText, Send,
  UserRound, Wrench,
} from 'lucide-react'
import { initialUsers } from './mockData'
import { tenantMockInvoiceItems, tenantMockNotices, tenantMockProfiles } from './tenantMockData'
import './Tenant.css'

const money = value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(Number(value) || 0)
const balanceOf = invoice => Math.max(0, Number(invoice.amount || 0) - Number(invoice.paid || 0))
const dateOf = value => {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value || '')
  return match ? new Date(Number(match[3]), Number(match[2]) - 1, Number(match[1])) : null
}
const invoiceStatus = invoice => {
  if (balanceOf(invoice) === 0) return { label: 'Đã thanh toán', tone: 'success' }
  const due = dateOf(invoice.due)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  if (due && due < today) return { label: 'Quá hạn', tone: 'danger' }
  return Number(invoice.paid || 0) > 0
    ? { label: 'Thanh toán một phần', tone: 'warning' }
    : { label: 'Chưa thanh toán', tone: 'neutral' }
}
const requestLabels = { new: 'Đã gửi', progress: 'Đang xử lý', done: 'Đã hoàn tất' }
async function copyCode(code, notify) {
  try {
    await navigator.clipboard.writeText(code)
    notify?.(`Đã sao chép mã ${code}.`)
  } catch {
    notify?.('Không sao chép được. Vui lòng chọn và sao chép mã thủ công.')
  }
}
const pageMeta = {
  overview: ['TỔNG QUAN', 'Chỗ ở của bạn', 'Những thông tin cần biết trong một nơi.'],
  room: ['CHỖ Ở HIỆN TẠI', 'Phòng của tôi', 'Thông tin phòng và khu trọ bạn đang ở.'],
  billing: ['TÀI CHÍNH', 'Hóa đơn của tôi', 'Theo dõi các kỳ thanh toán và số tiền còn lại.'],
  contracts: ['HỢP ĐỒNG', 'Hợp đồng thuê', 'Thông tin thỏa thuận thuê phòng hiện tại.'],
  requests: ['HỖ TRỢ', 'Yêu cầu của tôi', 'Gửi vấn đề cần hỗ trợ và theo dõi tiến độ.'],
  notices: ['CẦN CHÚ Ý', 'Thông báo quan trọng', 'Các việc cần chú ý liên quan đến chỗ ở.'],
  profile: ['TÀI KHOẢN', 'Thông tin cá nhân', 'Kiểm tra thông tin tài khoản của bạn.'],
}

function Header({ page, name }) {
  const [eyebrow, title, description] = pageMeta[page] || pageMeta.overview
  return <header className="tn-heading">
    <span>{eyebrow}</span>
    <h1>{page === 'overview' && name ? `Chào ${name}` : title}</h1>
    <p>{description}</p>
  </header>
}

function Empty({ icon: Icon = CircleHelp, title, description }) {
  return <div className="tn-empty"><Icon size={25} aria-hidden="true" /><strong>{title}</strong><p>{description}</p></div>
}

function StateTag({ label, tone = 'neutral' }) {
  return <span className={`tn-tag tn-tag--${tone}`}>{label}</span>
}

function LinkButton({ children, onClick, secondary = false }) {
  return <button type="button" className={`tn-action${secondary ? ' tn-action--secondary' : ''}`} onClick={onClick}>{children}<ArrowRight size={15} aria-hidden="true" /></button>
}

function Field({ label, value }) {
  return <div className="tn-field"><dt>{label}</dt><dd>{value || 'Chưa cập nhật'}</dd></div>
}

function Overview({ name, room, property, currentInvoice, openRequests, notices, navigate }) {
  return <>
    <Header page="overview" name={name} />
    <div className="tn-overview-grid">
      <section className="tn-card tn-home-card">
        <div className="tn-card-kicker"><Home size={16} aria-hidden="true" /> CHỖ Ở HIỆN TẠI</div>
        {room ? <><div className="tn-home-identity"><strong>Phòng {room.number}</strong><StateTag label="Đang ở" tone="success" /></div><p className="tn-home-property">{property?.name || 'Khu trọ chưa cập nhật'}</p><p className="tn-home-address"><MapPin size={15} aria-hidden="true" />{property?.address || 'Địa chỉ chưa cập nhật'}</p><div className="tn-home-facts"><span>Tiền thuê <strong>{money(room.price)}/tháng</strong></span><span>Diện tích <strong>{room.area ? `${room.area} m²` : 'Chưa cập nhật'}</strong></span></div><LinkButton onClick={() => navigate('tenant/room')}>Xem chỗ ở</LinkButton></>
          : <Empty icon={Home} title="Chưa có thông tin chỗ ở" description="Tài khoản này chưa được liên kết với phòng trong dữ liệu hiện tại." />}
      </section>
      <section className="tn-card tn-attention-card">
        <div className="tn-card-kicker"><Bell size={16} aria-hidden="true" /> VIỆC CẦN CHÚ Ý</div>
        {currentInvoice && balanceOf(currentInvoice) > 0 ? <div className="tn-attention-item"><span>Hóa đơn kỳ {currentInvoice.month}</span><strong>{money(balanceOf(currentInvoice))}</strong><small>Hạn thanh toán {currentInvoice.due}</small></div>
          : <div className="tn-attention-item tn-attention-item--clear">{currentInvoice ? <CheckCircle2 size={18} aria-hidden="true" /> : <ReceiptText size={18} aria-hidden="true" />}<span>{currentInvoice ? 'Không có hóa đơn còn phải thu.' : 'Mở mục Hóa đơn để xem số dư mới nhất.'}</span></div>}
        <div className="tn-attention-bottom"><span>{openRequests} yêu cầu đang theo dõi</span><span>{notices.length} thông tin cần chú ý</span></div>
        <LinkButton secondary onClick={() => navigate('tenant/notices')}>Xem thông báo</LinkButton>
      </section>
    </div>
    <section className="tn-quick-section"><h2>Truy cập nhanh</h2><div className="tn-quick-grid">
      <button type="button" onClick={() => navigate('tenant/billing')}><ReceiptText size={19} aria-hidden="true" /><span><strong>Hóa đơn</strong><small>Xem kỳ thu và số dư</small></span><ArrowRight size={16} aria-hidden="true" /></button>
      <button type="button" onClick={() => navigate('tenant/contracts')}><FileText size={19} aria-hidden="true" /><span><strong>Hợp đồng</strong><small>Thời hạn và tiền thuê</small></span><ArrowRight size={16} aria-hidden="true" /></button>
      <button type="button" onClick={() => navigate('tenant/requests')}><Wrench size={19} aria-hidden="true" /><span><strong>Gửi yêu cầu</strong><small>Liên hệ khi cần hỗ trợ</small></span><ArrowRight size={16} aria-hidden="true" /></button>
    </div></section>
  </>
}

function RoomPage({ room, property, navigate }) {
  return <><Header page="room" />{room ? <div className="tn-room-grid">
    <section className="tn-card tn-room-main"><div className="tn-room-photo"><img src="/room-studio.jpg" alt="Ảnh minh họa phòng trọ" /><span>Ảnh minh họa</span></div><div className="tn-room-content"><div className="tn-section-head"><div><span>PHÒNG ĐANG Ở</span><h2>Phòng {room.number}</h2></div><StateTag label="Đang ở" tone="success" /></div><p className="tn-room-address"><Building2 size={16} aria-hidden="true" />{property?.name || 'Khu trọ chưa cập nhật'}</p><p className="tn-room-address"><MapPin size={16} aria-hidden="true" />{property?.address || 'Địa chỉ chưa cập nhật'}</p><dl className="tn-facts"><Field label="Tầng" value={room.floor === 0 ? 'Tầng trệt' : room.floor != null ? `Tầng ${room.floor}` : ''} /><Field label="Diện tích" value={room.area ? `${room.area} m²` : ''} /><Field label="Giá thuê/tháng" value={money(room.price)} /><Field label="Kỳ thanh toán tiếp theo" value={room.due} /></dl>{room.description && <p className="tn-room-description">{room.description}</p>}</div></section>
    <aside className="tn-card tn-room-side"><div className="tn-card-kicker"><UserRound size={16} aria-hidden="true" /> NGƯỜI ĐANG Ở</div>{room.occupants?.length ? <ul className="tn-occupants">{room.occupants.map(person => <li key={person}>{person}</li>)}</ul> : <p>{room.tenant || 'Chưa có danh sách người ở.'}</p>}<div className="tn-room-actions"><LinkButton secondary onClick={() => navigate('tenant/contracts')}>Xem hợp đồng</LinkButton><LinkButton secondary onClick={() => navigate('tenant/billing')}>Xem hóa đơn</LinkButton><LinkButton onClick={() => navigate('tenant/requests')}>Gửi yêu cầu</LinkButton></div></aside>
  </div> : <section className="tn-card"><Empty icon={Home} title="Chưa có phòng được liên kết" description="Thông tin chỗ ở sẽ xuất hiện khi tài khoản được liên kết với phòng." /></section>}</>
}

function BillingPage({ invoices, notify }) {
  const [selectedId, setSelectedId] = useState(null)
  const [filter, setFilter] = useState('all')
  const filtered = invoices.filter(invoice => filter === 'all' || (filter === 'open' ? balanceOf(invoice) > 0 : balanceOf(invoice) === 0))
  const selected = filtered.find(item => item.id === selectedId) || filtered[0]
  const items = selected?.items?.length ? selected.items : tenantMockInvoiceItems[selected?.id] || []
  const filters = [
    { id: 'all', label: 'Tất cả', count: invoices.length },
    { id: 'open', label: 'Còn phải thu', count: invoices.filter(item => balanceOf(item) > 0).length },
    { id: 'paid', label: 'Đã thanh toán', count: invoices.filter(item => balanceOf(item) === 0).length },
  ]
  return <><Header page="billing" /><div className="tn-billing-layout"><section className="tn-card tn-billing-list">
    <div className="tn-section-head"><div><span>CÁC KỲ HÓA ĐƠN</span><h2>Danh sách hóa đơn</h2></div><small>{invoices.length} kỳ</small></div>
    <div className="tn-filter-bar" role="group" aria-label="Lọc hóa đơn">{filters.map(item => <button key={item.id} type="button" className={filter === item.id ? 'is-active' : ''} onClick={() => { setFilter(item.id); setSelectedId(null) }} aria-pressed={filter === item.id}>{item.label}<span>{item.count}</span></button>)}</div>
    {filtered.length ? <><div className="tn-bill-head" aria-hidden="true"><span>Kỳ / Mã</span><span>Hạn thu</span><span>Tổng tiền</span><span>Còn thu</span></div><ul className="tn-bills">{filtered.map(invoice => { const status = invoiceStatus(invoice); return <li key={invoice.id}><button type="button" className={selected?.id === invoice.id ? 'is-selected' : ''} onClick={() => setSelectedId(invoice.id)} aria-pressed={selected?.id === invoice.id}><span><strong>Kỳ {invoice.month}</strong><small>{invoice.id}</small></span><span>{invoice.due}</span><span>{money(invoice.amount)}</span><span><strong>{money(balanceOf(invoice))}</strong><StateTag {...status} /></span></button></li> })}</ul></> : <Empty icon={ReceiptText} title="Không có hóa đơn phù hợp" description="Chọn trạng thái khác để xem các kỳ hóa đơn." />}
  </section><aside className="tn-card tn-bill-detail"><div className="tn-section-head"><div><span>CHI TIẾT HÓA ĐƠN</span><h2>{selected?.id || 'Chưa có hóa đơn'}</h2></div>{selected && <StateTag {...invoiceStatus(selected)} />}</div>{selected ? <><div className="tn-bill-period">Phòng {selected.room} · Kỳ {selected.month}</div><dl className="tn-bill-totals"><Field label="Tổng hóa đơn" value={money(selected.amount)} /><Field label="Đã ghi nhận" value={money(selected.paid)} /><Field label="Còn phải thu" value={money(balanceOf(selected))} /><Field label="Hạn thanh toán" value={selected.due} /></dl>{items.length > 0 && <div className="tn-bill-items"><h3>Các khoản thu</h3>{items.map(item => <div key={item.id}><span>{item.description}</span><strong>{money(item.amount)}</strong></div>)}</div>}{selected.adjustments?.length > 0 && <div className="tn-bill-adjustments"><h3>Điều chỉnh</h3>{selected.adjustments.map(item => <p key={item.id}>{item.reason} · {item.direction === 'add' ? '+' : '−'}{money(item.amount)}</p>)}</div>}<button type="button" className="tn-inline-action" onClick={() => copyCode(selected.id, notify)}><Copy size={14} aria-hidden="true" /> Sao chép mã hóa đơn</button><p className="tn-small-note">Số liệu đang dùng dữ liệu mẫu, chưa kết nối lịch sử giao dịch.</p></> : <p className="tn-small-note">Chọn một hóa đơn để xem thông tin.</p>}</aside></div></>
}

function ContractsPage({ contracts, navigate, notify }) {
  return <><Header page="contracts" />{contracts.length ? <div className="tn-contract-list">{contracts.map(contract => <section className="tn-card" key={contract.id}><div className="tn-section-head"><div><span>MÃ HỢP ĐỒNG {contract.id}</span><h2>Phòng {contract.room}</h2></div><StateTag label={contract.status === 'active' ? 'Đang hiệu lực' : 'Sắp hết hạn'} tone={contract.status === 'active' ? 'success' : 'warning'} /></div><dl className="tn-facts"><Field label="Người thuê" value={contract.tenant} /><Field label="Tiền thuê/tháng" value={money(contract.rent)} /><Field label="Ngày bắt đầu" value={contract.start} /><Field label="Ngày kết thúc" value={contract.end} /></dl><div className="tn-card-actions"><button type="button" className="tn-inline-action" onClick={() => copyCode(contract.id, notify)}><Copy size={14} aria-hidden="true" /> Sao chép mã</button><button type="button" className="tn-inline-action" onClick={() => navigate('tenant/room')}>Xem phòng <ArrowRight size={14} aria-hidden="true" /></button></div></section>)}</div> : <section className="tn-card"><Empty icon={FileText} title="Chưa có hợp đồng" description="Tài khoản chưa có hợp đồng thuê liên kết trong dữ liệu hiện tại." /></section>}</>
}

function RequestsPage({ room, tenantName, requests, setRequests, notify }) {
  const [category, setCategory] = useState('Sửa chữa')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState('')
  const [filter, setFilter] = useState('all')
  const filtered = requests.filter(request => filter === 'all' || (filter === 'open' ? request.status !== 'done' : request.status === 'done'))
  function submit(event) {
    event.preventDefault()
    if (!room) return setError('Tài khoản cần được liên kết với phòng trước khi gửi yêu cầu.')
    if (title.trim().length < 5) return setError('Tiêu đề cần ít nhất 5 ký tự.')
    if (description.trim().length < 15) return setError('Mô tả cần ít nhất 15 ký tự để người quản lý hiểu vấn đề.')
    const date = new Intl.DateTimeFormat('vi-VN').format(new Date())
    setRequests?.(current => [{ id: `YC-${Date.now()}`, title: title.trim(), description: description.trim(), category, room: room.number, tenant: tenantName, date, status: 'new', priority: 'Trung bình' }, ...current])
    setTitle('')
    setDescription('')
    setError('')
    notify?.('Đã gửi yêu cầu mẫu.')
  }
  return <><Header page="requests" /><div className="tn-request-layout"><section className="tn-card tn-request-form"><div className="tn-section-head"><div><span>GỬI TỚI QUẢN LÝ</span><h2>Tạo yêu cầu</h2></div><Wrench size={19} aria-hidden="true" /></div><p className="tn-small-note">Nêu rõ vị trí và tình trạng để được hỗ trợ nhanh hơn. Biểu mẫu hiện dùng dữ liệu mẫu.</p><form onSubmit={submit} noValidate><label>Loại yêu cầu<select value={category} onChange={event => setCategory(event.target.value)}><option>Sửa chữa</option><option>Tiện ích</option><option>Khác</option></select></label><label>Tiêu đề<input value={title} onChange={event => { setTitle(event.target.value); setError('') }} maxLength={100} placeholder="Ví dụ: Máy lạnh không hoạt động" required /></label><label>Mô tả chi tiết<textarea value={description} onChange={event => { setDescription(event.target.value); setError('') }} maxLength={1000} rows={5} placeholder="Mô tả vấn đề, thời điểm phát hiện và vị trí trong phòng..." required /></label>{error && <p className="tn-form-error" role="alert"><AlertCircle size={15} aria-hidden="true" />{error}</p>}<button className="tn-submit" type="submit" disabled={!room}><Send size={16} aria-hidden="true" /> Gửi yêu cầu</button></form></section><section className="tn-card tn-request-history"><div className="tn-section-head"><div><span>THEO DÕI</span><h2>Yêu cầu đã gửi</h2></div><small>{requests.length} yêu cầu</small></div><div className="tn-filter-bar" role="group" aria-label="Lọc yêu cầu"><button type="button" className={filter === 'all' ? 'is-active' : ''} onClick={() => setFilter('all')} aria-pressed={filter === 'all'}>Tất cả <span>{requests.length}</span></button><button type="button" className={filter === 'open' ? 'is-active' : ''} onClick={() => setFilter('open')} aria-pressed={filter === 'open'}>Đang theo dõi <span>{requests.filter(item => item.status !== 'done').length}</span></button><button type="button" className={filter === 'done' ? 'is-active' : ''} onClick={() => setFilter('done')} aria-pressed={filter === 'done'}>Hoàn tất <span>{requests.filter(item => item.status === 'done').length}</span></button></div>{filtered.length ? <ul>{filtered.map(request => <li key={request.id}><div><strong>{request.title}</strong><small>{request.id} · {request.date} · {request.category || 'Khác'}</small>{request.description && <p>{request.description}</p>}</div><StateTag label={requestLabels[request.status] || 'Đã gửi'} tone={request.status === 'done' ? 'success' : request.status === 'progress' ? 'warning' : 'neutral'} /></li>)}</ul> : <Empty icon={ClipboardList} title="Không có yêu cầu phù hợp" description="Thử chọn trạng thái khác để xem yêu cầu." />}</section></div></>
}

function NoticesPage({ notices, readIds, onRead, onReadAll, navigate }) {
  const [filter, setFilter] = useState('all')
  const unread = notices.filter(notice => !readIds.includes(notice.id))
  const visible = filter === 'unread' ? unread : notices
  return <><Header page="notices" /><section className="tn-card tn-notices"><div className="tn-section-head"><div><span>THÔNG BÁO CHO BẠN</span><h2>Cần chú ý</h2></div><small>{unread.length} chưa đọc</small></div><div className="tn-notice-toolbar"><div className="tn-filter-bar" role="group" aria-label="Lọc thông báo"><button type="button" className={filter === 'all' ? 'is-active' : ''} onClick={() => setFilter('all')} aria-pressed={filter === 'all'}>Tất cả <span>{notices.length}</span></button><button type="button" className={filter === 'unread' ? 'is-active' : ''} onClick={() => setFilter('unread')} aria-pressed={filter === 'unread'}>Chưa đọc <span>{unread.length}</span></button></div><button type="button" className="tn-inline-action" onClick={onReadAll} disabled={!unread.length}><CheckCheck size={15} aria-hidden="true" /> Đánh dấu tất cả đã đọc</button></div>{visible.length ? <ul>{visible.map(notice => { const isRead = readIds.includes(notice.id); return <li key={notice.id} className={isRead ? 'is-read' : 'is-unread'}><span className={`tn-notice-icon tn-notice-icon--${notice.tone}`}><notice.icon size={18} aria-hidden="true" /></span><div><strong>{notice.title}</strong><p>{notice.description}</p><small>{notice.meta}</small><div className="tn-notice-actions">{notice.target && <button type="button" className="tn-inline-action" onClick={() => { onRead(notice.id); navigate(`tenant/${notice.target}`) }}>Xem liên quan <ArrowRight size={14} aria-hidden="true" /></button>}{!isRead && <button type="button" className="tn-inline-action" onClick={() => onRead(notice.id)}><Check size={14} aria-hidden="true" /> Đã đọc</button>}</div></div><StateTag label={isRead ? 'Đã đọc' : notice.label} tone={isRead ? 'neutral' : notice.tone} /></li> })}</ul> : <Empty icon={Bell} title={filter === 'unread' ? 'Đã đọc hết thông báo' : 'Chưa có thông báo'} description={filter === 'unread' ? 'Các thông báo mới sẽ xuất hiện tại đây.' : 'Thông tin liên quan đến hóa đơn và yêu cầu sẽ xuất hiện tại đây.'} />}</section></>
}

function ProfilePage({ user, profile, onSave, notify }) {
  const [draft, setDraft] = useState(profile)
  const [editing, setEditing] = useState(false)
  useEffect(() => { setDraft(profile); setEditing(false) }, [profile, user?.id])
  function save(event) {
    event.preventDefault()
    if (!draft.fullName.trim()) return
    onSave({ ...draft, fullName: draft.fullName.trim() })
    setEditing(false)
    notify?.('Đã cập nhật thông tin trong bản xem trước.')
  }
  return <><Header page="profile" /><section className="tn-card tn-profile"><div className="tn-section-head"><div><span>TÀI KHOẢN CỦA BẠN</span><h2>{profile.fullName || user?.userName || 'Người thuê'}</h2></div><StateTag label="Người thuê" tone="neutral" /></div><p className="tn-small-note">Thông tin chỉnh sửa chỉ lưu trong phiên xem trước, chưa đồng bộ lên máy chủ.</p><form onSubmit={save}><label>Họ và tên<input value={draft.fullName} disabled={!editing} onChange={event => setDraft(current => ({ ...current, fullName: event.target.value }))} required /></label><label>Email<input value={draft.email} disabled readOnly /></label><label>Số điện thoại<input value={draft.phoneNumber} disabled={!editing} onChange={event => setDraft(current => ({ ...current, phoneNumber: event.target.value }))} /></label><label>Địa chỉ liên hệ<input value={draft.address} disabled={!editing} onChange={event => setDraft(current => ({ ...current, address: event.target.value }))} /></label><div className="tn-profile-actions">{editing ? <><button type="button" className="tn-action tn-action--secondary" onClick={() => { setDraft(profile); setEditing(false) }}>Hủy</button><button type="submit" className="tn-submit">Lưu thay đổi</button></> : <button type="button" className="tn-action tn-action--secondary" onClick={() => setEditing(true)}>Chỉnh sửa thông tin <ArrowRight size={15} aria-hidden="true" /></button>}</div></form></section></>
}

export default function Tenant({ page = 'overview', user, navigate, notify, rooms = [], properties = [], contracts = [], invoices = [], invoiceHistory = [], requests = [], setRequests }) {
  const [readIds, setReadIds] = useState([])
  useEffect(() => setReadIds([]), [user?.id])
  const mockTenant = initialUsers.find(item => item.role === 'Người thuê' && item.email?.toLocaleLowerCase('vi-VN') === user?.email?.toLocaleLowerCase('vi-VN'))
  const mockProfile = mockTenant ? tenantMockProfiles[mockTenant.email.toLowerCase()] : null
  const tenantName = mockTenant?.name || ''
  const displayName = (user?.fullName || user?.name || mockTenant?.name || user?.userName || '').trim()
  const [profile, setProfile] = useState(() => ({ fullName: displayName, email: user?.email || '', phoneNumber: user?.phoneNumber || mockProfile?.phoneNumber || '', address: mockProfile?.address || '' }))
  useEffect(() => setProfile({ fullName: displayName, email: user?.email || '', phoneNumber: user?.phoneNumber || mockProfile?.phoneNumber || '', address: mockProfile?.address || '' }), [user?.id, user?.email, user?.phoneNumber, displayName, mockProfile?.phoneNumber, mockProfile?.address])
  const mine = item => tenantName && item?.tenant?.trim().toLocaleLowerCase('vi-VN') === tenantName.toLocaleLowerCase('vi-VN')
  const myRooms = rooms.filter(room => mine(room) || (tenantName && room.occupants?.some(name => name.trim().toLocaleLowerCase('vi-VN') === tenantName.toLocaleLowerCase('vi-VN'))))
  const room = myRooms[0]
  const property = properties.find(item => item.id === room?.propertyId)
  const myContracts = contracts.filter(mine)
  const myInvoices = [...invoices, ...invoiceHistory].filter(mine).sort((a, b) => (dateOf(b.due)?.getTime() || 0) - (dateOf(a.due)?.getTime() || 0))
  const myRequests = requests.filter(mine)
  const currentInvoice = myInvoices[0]
  const notices = useMemo(() => {
    const result = tenantMockNotices.filter(notice => mockTenant && notice.audienceEmail.toLowerCase() === mockTenant.email.toLowerCase()).map(notice => ({ ...notice, icon: Bell, meta: `${notice.category} · ${notice.date}`, label: notice.important ? 'Quan trọng' : 'Thông tin', tone: notice.important ? 'warning' : 'neutral' }))
    myInvoices.filter(invoice => balanceOf(invoice) > 0).forEach(invoice => result.push({ id: invoice.id, icon: ReceiptText, title: `Hóa đơn kỳ ${invoice.month} còn phải thu`, description: `${money(balanceOf(invoice))} · hạn thanh toán ${invoice.due}.`, meta: `Phòng ${invoice.room} · ${invoice.id}`, label: invoiceStatus(invoice).label, tone: invoiceStatus(invoice).tone, target: 'billing' }))
    myRequests.filter(request => request.status === 'progress').forEach(request => result.push({ id: request.id, icon: Wrench, title: `Yêu cầu đang được xử lý: ${request.title}`, description: 'Theo dõi tiến độ xử lý trong mục Yêu cầu.', meta: `${request.id} · ${request.date}`, label: 'Đang xử lý', tone: 'warning', target: 'requests' }))
    return result
  }, [mockTenant, myInvoices, myRequests])
  const unreadNotices = notices.filter(notice => !readIds.includes(notice.id))
  const markRead = id => setReadIds(current => current.includes(id) ? current : [...current, id])

  if (page === 'room') return <div className="tn-page"><RoomPage room={room} property={property} navigate={navigate} /></div>
  if (page === 'billing') return <div className="tn-page"><BillingPage invoices={myInvoices} notify={notify} /></div>
  if (page === 'contracts') return <div className="tn-page"><ContractsPage contracts={myContracts} navigate={navigate} notify={notify} /></div>
  if (page === 'requests') return <div className="tn-page"><RequestsPage room={room} tenantName={tenantName} requests={myRequests} setRequests={setRequests} notify={notify} /></div>
  if (page === 'notices') return <div className="tn-page"><NoticesPage notices={notices} readIds={readIds} onRead={markRead} onReadAll={() => setReadIds(notices.map(notice => notice.id))} navigate={navigate} /></div>
  if (page === 'profile') return <div className="tn-page"><ProfilePage user={user} profile={profile} onSave={setProfile} notify={notify} /></div>
  return <div className="tn-page"><Overview name={profile.fullName || displayName} room={room} property={property} currentInvoice={currentInvoice} openRequests={myRequests.filter(request => request.status !== 'done').length} notices={unreadNotices} navigate={navigate} /></div>
}
