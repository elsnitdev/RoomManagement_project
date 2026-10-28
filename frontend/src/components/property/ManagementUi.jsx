import { useEffect, useRef } from 'react'
import { AlertTriangle, ChevronLeft, ChevronRight, RotateCcw, Trash2, X } from 'lucide-react'

export const dateText = value => value
  ? new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value))
  : '—'

export const moneyText = value => new Intl.NumberFormat('vi-VN', {
  style: 'currency', currency: 'VND', maximumFractionDigits: 0,
}).format(value)

export const messageOf = error => error?.status === 409 && error?.details?.code === 'ROW_VERSION_CONFLICT'
  ? 'Dữ liệu đã thay đổi. Hãy tải lại trang và mở lại biểu mẫu trước khi lưu.'
  : error?.message || 'Không thể xử lý yêu cầu. Vui lòng thử lại.'

export function IconButton({ label, icon: Icon, tone = 'neutral', className = '', ...props }) {
  return (
    <button
      type="button"
      className={`pm-icon-button pm-icon-button--${tone} ${className}`.trim()}
      aria-label={label}
      title={label}
      {...props}
    >
      <Icon size={18} aria-hidden="true" />
    </button>
  )
}

const roomStatuses = {
  Available: { label: 'Còn trống', tone: 'available' },
  Occupied: { label: 'Đã thuê', tone: 'occupied' },
  Maintenance: { label: 'Bảo trì', tone: 'maintenance' },
  Inactive: { label: 'Ngừng sử dụng', tone: 'inactive' },
}

export function Status({ active, roomStatus }) {
  if (roomStatus) {
    const status = roomStatuses[roomStatus] || { label: roomStatus, tone: 'inactive' }
    return <span className={`pm-status pm-status--${status.tone}`}>{status.label}</span>
  }
  return <span className={`pm-status pm-status--${active ? 'available' : 'inactive'}`}>{active ? 'Đang hoạt động' : 'Tạm ngừng'}</span>
}

export function Feedback({ error, onRetry }) {
  return (
    <div className="pm-feedback" role="alert">
      <AlertTriangle size={21} aria-hidden="true" />
      <div><strong>Không tải được dữ liệu</strong><p>{messageOf(error)}</p></div>
      {onRetry && <IconButton label="Thử tải lại" icon={RotateCcw} onClick={onRetry} />}
    </div>
  )
}

export function Pagination({ page, meta, onChange }) {
  if (!meta || meta.totalPages <= 1) return null
  return (
    <nav className="pm-pagination" aria-label="Phân trang">
      <span>Trang {page} / {meta.totalPages} · {meta.totalItems} mục</span>
      <div>
        <IconButton label="Trang trước" icon={ChevronLeft} disabled={page <= 1} onClick={() => onChange(page - 1)} />
        <IconButton label="Trang sau" icon={ChevronRight} disabled={page >= meta.totalPages} onClick={() => onChange(page + 1)} />
      </div>
    </nav>
  )
}

export function ConfirmationToast({ request, busy, onCancel, onConfirm }) {
  const cancelRef = useRef(null)
  const requestOpen = Boolean(request)

  useEffect(() => {
    if (requestOpen) cancelRef.current?.focus()
  }, [requestOpen])

  useEffect(() => {
    if (!requestOpen) return
    const onEscape = event => {
      if (event.key === 'Escape' && !busy) onCancel()
    }
    window.addEventListener('keydown', onEscape)
    return () => window.removeEventListener('keydown', onEscape)
  }, [requestOpen, busy, onCancel])

  if (!request) return null
  const itemName = request.kind === 'property' ? request.propertyName : `phòng ${request.room.roomNumber}`

  return (
    <div className="pm-confirm-toast" role="alertdialog" aria-labelledby="pm-confirm-title" aria-describedby="pm-confirm-description" aria-modal="false">
      <div className="pm-confirm-icon"><AlertTriangle size={22} aria-hidden="true" /></div>
      <div className="pm-confirm-body">
        <h2 id="pm-confirm-title">Xóa {itemName}?</h2>
        <p id="pm-confirm-description">Dữ liệu đã xóa không thể khôi phục. Nếu có phòng, hợp đồng hoặc dữ liệu liên quan, hệ thống sẽ từ chối thao tác.</p>
        {request.error && <p className="pm-confirm-error" role="alert">{request.error}</p>}
        <div className="pm-confirm-actions">
          <button ref={cancelRef} type="button" className="pm-confirm-cancel" onClick={onCancel} disabled={busy}><X size={16} aria-hidden="true" /> Hủy</button>
          <button type="button" className="pm-confirm-delete" onClick={onConfirm} disabled={busy}><Trash2 size={16} aria-hidden="true" /> {busy ? 'Đang xóa...' : 'Xác nhận xóa'}</button>
        </div>
      </div>
    </div>
  )
}
