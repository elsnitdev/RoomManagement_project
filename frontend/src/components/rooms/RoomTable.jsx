import { Pencil, Trash2 } from 'lucide-react'
import { IconButton, Status, moneyText } from '../property/ManagementUi'

export default function RoomTable({ rooms, busy, onEdit, onDelete }) {
  return (
    <div className="pm-room-table-wrap">
      <table className="pm-room-table">
        <thead><tr><th scope="col">Phòng</th><th scope="col">Tầng · Diện tích</th><th scope="col">Giá/tháng</th><th scope="col">Số người tối đa</th><th scope="col">Trạng thái</th><th scope="col"><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{rooms.map(room => (
          <tr key={room.id}>
            <td><strong>{room.roomNumber}</strong><small>{room.roomName || 'Chưa đặt tên'}</small></td>
            <td>{room.floor === null ? '—' : `Tầng ${room.floor}`} · {room.area === null ? '—' : `${room.area} m²`}</td>
            <td>{moneyText(room.basePrice)}</td>
            <td>{room.maxOccupants}</td>
            <td><Status roomStatus={room.status} /></td>
            <td><div className="pm-row-actions">
              <IconButton label={`Sửa phòng ${room.roomNumber}`} icon={Pencil} onClick={() => onEdit(room.id)} disabled={busy} />
              <IconButton label={`Xóa phòng ${room.roomNumber}`} icon={Trash2} tone="danger" onClick={event => onDelete(event, room)} disabled={busy} />
            </div></td>
          </tr>
        ))}</tbody>
      </table>
    </div>
  )
}
