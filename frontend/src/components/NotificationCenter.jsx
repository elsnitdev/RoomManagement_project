import { useCallback, useEffect, useRef, useState } from 'react'
import {
  AlertCircle,
  BellOff,
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
  X,
} from 'lucide-react'
import { notificationApi } from '../services/api/notificationApi'
import { listProperties } from '../services/api/PropertyApi'
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
  if (item?.requestId || type.includes('request')) {
    return {
      label: 'Yêu cầu',
      badgeClass: 'nt-type-badge--request',
      Icon: MessageSquareText,
    }
  }
  return {
    label: 'Thông báo chung',
    badgeClass: 'nt-type-badge--general',
    Icon: Megaphone,
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

export default function NotificationCenter({ role, onReadChange }) {
  const [items, setItems] = useState([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [filter, setFilter] = useState('all')
  const [unreadCount, setUnreadCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [selectedNotification, setSelectedNotification] = useState(null)
  const [markingReadId, setMarkingReadId] = useState(null)
  const [markingAll, setMarkingAll] = useState(false)
  const markingReadRef = useRef(new Set())

  const [showComposer, setShowComposer] = useState(false)
  const [properties, setProperties] = useState([])
  const [propertyId, setPropertyId] = useState('')
  const [title, setTitle] = useState('')
  const [message, setMessage] = useState('')
  const [confirmingSend, setConfirmingSend] = useState(false)
  const [sending, setSending] = useState(false)

  const canBroadcast = role === 'Admin' || role === 'Manager'

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
      const unreadOnly = nextFilter === 'unread'
      const [response] = await Promise.all([
        notificationApi.list(nextPage, unreadOnly),
        fetchUnreadCount(),
      ])
      setItems(response.data || [])
      setTotalPages(Math.max(1, response.meta?.totalPages || 1))
      setPage(nextPage)
      onReadChange?.()
    } catch (e) {
      setError(e.message || 'Không thể tải thông báo.')
    } finally {
      setLoading(false)
    }
  }, [filter, fetchUnreadCount, onReadChange])

  useEffect(() => {
    load(1, 'all')
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (role !== 'Manager') return
    listProperties({ page: 1, pageSize: 100, isActive: true })
      .then((response) => {
        const rows = response.data || []
        setProperties(rows)
        setPropertyId(rows[0]?.id || '')
      })
      .catch((e) => setError(e.message || 'Không thể tải khu trọ.'))
  }, [role])

  useEffect(() => {
    if (!selectedNotification && !showComposer) return undefined

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    function handleKeyDown(event) {
      if (event.key !== 'Escape') return
      if (selectedNotification) {
        setSelectedNotification(null)
      } else if (showComposer && !sending) {
        setShowComposer(false)
        setConfirmingSend(false)
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [selectedNotification, showComposer, sending])

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

  function openDetail(item) {
    setSelectedNotification(item)
    if (!item.isRead) {
      markRead(item.id)
    }
  }

  async function markAllRead() {
    if (markingAll || unreadCount === 0) return
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

  function handleComposerSubmit(event) {
    event.preventDefault()
    if (!title.trim() || !message.trim()) return
    if (role === 'Manager' && !propertyId) return
    setConfirmingSend(true)
  }

  async function confirmBroadcast() {
    setSending(true)
    setError('')
    try {
      const body = {
        title: title.trim(),
        message: message.trim(),
        idempotencyKey: crypto.randomUUID(),
      }
      if (role === 'Admin') await notificationApi.adminBroadcast(body)
      else await notificationApi.managerBroadcast(propertyId, body)
      setTitle('')
      setMessage('')
      setConfirmingSend(false)
      setShowComposer(false)
      await load(1, filter)
    } catch (e) {
      setError(e.message || 'Không thể gửi thông báo.')
    } finally {
      setSending(false)
    }
  }

  function openRelated(item) {
    setSelectedNotification(null)
    const base = role === 'Tenant' ? 'tenant' : 'landlord'
    if (item.invoiceId) window.location.hash = `/${base}/billing`
    else if (item.contractId) window.location.hash = `/${base}/contracts`
    else if (item.requestId) window.location.hash = `/${base}/requests`
  }

  const hasRelatedLink = (item) => Boolean(item?.invoiceId || item?.contractId || item?.requestId)

  const selectedMeta = selectedNotification ? getNotificationMeta(selectedNotification) : null

  return (
    <section className="nt-page">
      <header className="nt-header">
        <div className="nt-header-copy">
          <span className="nt-eyebrow">TRUNG TÂM THÔNG BÁO</span>
          <h1 className="nt-title">Thông báo</h1>
          <p className="nt-subtitle">
            Theo dõi hóa đơn, hợp đồng, yêu cầu hỗ trợ và thông báo từ ban quản lý.
          </p>
        </div>

        {canBroadcast && (
          <button
            type="button"
            className="nt-btn nt-btn--primary"
            onClick={() => {
              setConfirmingSend(false)
              setShowComposer(true)
            }}
          >
            <Plus size={16} aria-hidden="true" />
            <span>Phát hành thông báo</span>
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
          </div>

          <button
            type="button"
            className="nt-btn nt-btn--ghost"
            disabled={loading || markingAll || unreadCount === 0}
            onClick={markAllRead}
          >
            <CheckCheck size={16} aria-hidden="true" />
            <span>{markingAll ? 'Đang xử lý…' : 'Đánh dấu tất cả đã đọc'}</span>
          </button>
        </div>

        {error && (
          <div className="nt-alert" role="alert">
            <AlertCircle size={16} aria-hidden="true" />
            <span>{error}</span>
          </div>
        )}

        {loading ? (
          <div className="nt-loading">Đang tải thông báo…</div>
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

                    <span className="nt-item-meta">
                      <Clock size={12} aria-hidden="true" />
                      <span>{formatDateTime(item.createdAt)}</span>
                    </span>
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
                : 'Các thông báo mới về hóa đơn, hợp đồng và yêu cầu sẽ hiển thị tại đây.'}
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

      {showComposer && canBroadcast && (
        <div
          className="nt-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target === event.currentTarget && !sending) {
              setShowComposer(false)
              setConfirmingSend(false)
            }
          }}
        >
          <div
            className="nt-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="nt-compose-title"
          >
            <div className="nt-modal-header">
              <div className="nt-modal-header-main">
                <span className="nt-eyebrow">PHÁT HÀNH THÔNG BÁO</span>
                <h2 id="nt-compose-title" className="nt-modal-title">
                  Gửi thông báo mới
                </h2>
              </div>
              <button
                type="button"
                className="nt-modal-close"
                aria-label="Đóng biểu mẫu phát hành thông báo"
                disabled={sending}
                onClick={() => {
                  setShowComposer(false)
                  setConfirmingSend(false)
                }}
              >
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleComposerSubmit}>
              <div className="nt-modal-body">
                <div className="nt-form">
                  {role === 'Manager' && (
                    <label className="nt-field">
                      <span className="nt-label">Khu trọ</span>
                      <select
                        className="nt-select"
                        value={propertyId}
                        onChange={(e) => {
                          setPropertyId(e.target.value)
                          setConfirmingSend(false)
                        }}
                        required
                      >
                        {properties.map((property) => (
                          <option key={property.id} value={property.id}>
                            {property.name}
                          </option>
                        ))}
                      </select>
                    </label>
                  )}

                  <label className="nt-field">
                    <span className="nt-label">Tiêu đề</span>
                    <input
                      className="nt-input"
                      value={title}
                      onChange={(e) => {
                        setTitle(e.target.value)
                        setConfirmingSend(false)
                      }}
                      maxLength={250}
                      placeholder="Nhập tiêu đề thông báo…"
                      required
                    />
                    <span className="nt-char-hint">{title.length}/250</span>
                  </label>

                  <label className="nt-field">
                    <span className="nt-label">Nội dung</span>
                    <textarea
                      className="nt-textarea"
                      value={message}
                      onChange={(e) => {
                        setMessage(e.target.value)
                        setConfirmingSend(false)
                      }}
                      maxLength={4000}
                      placeholder="Nhập nội dung chi tiết thông báo…"
                      required
                    />
                    <span className="nt-char-hint">{message.length}/4000</span>
                  </label>

                  {confirmingSend && (
                    <div className="nt-confirm-box">
                      <span>Phát hành thông báo tới tất cả người nhận trong phạm vi đã chọn?</span>
                      <div className="nt-confirm-actions">
                        <button
                          type="button"
                          className="nt-btn nt-btn--subtle"
                          disabled={sending}
                          onClick={() => setConfirmingSend(false)}
                        >
                          Hủy
                        </button>
                        <button
                          type="button"
                          className="nt-btn nt-btn--primary"
                          disabled={sending}
                          onClick={confirmBroadcast}
                        >
                          <Send size={14} aria-hidden="true" />
                          <span>{sending ? 'Đang gửi…' : 'Xác nhận gửi'}</span>
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              </div>

              <div className="nt-modal-footer">
                <button
                  type="button"
                  className="nt-btn nt-btn--subtle"
                  disabled={sending}
                  onClick={() => {
                    setShowComposer(false)
                    setConfirmingSend(false)
                  }}
                >
                  Đóng
                </button>
                {!confirmingSend && (
                  <button
                    type="submit"
                    className="nt-btn nt-btn--primary"
                    disabled={sending || (role === 'Manager' && !propertyId)}
                  >
                    <Send size={15} aria-hidden="true" />
                    <span>Phát hành</span>
                  </button>
                )}
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  )
}
