import { useCallback, useEffect, useRef, useState } from 'react'
import {
  AlertCircle,
  ArrowLeft,
  BellOff,
  Building2,
  CheckCheck,
  ChevronRight,
  Clock,
  ExternalLink,
  FileText,
  Inbox,
  Megaphone,
  MessageSquareText,
  Plus,
  ReceiptText,
  Send,
  ShieldAlert,
  UserRound,
  Users,
  X,
} from 'lucide-react'
import { notificationApi } from '../services/api/notificationApi'
import NotificationComposerModal from './NotificationComposerModal'
import '../styles/notifications.css'

function getNotificationMeta(item) {
  const type = (item?.type || '').toLowerCase()
  if (item?.invoiceId || type.includes('invoice') || type.includes('billing') || type.includes('payment')) {
    return {
      label: 'Hóa đơn',
      badgeClass: 'nt-type-badge--invoice',
      Icon: ReceiptText,
    }
  }
  if (item?.contractId || type.includes('contract')) {
    return {
      label: 'Hợp đồng',
      badgeClass: 'nt-type-badge--contract',
      Icon: FileText,
    }
  }
  if (type === 'tenantrequest' || (item?.requestId && !type.includes('announcement'))) {
    return {
      label: 'Yêu cầu',
      badgeClass: 'nt-type-badge--request',
      Icon: MessageSquareText,
    }
  }
  if (
    type === 'systembroadcast' ||
    type === 'adminlandlordbroadcast' ||
    type === 'admindirectlandlord'
  ) {
    return {
      label: 'Hệ thống · Admin',
      badgeClass: 'nt-type-badge--system',
      Icon: ShieldAlert,
    }
  }
  if (
    type === 'operatoralltenants' ||
    type === 'propertyannouncement' ||
    type === 'operatordirecttenant' ||
    type === 'managerannouncement' ||
    type === 'announcement'
  ) {
    return {
      label: 'Ban quản lý',
      badgeClass: 'nt-type-badge--general',
      Icon: Megaphone,
    }
  }
  return {
    label: 'Thông báo chung',
    badgeClass: 'nt-type-badge--general',
    Icon: Megaphone,
  }
}

function getSentScopeBadge(item) {
  switch (item?.targetScope) {
    case 'AllSystem':
      return { label: 'Toàn hệ thống', badgeClass: 'nt-type-badge--system' }
    case 'AllLandlords':
      return { label: 'Tất cả chủ trọ', badgeClass: 'nt-type-badge--system' }
    case 'SpecificLandlord':
      return { label: 'Chủ trọ cụ thể', badgeClass: 'nt-type-badge--system' }
    case 'AllActiveTenants':
      return { label: 'Tất cả người thuê', badgeClass: 'nt-type-badge--general' }
    case 'SpecificProperty':
      return { label: 'Theo khu trọ', badgeClass: 'nt-type-badge--contract' }
    case 'SpecificTenant':
      return { label: 'Cá nhân cụ thể', badgeClass: 'nt-type-badge--request' }
    default:
      return { label: 'Đã phát hành', badgeClass: 'nt-type-badge--general' }
  }
}

