import { ArrowLeft, Building2, FileText, ReceiptText, Users } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { useApiQuery } from '../hooks/useApiQuery'
import { getProperty, getRoom, listRoomRows } from '../services/api'
import RoomImageGallery from '../components/rooms/RoomImageGallery'
import { Feedback, Status, moneyText } from '../components/property/ManagementUi'
import '../styles/property-management.css'
import './RoomDetailPage.css'

const floorText = floor => floor == null ? 'Chưa cập nhật' : floor === 0 ? 'Tầng trệt' : `Tầng ${floor}`

function Fact({ label, children }) {
  return <div className="rd-fact"><span>{label}</span><strong>{children}</strong></div>
}

export default function RoomDetailPage({ propertyId, roomId, navigate, notify }) {
  const { user } = useAuth()
  const roomQuery = useApiQuery(
    `rooms:${propertyId}:${user?.id}:detail:${roomId}`,
    () => getRoom(propertyId, roomId),
    { enabled: Boolean(user && propertyId && roomId) },
  )
  const propertyQuery = useApiQuery(
    `properties:${user?.id}:detail:${propertyId}`,
    () => getProperty(propertyId),
    { enabled: Boolean(user && propertyId) },
  )
  const rowsQuery = useApiQuery(
    `room-rows:${propertyId}:${user?.id}`,
    () => listRoomRows(propertyId),
    { enabled: Boolean(user && propertyId) },
  )
  const room = roomQuery.data?.data
  const property = propertyQuery.data?.data
  const row = (rowsQuery.data || []).find(item => item.id === room?.rowId)
  const occupants = room?.occupants || []
  const back = () => navigate(`landlord/properties/${propertyId}/rooms`)

  return <div className="pm-page rd-page">
    <nav className="rd-breadcrumb" aria-label="Đường dẫn">
      <button type="button" onClick={back}><ArrowLeft size={17} aria-hidden="true" /> Danh sách phòng</button>
      <span aria-hidden="true">/</span><span>Chi tiết phòng</span>
    </nav>

    {roomQuery.isLoading || propertyQuery.isLoading ? <div className="pm-loading" role="status">Đang tải chi tiết phòng...</div>
      : roomQuery.error || propertyQuery.error ? <Feedback error={roomQuery.error || propertyQuery.error} onRetry={() => { roomQuery.reload(); propertyQuery.reload() }} />
        : room && property ? <>
          <header className="rd-hero">
            <div className="rd-hero__identity">
              <span className="pm-eyebrow">CHI TIẾT PHÒNG · {property.name}</span>
              <h1>Phòng {room.roomNumber}</h1>
              <p>{room.roomName || 'Chưa đặt tên phòng'} · {property.address || 'Chưa cập nhật địa chỉ'}</p>
            </div>
            <div className="rd-hero__status"><Status roomStatus={room.status} /></div>
          </header>

          <div className="rd-metrics" aria-label="Thông số chính">
            <div className="rd-metric"><span>Giá thuê hàng tháng</span><strong>{moneyText(room.basePrice)}</strong></div>
            <div className="rd-metric"><span>Diện tích</span><strong>{room.area == null ? 'Chưa cập nhật' : `${room.area} m²`}</strong></div>
            <div className="rd-metric"><span>Sức chứa tối đa</span><strong>{room.maxOccupants} người</strong></div>
          </div>

          <div className="rd-layout">
            <div className="rd-main">
              <div className="content-card rd-gallery-card"><RoomImageGallery propertyId={propertyId} roomId={roomId} notify={notify} /></div>
              <section className="content-card rd-card" aria-labelledby="rd-info-title">
                <div className="rd-card__heading"><Building2 size={20} aria-hidden="true" /><div><h2 id="rd-info-title">Thông tin phòng</h2><p>Vị trí và thông tin vận hành</p></div></div>
                <div className="rd-facts">
                  <Fact label="Mã phòng">{room.roomNumber}</Fact>
                  <Fact label="Tên phòng">{room.roomName || 'Chưa cập nhật'}</Fact>
                  <Fact label="Khu trọ">{property.name}</Fact>
                  <Fact label="Dãy">{row ? `${row.code} · ${row.name}` : room.rowId ? rowsQuery.isLoading ? 'Đang tải...' : 'Không xác định' : 'Chưa có dãy'}</Fact>
                  <Fact label="Tầng">{floorText(room.floor)}</Fact>
                  <Fact label="Tình trạng"><Status roomStatus={room.status} /></Fact>
                </div>
                {room.description && <div className="rd-description"><h3>Mô tả</h3><p>{room.description}</p></div>}
              </section>
            </div>

            <aside className="rd-sidebar">
              <section className="content-card rd-card" aria-labelledby="rd-occupants-title">
                <div className="rd-card__heading"><Users size={20} aria-hidden="true" /><div><h2 id="rd-occupants-title">Người đang ở</h2><p>{occupants.length} / {room.maxOccupants} người</p></div></div>
                {occupants.length ? <ul className="rd-occupants">{occupants.map(person => <li key={person.tenantId}>{person.fullName}</li>)}</ul>
                  : <p className="rd-empty">Phòng chưa có người ở.</p>}
              </section>
              <section className="content-card rd-card" aria-labelledby="rd-actions-title">
                <div className="rd-card__heading"><FileText size={20} aria-hidden="true" /><div><h2 id="rd-actions-title">Quản lý phòng</h2><p>Hợp đồng và hóa đơn</p></div></div>
                <div className="rd-hero__actions">
                  {room.status === 'Available' && <button className="rd-action rd-action--primary" type="button" onClick={() => navigate(`landlord/contracts/new/${propertyId}/${room.id}`)}><FileText size={17} aria-hidden="true" /> Tạo hợp đồng</button>}
                  {room.contractId && <button className="rd-action" type="button" onClick={() => navigate(`landlord/contracts/${propertyId}/${room.contractId}`)}><FileText size={17} aria-hidden="true" /> Xem hợp đồng hiện tại</button>}
                  <button className="rd-action" type="button" onClick={() => navigate(`landlord/billing/rooms/${propertyId}/${room.id}`)}><ReceiptText size={17} aria-hidden="true" /> Xem hóa đơn phòng</button>
                </div>
              </section>
            </aside>
          </div>
        </> : <div className="pm-feedback" role="alert">Không tìm thấy phòng.</div>}
  </div>
}
