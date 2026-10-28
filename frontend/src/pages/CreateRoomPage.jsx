import { ArrowLeft, AlertTriangle } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { useApiQuery } from '../hooks/useApiQuery'
import { getProperty } from '../services/api'
import RoomForm from '../components/rooms/RoomForm'
import { Feedback, IconButton } from '../components/property/ManagementUi'
import '../styles/property-management.css'

export default function CreateRoomPage({ propertyId, navigate, notify }) {
  const { user } = useAuth()
  const propertyQuery = useApiQuery(
    `properties:${user?.id}:detail:${propertyId}`,
    () => getProperty(propertyId),
    { enabled: Boolean(user && propertyId) },
  )
  const property = propertyQuery.data?.data
  const back = () => navigate(`landlord/properties/${propertyId}/rooms`)

  return (
    <div className="pm-page pm-page--create-room">
      <div className="pm-detail-topline"><IconButton label="Quay về quản lý phòng" icon={ArrowLeft} onClick={back} /><span>Khu trọ / Quản lý phòng / Tạo phòng</span></div>
      {propertyQuery.isLoading ? <div className="pm-loading" role="status">Đang tải khu trọ...</div>
        : propertyQuery.error ? <Feedback error={propertyQuery.error} onRetry={propertyQuery.reload} />
          : property && <>
            <header className="pm-hero"><div><span className="pm-eyebrow">{property.name}</span><h1>Tạo phòng mới</h1><p>Phòng mới sẽ thuộc khu trọ {property.name}.</p></div></header>
            {property.isActive
              ? <RoomForm propertyId={propertyId} onCancel={back} onSaved={room => { notify(`Đã tạo phòng ${room.roomNumber}.`); back() }} />
              : <p className="pm-inactive-note"><AlertTriangle size={17} aria-hidden="true" /> Khu trọ đang tạm ngừng. Hãy kích hoạt khu trọ trong phần chỉnh sửa trước khi tạo phòng.</p>}
          </>}
    </div>
  )
}