function formatDateTime(value) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export default function NotificationCenter({ role, initialNotificationId = '', navigate, onReadChange, notify }) {
  const [items, setItems] = useState([])
  const [sentItems, setSentItems] = useState([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [filter, setFilter] = useState('all')
  const [unreadCount, setUnreadCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [selectedNotification, setSelectedNotification] = useState(null)
  const [detailItem, setDetailItem] = useState(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [selectedSent, setSelectedSent] = useState(null)
  const [markingReadId, setMarkingReadId] = useState(null)
  const [markingAll, setMarkingAll] = useState(false)
  const markingReadRef = useRef(new Set())

  const [showComposer, setShowComposer] = useState(false)

  const canSendNotification = role === 'Admin' || role === 'Landlord' || role === 'Manager'

  const fetchUnreadCount = useCallback(async () => {
    try {
      const response = await notificationApi.unreadCount()
      const count = response?.data?.count
      if (typeof count === 'number') setUnreadCount(count)
    } catch {
      // Keep existing count if unread-count endpoint fails silently
    }
  }, [])

  const load = useCallback(async (nextPage = 1, nextFilter = filter) => {
    setLoading(true)
    setError('')
    try {
      if (nextFilter === 'sent' && canSendNotification) {
        const [response] = await Promise.all([
          notificationApi.listSent(nextPage, 20),
          fetchUnreadCount(),
        ])
        setSentItems(response.data || [])
        setTotalPages(Math.max(1, response.meta?.totalPages || 1))
        setPage(nextPage)
      } else {
        const unreadOnly = nextFilter === 'unread'
        const [response] = await Promise.all([
          notificationApi.list(nextPage, unreadOnly),
          fetchUnreadCount(),
        ])
        setItems(response.data || [])
        setTotalPages(Math.max(1, response.meta?.totalPages || 1))
        setPage(nextPage)
        onReadChange?.()
      }
    } catch (e) {
      setError(e.message || 'Không thể tải thông báo.')
    } finally {
      setLoading(false)
    }
  }, [filter, canSendNotification, fetchUnreadCount, onReadChange])

  useEffect(() => {
    if (!canSendNotification && filter === 'sent') {
      setFilter('all')
      load(1, 'all')
      return
    }
    load(1, filter)
  }, [role]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!selectedNotification && !selectedSent) return undefined

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    function handleKeyDown(event) {
      if (event.key !== 'Escape') return
      if (selectedNotification) {
        setSelectedNotification(null)
      } else if (selectedSent) {
        setSelectedSent(null)
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [selectedNotification, selectedSent])

  async function markRead(id) {
    if (!id || markingReadRef.current.has(id)) return
    markingReadRef.current.add(id)
    setMarkingReadId(id)
    try {
      await notificationApi.read(id)
      const nowIso = new Date().toISOString()
      setItems((current) =>
        current.map((item) =>
          item.id === id ? { ...item, isRead: true, readAt: item.readAt || nowIso } : item
        )
      )
      setSelectedNotification((current) =>
        current && current.id === id
          ? { ...current, isRead: true, readAt: current.readAt || nowIso }
          : current
      )
      setUnreadCount((current) => Math.max(0, current - 1))
      onReadChange?.()
    } catch (e) {
      setError(e.message || 'Không thể đánh dấu đã đọc.')
    } finally {
      markingReadRef.current.delete(id)
      setMarkingReadId((current) => (current === id ? null : current))
    }
  }

  const roleBase = role === 'Tenant' ? 'tenant' : role === 'Admin' ? 'admin' : 'landlord'

  function openDetail(item) {
    if (!item?.id) return
    if (!item.isRead) {
      markRead(item.id)
    }
    if (navigate) {
      navigate(`${roleBase}/notifications/${item.id}`)
    } else {
      window.location.hash = `/${roleBase}/notifications/${item.id}`
    }
  }

  function backToList() {
    setDetailItem(null)
    if (navigate) {
      navigate(`${roleBase}/notifications`)
    } else {
      window.location.hash = `/${roleBase}/notifications`
    }
  }

  async function markAllRead() {
    if (markingAll || unreadCount === 0 || filter === 'sent') return
    setMarkingAll(true)
    setError('')
    try {
      await notificationApi.readAll()
      const nowIso = new Date().toISOString()
      setItems((current) =>
        filter === 'unread'
          ? []
          : current.map((item) => ({ ...item, isRead: true, readAt: item.readAt || nowIso }))
      )
      if (filter === 'unread') {
        setPage(1)
        setTotalPages(1)
      }
      setSelectedNotification((current) =>
        current ? { ...current, isRead: true, readAt: current.readAt || nowIso } : current
      )
      setUnreadCount(0)
      onReadChange?.()
    } catch (e) {
      setError(e.message || 'Không thể đánh dấu tất cả đã đọc.')
    } finally {
      setMarkingAll(false)
    }
  }

  function handleFilterChange(nextFilter) {
    if (nextFilter === filter) return
    setFilter(nextFilter)
    load(1, nextFilter)
  }

  function hasRelatedLink(item) {
    if (!item || role === 'Admin') return false
    const type = (item?.type || '').toLowerCase()
    if (item.invoiceId || item.contractId) return true
    if (item.requestId && !type.includes('announcement')) return true
    return false
  }

  function relatedLabel(item) {
    if (item?.invoiceId) return 'Xem chi tiết hóa đơn'
    if (item?.contractId) return 'Xem chi tiết hợp đồng'
    if (item?.requestId) return 'Xem chi tiết yêu cầu'
    return 'Xem nội dung liên quan'
  }

  function openRelated(item) {
    if (!hasRelatedLink(item)) return
    setSelectedNotification(null)
    const base = role === 'Tenant' ? 'tenant' : 'landlord'
    let target = `${base}/notifications`
    if (item.invoiceId) {
      target = `${base}/billing/${item.invoiceId}`
    } else if (item.contractId) {
      target =
        role !== 'Tenant' && item.propertyId
          ? `landlord/contracts/${item.propertyId}/${item.contractId}`
          : `${base}/contracts`
    } else if (item.requestId) {
      target = `${base}/requests`
    }
    if (navigate) navigate(target)
    else window.location.hash = `/${target}`
  }

  useEffect(() => {
    if (!initialNotificationId) {
      setDetailItem(null)
      setDetailLoading(false)
      return
    }
    const fromList = items.find((entry) => String(entry.id) === String(initialNotificationId))
    if (fromList) {
      setDetailItem(fromList)
      if (!fromList.isRead) markRead(fromList.id)
      return
    }
    let cancelled = false
    setDetailLoading(true)
    notificationApi
      .list(1, false, 100)
      .then((res) => {
        if (cancelled) return
        const found = (res?.data || []).find(
          (entry) => String(entry.id) === String(initialNotificationId),
        )
        if (found) {
          setDetailItem(found)
          if (!found.isRead) markRead(found.id)
        } else {
          setDetailItem(null)
        }
      })
      .catch(() => {
        if (!cancelled) setDetailItem(null)
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [initialNotificationId, items])

  const subtitle =
    role === 'Admin'
      ? 'Nhận thông báo hệ thống và gửi thông báo tới toàn hệ thống, các chủ trọ hoặc một chủ trọ cụ thể.'
      : role === 'Landlord' || role === 'Manager'
        ? 'Nhận thông báo từ quản trị viên, yêu cầu từ người thuê và gửi thông báo tới người thuê đang có hợp đồng hoạt động.'
        : 'Theo dõi hóa đơn, hợp đồng, yêu cầu hỗ trợ và thông báo từ ban quản lý.'

  const selectedMeta = selectedNotification ? getNotificationMeta(selectedNotification) : null
  const selectedSentMeta = selectedSent ? getSentScopeBadge(selectedSent) : null

  if (initialNotificationId) {
    const detailMeta = detailItem ? getNotificationMeta(detailItem) : null
    const DetailIcon = detailMeta?.Icon
    return (
      <section className="nt-page nt-page--detail">
        <button type="button" className="nt-back-btn" onClick={backToList}>
          <ArrowLeft size={16} aria-hidden="true" />
          <span>Quay lại danh sách thông báo</span>
        </button>

        {detailLoading && !detailItem ? (
          <div className="nt-card nt-detail-card">
            <div className="nt-state" role="status">
              <p className="nt-state-text">Đang tải chi tiết thông báo…</p>
            </div>
          </div>
        ) : !detailItem ? (
          <div className="nt-card nt-detail-card">
            <div className="nt-state">
              <div className="nt-state-icon">
                <BellOff size={24} aria-hidden="true" />
              </div>
              <strong className="nt-state-title">Không tìm thấy thông báo</strong>
              <p className="nt-state-text">
                Thông báo không tồn tại hoặc đã bị xóa khỏi hộp thư của bạn.
              </p>
              <button type="button" className="nt-btn nt-btn--primary" onClick={backToList}>
                Về danh sách thông báo
              </button>
            </div>
          </div>
        ) : (
          <article className="nt-card nt-detail-card">
            <header className="nt-detail-head">
              <div className="nt-detail-head-top">
                {DetailIcon && (
                  <span className="nt-detail-icon">
                    <DetailIcon size={21} aria-hidden="true" />
                  </span>
                )}
                <div className="nt-detail-head-copy">
                  <div className="nt-detail-badges">
                    {detailMeta && (
                      <span className={`nt-type-badge ${detailMeta.badgeClass}`}>
                        <span>{detailMeta.label}</span>
                      </span>
                    )}
                    <span className={`nt-status-pill ${detailItem.isRead ? 'nt-status-pill--read' : 'nt-status-pill--new'}`}>
                      {detailItem.isRead ? 'Đã đọc' : 'Mới'}
                    </span>
                  </div>
                  <h1 className="nt-detail-title">{detailItem.title}</h1>
                </div>
              </div>

              <div className="nt-detail-meta-bar">
                <span className="nt-detail-meta-chip">
                  <Clock size={13} aria-hidden="true" />
                  <span>{formatDateTime(detailItem.createdAt)}</span>
                </span>
                {detailItem.senderName && (
                  <span className="nt-detail-meta-chip">
                    <UserRound size={13} aria-hidden="true" />
                    <span>Từ: <strong>{detailItem.senderName}</strong></span>
                  </span>
                )}
                {detailItem.propertyName && (
                  <span className="nt-detail-meta-chip">
                    <Building2 size={13} aria-hidden="true" />
                    <span>{detailItem.propertyName}</span>
                  </span>
                )}
              </div>
            </header>

            <div className="nt-detail-body">
              <p className="nt-detail-message">{detailItem.message}</p>

              {hasRelatedLink(detailItem) && (
                <div className="nt-detail-callout">
                  <div className="nt-detail-callout__info">
                    {DetailIcon && (
                      <span className="nt-detail-callout__icon">
                        <DetailIcon size={18} aria-hidden="true" />
                      </span>
                    )}
                    <div>
                      <strong>
                        {detailItem.invoiceId
                          ? 'Hóa đơn liên quan'
                          : detailItem.contractId
                            ? 'Hợp đồng liên quan'
                            : 'Yêu cầu liên quan'}
                      </strong>
                      <span>
                        Nhấn để mở trực tiếp trang chi tiết và thực hiện thao tác tiếp theo.
                      </span>
                    </div>
                  </div>
                  <button
                    type="button"
                    className="nt-btn nt-btn--primary"
                    onClick={() => openRelated(detailItem)}
                  >
                    <span>{relatedLabel(detailItem)}</span>
                    <ExternalLink size={15} aria-hidden="true" />
                  </button>
                </div>
              )}
            </div>

            <footer className="nt-detail-foot">
              <span className="nt-detail-foot-note">
                {detailItem.readAt
                  ? `Đã xem lúc ${formatDateTime(detailItem.readAt)}`
                  : 'Thông báo đã được đánh dấu là đã đọc'}
              </span>
              <button type="button" className="nt-btn nt-btn--subtle" onClick={backToList}>
                <ArrowLeft size={15} aria-hidden="true" />
                <span>Danh sách thông báo</span>
              </button>
            </footer>
          </article>
        )}
      </section>
    )
  }

  return (
    <section className="nt-page">
      <header className="nt-header">
        <div className="nt-header-copy">
          <span className="nt-eyebrow">TRUNG TÂM THÔNG BÁO</span>
          <h1 className="nt-title">Thông báo</h1>
          <p className="nt-subtitle">{subtitle}</p>
        </div>

        {canSendNotification && (
          <button
            type="button"
            className="nt-btn nt-btn--primary"
            onClick={() => setShowComposer(true)}
          >
            <Plus size={16} aria-hidden="true" />
            <span>Gửi thông báo</span>
          </button>
        )}
      </header>

      <div className="nt-card">
        <div className="nt-toolbar">
          <div className="nt-tabs" role="tablist" aria-label="Lọc thông báo">
            <button
              type="button"
              role="tab"
              aria-selected={filter === 'all'}
              className={`nt-tab ${filter === 'all' ? 'is-active' : ''}`}
              onClick={() => handleFilterChange('all')}
            >
              <span>Tất cả</span>
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={filter === 'unread'}
              className={`nt-tab ${filter === 'unread' ? 'is-active' : ''}`}
              onClick={() => handleFilterChange('unread')}
            >
              <span>Chưa đọc</span>
              <span className="nt-tab-badge">{unreadCount}</span>
            </button>
            {canSendNotification && (
              <button
                type="button"
                role="tab"
                aria-selected={filter === 'sent'}
                className={`nt-tab ${filter === 'sent' ? 'is-active' : ''}`}
                onClick={() => handleFilterChange('sent')}
              >
                <Send size={13} aria-hidden="true" />
                <span>Đã gửi</span>
              </button>
            )}
          </div>

          {filter !== 'sent' && (
            <button
              type="button"
              className="nt-btn nt-btn--ghost"
              disabled={loading || markingAll || unreadCount === 0}
              onClick={markAllRead}
            >
              <CheckCheck size={16} aria-hidden="true" />
              <span>{markingAll ? 'Đang xử lý…' : 'Đánh dấu tất cả đã đọc'}</span>
            </button>
          )}
        </div>

        {error && (
          <div className="nt-alert" role="alert">
            <AlertCircle size={16} aria-hidden="true" />
            <span>{error}</span>
          </div>
        )}

        {loading ? (
          <div className="nt-loading">Đang tải thông báo…</div>
        ) : filter === 'sent' ? (
          sentItems.length > 0 ? (
            <ul className="nt-list">
              {sentItems.map((sent) => {
                const scopeMeta = getSentScopeBadge(sent)
                const allRead = sent.totalRecipients > 0 && sent.readCount >= sent.totalRecipients
                return (
                  <li
                    key={sent.eventKey}
                    role="button"
                    tabIndex={0}
                    className="nt-item nt-item--read"
                    onClick={() => setSelectedSent(sent)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault()
                        setSelectedSent(sent)
                      }
                    }}
                  >
                    <div className="nt-item-icon" aria-hidden="true">
                      <Send size={17} />
                    </div>

                    <div className="nt-item-body">
                      <div className="nt-item-top">
                        <h3 className="nt-item-title" style={{ color: '#102a43', fontWeight: 700 }}>
                          {sent.title}
                        </h3>
                        <span className={`nt-type-badge ${scopeMeta.badgeClass}`}>
                          {scopeMeta.label}
                        </span>
                      </div>

                      <p className="nt-item-preview">{sent.message}</p>

                      <div className="nt-item-meta-row">
                        <span className="nt-item-meta">
                          <Users size={12} aria-hidden="true" />
                          <span>{sent.targetLabel}</span>
                        </span>
                        <span className="nt-item-meta">
                          <Clock size={12} aria-hidden="true" />
                          <span>{formatDateTime(sent.createdAt)}</span>
                        </span>
                      </div>
                    </div>

                    <div className="nt-item-side">
                      <span
                        className={`nt-progress-pill${allRead ? ' nt-progress-pill--done' : ''}`}
                      >
                        Đã đọc {sent.readCount}/{sent.totalRecipients}
                      </span>

                      <span className="nt-detail-hint">
                        <span>Xem chi tiết</span>
                        <ChevronRight size={14} aria-hidden="true" />
                      </span>
                    </div>
                  </li>
                )
              })}
            </ul>
          ) : (
            <div className="nt-empty">
              <div className="nt-empty-icon" aria-hidden="true">
                <Send size={24} />
              </div>
              <h3 className="nt-empty-title">Chưa có thông báo đã gửi</h3>
              <p className="nt-empty-desc">
                Nhấn nút “Gửi thông báo” để phát hành thông báo tới đối tượng mong muốn.
              </p>
            </div>
          )
        ) : items.length > 0 ? (
          <ul className="nt-list">
            {items.map((item) => {
              const meta = getNotificationMeta(item)
              const TypeIcon = meta.Icon
              return (
                <li
                  key={item.id}
                  role="button"
                  tabIndex={0}
                  className={`nt-item ${item.isRead ? 'nt-item--read' : 'nt-item--unread'}`}
                  onClick={() => openDetail(item)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault()
                      openDetail(item)
                    }
                  }}
                >
                  <div className="nt-item-icon" aria-hidden="true">
                    <TypeIcon size={18} />
                  </div>

                  <div className="nt-item-body">
                    <div className="nt-item-top">
                      {!item.isRead && <span className="nt-unread-dot" aria-label="Chưa đọc" />}
                      <h3 className="nt-item-title">{item.title}</h3>
                      <span className={`nt-type-badge ${meta.badgeClass}`}>{meta.label}</span>
                    </div>

                    <p className="nt-item-preview">{item.message}</p>

                    <div className="nt-item-meta-row">
                      <span className="nt-item-meta">
                        <Clock size={12} aria-hidden="true" />
                        <span>{formatDateTime(item.createdAt)}</span>
                      </span>
                      {item.senderName && (
                        <span className="nt-item-meta">
                          <UserRound size={12} aria-hidden="true" />
                          <span>Từ: {item.senderName}</span>
                        </span>
                      )}
                      {item.propertyName && (
                        <span className="nt-item-meta">
                          <Building2 size={12} aria-hidden="true" />
                          <span>{item.propertyName}</span>
                        </span>
                      )}
                    </div>
                  </div>

                  <div className="nt-item-side">
                    <span className={`nt-status-pill ${item.isRead ? 'nt-status-pill--read' : 'nt-status-pill--new'}`}>
                      {item.isRead ? 'Đã đọc' : 'Mới'}
                    </span>

                    <span className="nt-detail-hint">
                      <span>Xem chi tiết</span>
                      <ChevronRight size={14} aria-hidden="true" />
                    </span>
                  </div>
                </li>
              )
            })}
          </ul>
        ) : (
          <div className="nt-empty">
            <div className="nt-empty-icon" aria-hidden="true">
              {filter === 'unread' ? <BellOff size={24} /> : <Inbox size={24} />}
            </div>
            <h3 className="nt-empty-title">
              {filter === 'unread' ? 'Không có thông báo chưa đọc' : 'Chưa có thông báo nào'}
            </h3>
            <p className="nt-empty-desc">
              {filter === 'unread'
                ? 'Bạn đã xem toàn bộ thông báo trong hộp thư.'
                : 'Các thông báo mới về hóa đơn, hợp đồng, yêu cầu và thông báo hệ thống sẽ hiển thị tại đây.'}
            </p>
          </div>
        )}

        <div className="nt-pagination">
          <button
            type="button"
            className="nt-btn nt-btn--subtle"
            disabled={loading || page <= 1}
            onClick={() => load(page - 1, filter)}
          >
            Trước
          </button>
          <span className="nt-page-info">
            Trang {page} / {totalPages}
          </span>
          <button
            type="button"
            className="nt-btn nt-btn--subtle"
            disabled={loading || page >= totalPages}
            onClick={() => load(page + 1, filter)}
          >
            Sau
          </button>
        </div>
      </div>

      {/* Received Notification Detail Modal */}
      {selectedNotification && (
        <div
          className="nt-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) setSelectedNotification(null)
          }}
        >
          <div
            className="nt-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="nt-detail-title"
          >
            <div className="nt-modal-header">
              <div className="nt-modal-header-main">
                <div className="nt-modal-badges">
                  <span className={`nt-type-badge ${selectedMeta.badgeClass}`}>
                    {selectedMeta.label}
                  </span>
                  <span
                    className={`nt-modal-read-state ${
                      markingReadId === selectedNotification.id
                        ? 'nt-modal-read-state--syncing'
                        : 'nt-modal-read-state--done'
                    }`}
                  >
                    {markingReadId === selectedNotification.id ? 'Đang đánh dấu đã đọc' : 'Đã đọc'}
                  </span>
                </div>

                <h2 id="nt-detail-title" className="nt-modal-title">
                  {selectedNotification.title}
                </h2>

                <div className="nt-modal-meta">
                  <span className="nt-modal-meta-item">
                    <Clock size={13} aria-hidden="true" />
                    <span>{formatDateTime(selectedNotification.createdAt)}</span>
                  </span>
                  {selectedNotification.senderName && (
                    <span className="nt-modal-meta-item">
                      <UserRound size={13} aria-hidden="true" />
                      <span>Từ: {selectedNotification.senderName}</span>
                    </span>
                  )}
                  {selectedNotification.propertyName && (
                    <span className="nt-modal-meta-item">
                      <Building2 size={13} aria-hidden="true" />
                      <span>{selectedNotification.propertyName}</span>
                    </span>
                  )}
                </div>
              </div>

              <button
                type="button"
                className="nt-modal-close"
                aria-label="Đóng chi tiết thông báo"
                onClick={() => setSelectedNotification(null)}
              >
                <X size={18} />
              </button>
            </div>

            <div className="nt-modal-body">
              <p className="nt-modal-message">{selectedNotification.message}</p>
            </div>

            <div className="nt-modal-footer">
              <button
                type="button"
                className="nt-btn nt-btn--subtle"
                onClick={() => setSelectedNotification(null)}
              >
                Đóng
              </button>
              {hasRelatedLink(selectedNotification) && (
                <button
                  type="button"
                  className="nt-btn nt-btn--primary"
                  onClick={() => openRelated(selectedNotification)}
                >
                  <ExternalLink size={15} aria-hidden="true" />
                  <span>Xem nội dung liên quan</span>
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Sent Notification Detail Modal */}
      {selectedSent && (
        <div
          className="nt-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget) setSelectedSent(null)
          }}
        >
          <div
            className="nt-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="nt-sent-detail-title"
          >
            <div className="nt-modal-header">
              <div className="nt-modal-header-main">
                <div className="nt-modal-badges">
                  <span className={`nt-type-badge ${selectedSentMeta.badgeClass}`}>
                    {selectedSentMeta.label}
                  </span>
                  <span
                    className={`nt-progress-pill${
                      selectedSent.totalRecipients > 0 &&
                      selectedSent.readCount >= selectedSent.totalRecipients
                        ? ' nt-progress-pill--done'
                        : ''
                    }`}
                  >
                    Đã đọc {selectedSent.readCount}/{selectedSent.totalRecipients} người
                  </span>
                </div>

                <h2 id="nt-sent-detail-title" className="nt-modal-title">
                  {selectedSent.title}
                </h2>

                <div className="nt-modal-meta">
                  <span className="nt-modal-meta-item">
                    <Users size={13} aria-hidden="true" />
                    <span>{selectedSent.targetLabel}</span>
                  </span>
                  <span className="nt-modal-meta-item">
                    <Clock size={13} aria-hidden="true" />
                    <span>{formatDateTime(selectedSent.createdAt)}</span>
                  </span>
                </div>
              </div>

              <button
                type="button"
                className="nt-modal-close"
                aria-label="Đóng chi tiết thông báo đã gửi"
                onClick={() => setSelectedSent(null)}
              >
                <X size={18} />
              </button>
            </div>

            <div className="nt-modal-body">
              <p className="nt-modal-message">{selectedSent.message}</p>
            </div>

            <div className="nt-modal-footer">
              <button
                type="button"
                className="nt-btn nt-btn--subtle"
                onClick={() => setSelectedSent(null)}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {canSendNotification && (
        <NotificationComposerModal
          open={showComposer}
          role={role}
          notify={notify}
          onClose={() => setShowComposer(false)}
          onSent={() => {
            setFilter('sent')
            load(1, 'sent')
          }}
        />
      )}
    </section>
  )
}
