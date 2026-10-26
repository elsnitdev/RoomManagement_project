import { useState } from 'react'
import { contracts, formatCurrency } from './mockData'
import { Badge, Icon } from './ui'
import './RoomDetail.css'

const photos = [
  { src: '/room-studio.jpg', alt: 'Ảnh minh họa không gian phòng trọ', label: 'Không gian phòng' },
  { src: '/room-bathroom.jpg', alt: 'Ảnh minh họa phòng tắm', label: 'Phòng tắm' },
]

const statusLabels = {
  occupied: 'Đang cho thuê',
  vacant: 'Phòng trống',
  maintenance: 'Đang bảo trì',
}

function DetailItem({ label, value }) {
  return (
    <div className="room-detail-item">
      <span>{label}</span>
      <strong>{value || 'Chưa cập nhật'}</strong>
    </div>
  )
}

export default function RoomDetail({ room, property, navigate }) {
  const [activePhoto, setActivePhoto] = useState(0)

  if (!room) {
    return (
      <section className="room-detail-missing content-card">
        <span className="room-detail-empty-icon"><Icon name="door" size={26} /></span>
        <h1>Không tìm thấy phòng</h1>
        <p>Phòng có thể đã bị xóa khỏi dữ liệu mẫu hoặc đường dẫn không hợp lệ.</p>
        <button className="button button--primary" type="button" onClick={() => navigate('landlord/rooms')}>
          Về danh sách phòng
        </button>
      </section>
    )
  }

  const occupants = room.status === 'occupied'
    ? (room.occupants?.length ? room.occupants : room.tenant?.trim() ? [room.tenant] : [])
    : []
  const hasTenant = occupants.length > 0
  const displayStatus = room.status === 'occupied' && !hasTenant ? 'vacant' : room.status
  const contract = hasTenant
    ? contracts.find(item => item.room === room.number && item.tenant === room.tenant)
    : null

  return (
    <div className="room-detail-page">
      <button className="room-detail-back" type="button" onClick={() => navigate('landlord/rooms')}>
        <Icon name="arrow" size={17} /> Danh sách phòng
      </button>

      <div className="room-detail-heading">
        <div>
          <span className="eyebrow">CHI TIẾT PHÒNG · DỮ LIỆU MẪU</span>
          <h1>Phòng {room.number}</h1>
          <p>{property?.name || 'Khu trọ chưa xác định'} · {property?.address || 'Chưa cập nhật địa chỉ'}</p>
        </div>
        <Badge kind={displayStatus}>{statusLabels[displayStatus] || 'Chưa xác định'}</Badge>
      </div>

      <section className="room-photo-card" aria-label="Hình ảnh minh họa phòng">
        <div className="room-photo-main">
          <img src={photos[activePhoto].src} alt={photos[activePhoto].alt} />
          <span className="room-photo-caption">{photos[activePhoto].label} · Ảnh minh họa</span>
        </div>
        <div className="room-photo-thumbnails" role="group" aria-label="Chọn ảnh phòng">
          {photos.map((photo, index) => (
            <button
              key={photo.src}
              type="button"
              className={activePhoto === index ? 'room-photo-thumbnail room-photo-thumbnail--active' : 'room-photo-thumbnail'}
              onClick={() => setActivePhoto(index)}
              aria-label={'Xem ảnh ' + photo.label.toLowerCase()}
              aria-pressed={activePhoto === index}
            >
              <img src={photo.src} alt="" />
              <span>{photo.label}</span>
            </button>
          ))}
          <p>Ảnh chỉ dùng để duyệt bố cục, chưa phải ảnh thật của phòng.</p>
        </div>
      </section>

      <div className="room-detail-grid">
        <div className="room-detail-primary">
          <section className="content-card room-detail-section">
            <div className="room-detail-section-heading">
              <span className="room-detail-section-icon"><Icon name="door" size={20} /></span>
              <div><h2>Thông tin phòng</h2><p>Thông số và vị trí của phòng</p></div>
            </div>
            <div className="room-detail-facts">
              <DetailItem label="Mã phòng" value={room.number} />
              <DetailItem label="Khu trọ" value={property?.name} />
              <DetailItem label="Tầng" value={room.floor === 0 ? 'Tầng trệt' : room.floor != null ? 'Tầng ' + room.floor : ''} />
              <DetailItem label="Diện tích" value={room.area ? room.area + ' m²' : ''} />
              <DetailItem label="Giá thuê hàng tháng" value={formatCurrency(room.price)} />
              <DetailItem label="Tình trạng" value={statusLabels[displayStatus]} />
            </div>
            {room.description && <p className="room-detail-description">{room.description}</p>}
          </section>

          <section className="content-card room-detail-section">
            <div className="room-detail-section-heading">
              <span className="room-detail-section-icon"><Icon name="file" size={20} /></span>
              <div><h2>Hợp đồng thuê</h2><p>Hợp đồng đang liên kết với phòng</p></div>
            </div>
            {contract ? (
              <>
                <div className="room-contract-bar">
                  <div><span>MÃ HỢP ĐỒNG</span><strong>{contract.id}</strong></div>
                  <Badge kind={contract.status === 'active' ? 'approved' : 'pending'}>
                    {contract.status === 'active' ? 'Đang hiệu lực' : 'Sắp hết hạn'}
                  </Badge>
                </div>
                <div className="room-detail-facts">
                  <DetailItem label="Người thuê" value={contract.tenant} />
                  <DetailItem label="Tiền thuê/tháng" value={formatCurrency(contract.rent)} />
                  <DetailItem label="Ngày bắt đầu" value={contract.start} />
                  <DetailItem label="Ngày kết thúc" value={contract.end} />
                </div>
              </>
            ) : (
              <div className="room-detail-empty">
                <Icon name="file" size={24} />
                <strong>{hasTenant ? 'Chưa có hợp đồng được liên kết' : 'Phòng chưa có hợp đồng'}</strong>
                <p>{hasTenant ? 'Hãy kiểm tra dữ liệu hợp đồng của người đang ở.' : 'Thông tin hợp đồng sẽ xuất hiện khi phòng có người thuê.'}</p>
              </div>
            )}
          </section>
        </div>

        <aside className="room-detail-aside">
          <section className="content-card room-rent-card">
            <span>GIÁ THUÊ HÀNG THÁNG</span>
            <strong>{formatCurrency(room.price)}</strong>
            <p>Giá niêm yết của phòng trong dữ liệu mẫu.</p>
          </section>

          <section className="content-card room-detail-section">
            <div className="room-detail-section-heading">
              <span className="room-detail-section-icon"><Icon name="users" size={20} /></span>
              <div><h2>Người đang ở</h2><p>Tình trạng cư trú hiện tại</p></div>
            </div>
            {hasTenant ? (
              <div className="room-tenant-list">
                {occupants.map((name, index) => (
                  <div className="room-tenant" key={name}>
                    <span className="room-tenant-avatar">{name.split(' ').slice(-2).map(word => word[0]).join('')}</span>
                    <div><strong>{name}</strong><small>{index === 0 ? 'Người thuê chính' : 'Người ở cùng'}</small></div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="room-detail-empty room-detail-empty--side">
                <Icon name="door" size={24} />
                <strong>{displayStatus === 'vacant' ? 'Phòng trống' : 'Chưa có người ở'}</strong>
                <p>{room.status === 'maintenance' ? 'Phòng đang bảo trì và chưa có người ở.' : 'Hiện chưa có người thuê phòng này.'}</p>
              </div>
            )}
            {hasTenant && room.due && (
              <div className="room-next-due"><Icon name="clock" size={17} /> Kỳ thanh toán tiếp theo: <strong>{room.due}</strong></div>
            )}
          </section>
        </aside>
      </div>
    </div>
  )
}
