import { useEffect, useState } from 'react'
import {
  AlertCircle,
  Check,
  CheckCircle2,
  Clock,
  Eye,
  Home,
  RefreshCw,
  Search,
  UserCheck,
  X,
} from 'lucide-react'
import { Empty, Heading } from '../ui'
import { useApiQuery } from '../hooks/useApiQuery'
import { apiRequest } from '../services/apiClient'
import { invalidateApiCache } from '../services/apiCache'
import '../styles/admin-users.css'

const base = '/api/admin/user-reviews'
const labels = {
  Pending: 'Đang chờ',
  Approved: 'Đã duyệt',
  Rejected: 'Đã từ chối',
  Unverified: 'Chưa xác minh',
  Verified: 'Đã xác minh',
  approve: 'Duyệt tài khoản',
  reject: 'Từ chối đăng ký',
  start: 'Bắt đầu đối chiếu',
  verify: 'Xác nhận đã đối chiếu',
  rejectVerification: 'Đối chiếu không đạt',
  revoke: 'Thu hồi xác minh',
  identityChanged: 'Thông tin định danh đã thay đổi',
}

function getInitials(name = '') {
  return (
    name
      .trim()
      .split(/\s+/)
      .slice(-2)
      .map((word) => word[0] || '')
      .join('')
      .toUpperCase() || 'U'
  )
}

function StatusBadge({ value }) {
  if (value === 'Approved' || value === 'Verified') {
    return (
      <span className="admin-badge admin-badge--verified">
        <CheckCircle2 size={12} /> {labels[value]}
      </span>
    )
  }
  if (value === 'Pending') {
    return (
      <span className="admin-badge admin-badge--pending">
        <Clock size={12} /> {labels[value]}
      </span>
    )
  }
  if (value === 'Rejected') {
    return (
      <span className="admin-badge admin-badge--locked">
        <X size={12} /> {labels[value]}
      </span>
    )
  }
  return (
    <span className="admin-badge admin-badge--unverified">
      <AlertCircle size={12} /> {labels[value] || value || '—'}
    </span>
  )
}

export default function AdminUserReviews({
  kind,
  notify,
  onChange,
  initialId,
  openIdentity,
}) {
  const approval = kind === 'approval'
  const [status, setStatus] = useState(approval ? 'Pending' : '')
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const [id, setId] = useState(initialId || null)
  const [historyPage, setHistoryPage] = useState(1)
  const [action, setAction] = useState('')
  const [reason, setReason] = useState('')
  const [method, setMethod] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const query = new URLSearchParams({
    kind,
    status,
    page,
    keyword: keyword.trim(),
  })
  const list = useApiQuery(
    `admin:reviews:list:${query}`,
    () => apiRequest(`${base}?${query}`),
  )
  const path = `${base}/${encodeURIComponent(id)}`
  const detail = useApiQuery(
    `admin:reviews:detail:${id}`,
    () => apiRequest(path),
    { enabled: !!id, ttlMs: 0 },
  )
  const history = useApiQuery(
    `admin:reviews:history:${id}:${historyPage}`,
    () => apiRequest(`${path}/history?page=${historyPage}`),
    { enabled: !!id, ttlMs: 0 },
  )
  const user = detail.data?.data
  const rows = list.data?.data || []
  const meta = list.data?.meta

  const statusTabs = approval
    ? [
        { val: 'Pending', label: 'Đang chờ' },
        { val: 'Approved', label: 'Đã duyệt' },
        { val: 'Rejected', label: 'Đã từ chối' },
        { val: '', label: 'Tất cả' },
      ]
    : [
        { val: '', label: 'Tất cả' },
        { val: 'Unverified', label: 'Chưa xác minh' },
        { val: 'Pending', label: 'Đang chờ' },
        { val: 'Verified', label: 'Đã xác minh' },
      ]

  const actions = approval
    ? user?.approvalStatus === 'Pending'
      ? ['approve', 'reject']
      : []
    : {
        Unverified: ['start'],
        Pending: ['verify', 'rejectVerification'],
        Verified: ['revoke'],
      }[user?.identityVerificationStatus] || []
  const needsReason = ['reject', 'verify', 'rejectVerification', 'revoke'].includes(action)

  useEffect(() => {
    if (!id) return
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const onKeyDown = (event) => {
      if (event.key === 'Escape' && !busy) {
        select(null)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [id, busy])

  function select(nextId) {
    setId(nextId)
    setHistoryPage(1)
    setAction('')
    setError('')
    setReason('')
    setMethod('')
  }

  async function submit(event) {
    event.preventDefault()
    if (!user || busy) return
    setBusy(true)
    setError('')
    try {
      await apiRequest(`${path}/decision`, {
        method: 'POST',
        body: {
          action,
          expectedVersion: user.reviewVersion,
          reason,
          verificationMethod: method,
        },
      })
      invalidateApiCache('admin:reviews')
      invalidateApiCache('admin:users')
      invalidateApiCache('admin:pending-landlords')
      list.reload()
      detail.reload()
      setHistoryPage(1)
      history.reload()
      onChange()
      setAction('')
      setReason('')
      setMethod('')
      notify('Đã lưu kết quả xử lý hồ sơ.')
    } catch (err) {
      setError(err.message)
      if (err.status === 409) {
        detail.reload()
        history.reload()
        list.reload()
        onChange()
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="admin-users-page">
      <Heading
        eyebrow="KIỂM DUYỆT HỒ SƠ"
        title={approval ? 'Duyệt hồ sơ chủ trọ' : 'Xác minh danh tính'}
        description={
          approval
            ? 'Xem xét và phê duyệt tài khoản chủ trọ đăng ký mới trước khi cho phép truy cập hệ thống.'
            : 'Đối chiếu thông tin định danh (CCCD/SĐT) và theo dõi lịch sử xác minh của người dùng.'
        }
      />

      <section className="admin-toolbar-card">
        <div className="admin-toolbar">
          <div
            className="admin-role-pills"
            role="tablist"
            aria-label="Lọc theo trạng thái hồ sơ"
          >
            {statusTabs.map(({ val, label }) => (
              <button
                key={val || 'all'}
                type="button"
                role="tab"
                aria-selected={status === val}
                className={`admin-role-pill ${status === val ? 'is-active' : ''}`}
                onClick={() => {
                  setStatus(val)
                  setPage(1)
                }}
              >
                <span>{label}</span>
              </button>
            ))}
          </div>

          <div className="admin-toolbar__controls">
            <div className="admin-search-wrap">
              <Search className="admin-search-icon" size={16} />
              <input
                className="admin-search-input"
                aria-label="Tìm hồ sơ"
                placeholder="Tên, email, SĐT hoặc CCCD..."
                value={keyword}
                onChange={(e) => {
                  setKeyword(e.target.value)
                  setPage(1)
                }}
              />
              {keyword && (
                <button
                  type="button"
                  className="admin-search-clear"
                  aria-label="Xóa từ khóa"
                  onClick={() => {
                    setKeyword('')
                    setPage(1)
                  }}
                >
                  <X size={13} />
                </button>
              )}
            </div>

            <button
              type="button"
              className="admin-btn-action-refresh"
              title="Làm mới danh sách"
              aria-label="Làm mới danh sách hồ sơ"
              onClick={list.reload}
              disabled={list.isLoading}
            >
              <RefreshCw
                size={16}
                className={list.isLoading ? 'spin' : ''}
              />
            </button>
          </div>
        </div>
      </section>

      <section className="admin-table-card">
        {list.isLoading ? (
          <div style={{ padding: '48px 24px', textAlign: 'center' }}>
            <p className="muted-copy" role="status">
              Đang tải danh sách hồ sơ…
            </p>
          </div>
        ) : list.error ? (
          <div style={{ padding: '24px' }}>
            <p className="form-error" role="alert">
              {list.error.message}
            </p>
            <button
              type="button"
              className="admin-btn admin-btn--edit"
              onClick={list.reload}
            >
              Thử lại
            </button>
          </div>
        ) : rows.length ? (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Tài khoản</th>
                  <th>Vai trò</th>
                  <th>Trạng thái</th>
                  <th style={{ textAlign: 'right' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const rowStatus = approval
                    ? row.approvalStatus
                    : row.identityVerificationStatus
                  return (
                    <tr key={row.id}>
                      <td>
                        <div className="admin-user-cell">
                          <span className="admin-avatar">
                            {getInitials(row.fullName || row.userName || 'U')}
                          </span>
                          <div className="admin-user-info">
                            <span className="admin-user-name">
                              {row.fullName || row.userName}
                            </span>
                            <span className="admin-user-handle">
                              @{row.userName}
                              {row.email ? ` · ${row.email}` : ''}
                            </span>
                          </div>
                        </div>
                      </td>
                      <td>
                        {row.role === 'Landlord' ? (
                          <span className="admin-badge admin-badge--role-landlord">
                            <Home size={12} /> Chủ trọ
                          </span>
                        ) : (
                          <span className="admin-badge admin-badge--role-tenant">
                            <UserCheck size={12} /> Người thuê
                          </span>
                        )}
                      </td>
                      <td>
                        <StatusBadge value={rowStatus} />
                      </td>
                      <td>
                        <div className="admin-actions-group">
                          <button
                            type="button"
                            className="admin-btn admin-btn--edit"
                            disabled={busy}
                            onClick={() => select(row.id)}
                          >
                            <Eye size={13} />
                            <span>Xem hồ sơ</span>
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        ) : (
          <div style={{ padding: '48px 20px' }}>
            <Empty
              icon="users"
              title="Không tìm thấy hồ sơ"
              description="Không có hồ sơ nào khớp với trạng thái lọc hoặc từ khóa hiện tại."
            />
          </div>
        )}

        {meta && (page > 1 || meta.totalPages > 1) && (
          <div className="admin-pagination">
            <span>
              Trang {page}
              {meta.totalPages > 0 && ` / ${meta.totalPages}`}
            </span>
            <div className="admin-pagination__controls">
              <button
                type="button"
                className="admin-pagination__btn"
                disabled={list.isLoading || page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Trang trước
              </button>
              <button
                type="button"
                className="admin-pagination__btn"
                disabled={
                  list.isLoading || !meta.totalPages || page >= meta.totalPages
                }
                onClick={() => setPage((p) => p + 1)}
              >
                Trang sau
              </button>
            </div>
          </div>
        )}
      </section>

      {id && (
        <div
          className="admin-modal-backdrop"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget && !busy) select(null)
          }}
        >
          <div
            className="admin-modal admin-modal--wide"
            role="dialog"
            aria-modal="true"
            aria-labelledby="review-modal-title"
          >
            <header className="admin-modal__header">
              <div className="admin-review-head-meta">
                <span className="admin-avatar">
                  {getInitials(
                    user?.fullName || user?.userName || 'U',
                  )}
                </span>
                <div>
                  <h2 id="review-modal-title">
                    {user?.fullName || user?.userName || 'Chi tiết hồ sơ'}
                  </h2>
                  {user?.id === id && (
                    <div className="admin-review-badges">
                      <StatusBadge
                        value={
                          approval
                            ? user.approvalStatus
                            : user.identityVerificationStatus
                        }
                      />
                    </div>
                  )}
                </div>
              </div>
              <button
                type="button"
                className="admin-modal__close"
                aria-label="Đóng hồ sơ"
                disabled={busy}
                onClick={() => select(null)}
              >
                <X size={18} />
              </button>
            </header>

            <div className="admin-modal__body">
              {detail.isLoading && (
                <p className="muted-copy" role="status">
                  Đang tải chi tiết hồ sơ…
                </p>
              )}
              {detail.error && (
                <div className="admin-modal-error" role="alert">
                  <AlertCircle size={16} />
                  <span>{detail.error.message}</span>
                  <button
                    type="button"
                    className="admin-btn admin-btn--edit"
                    onClick={detail.reload}
                  >
                    Thử lại
                  </button>
                </div>
              )}

              {user?.id === id && (
                <>
                  <div className="admin-review-grid">
                    <div className="admin-review-field">
                      <span>Email</span>
                      <strong>{user.email || '—'}</strong>
                    </div>
                    <div className="admin-review-field">
                      <span>Số điện thoại</span>
                      <strong>{user.phoneNumber || '—'}</strong>
                    </div>
                    <div className="admin-review-field">
                      <span>CCCD / CMND</span>
                      <strong>{user.citizenId || '—'}</strong>
                    </div>
                    <div className="admin-review-field">
                      <span>Ngày sinh</span>
                      <strong>{user.dateOfBirth || '—'}</strong>
                    </div>
                    <div className="admin-review-field admin-review-field--wide">
                      <span>Địa chỉ</span>
                      <strong>{user.address || '—'}</strong>
                    </div>
                    <div className="admin-review-field">
                      <span>Duyệt tài khoản</span>
                      <strong>{labels[user.approvalStatus] || '—'}</strong>
                    </div>
                    <div className="admin-review-field">
                      <span>Xác minh danh tính</span>
                      <strong>
                        {labels[user.identityVerificationStatus] || '—'}
                      </strong>
                    </div>
                    <div className="admin-review-field">
                      <span>Quyền đăng nhập</span>
                      <strong>
                        {user.isActive && user.approvalStatus === 'Approved'
                          ? 'Được phép'
                          : 'Chưa được phép'}
                      </strong>
                    </div>
                    {user.rejectionReason && (
                      <div className="admin-review-field admin-review-field--wide">
                        <span>Lý do từ chối đăng ký</span>
                        <strong>{user.rejectionReason}</strong>
                      </div>
                    )}
                  </div>

                  <div className="admin-review-actions">
                    {actions.map((value) => {
                      const isApprove =
                        value === 'approve' || value === 'verify'
                      const isReject =
                        value === 'reject' ||
                        value === 'rejectVerification' ||
                        value === 'revoke'
                      return (
                        <button
                          key={value}
                          type="button"
                          className={`admin-btn ${
                            isApprove
                              ? 'admin-btn--approve'
                              : isReject
                                ? 'admin-btn--reject'
                                : 'admin-btn--edit'
                          }`}
                          disabled={busy}
                          onClick={() => {
                            setAction(value)
                            setError('')
                          }}
                        >
                          {isApprove && <Check size={14} />}
                          {isReject && <X size={14} />}
                          <span>{labels[value]}</span>
                        </button>
                      )
                    })}
                    {approval && (
                      <button
                        type="button"
                        className="admin-btn admin-btn--edit"
                        disabled={busy}
                        onClick={() => {
                          select(null)
                          openIdentity(id)
                        }}
                      >
                        Mở xác minh danh tính
                      </button>
                    )}
                  </div>

                  {actions.includes(action) && (
                    <form className="admin-decision-box" onSubmit={submit}>
                      {action === 'verify' && (
                        <div className="admin-field">
                          <label htmlFor="review-verify-method">
                            Phương thức đối chiếu *
                          </label>
                          <input
                            id="review-verify-method"
                            required
                            maxLength={100}
                            value={method}
                            onChange={(e) => setMethod(e.target.value)}
                            placeholder="Ví dụ: Đối chiếu CCCD gốc / Zalo video"
                          />
                        </div>
                      )}
                      {needsReason && (
                        <div className="admin-field">
                          <label htmlFor="review-reason">
                            {action === 'verify'
                              ? 'Ghi chú kết quả *'
                              : 'Lý do *'}
                          </label>
                          <textarea
                            id="review-reason"
                            rows={3}
                            required
                            maxLength={500}
                            value={reason}
                            onChange={(e) => setReason(e.target.value)}
                            placeholder="Nhập nội dung rõ ràng để lưu vào lịch sử..."
                          />
                        </div>
                      )}
                      {action === 'approve' && (
                        <p className="admin-decision-box__note">
                          Tài khoản sẽ được phép đăng nhập với vai trò chủ trọ ngay sau khi duyệt.
                        </p>
                      )}
                      <div className="admin-decision-box__actions">
                        <button
                          type="button"
                          className="admin-btn admin-btn--edit"
                          disabled={busy}
                          onClick={() => setAction('')}
                        >
                          Hủy
                        </button>
                        <button
                          type="submit"
                          className={`admin-btn ${
                            action === 'reject' ||
                            action === 'rejectVerification' ||
                            action === 'revoke'
                              ? 'admin-btn--reject'
                              : 'admin-btn--approve'
                          }`}
                          disabled={
                            busy ||
                            (needsReason && !reason.trim()) ||
                            (action === 'verify' && !method.trim())
                          }
                        >
                          {busy ? 'Đang lưu…' : `Xác nhận — ${labels[action]}`}
                        </button>
                      </div>
                    </form>
                  )}

                  {error && (
                    <div className="admin-modal-error" role="alert">
                      <AlertCircle size={16} />
                      <span>{error}</span>
                    </div>
                  )}

                  <div className="admin-timeline-section">
                    <h3>Lịch sử xử lý</h3>
                    {history.isLoading && (
                      <p className="muted-copy" role="status">
                        Đang tải lịch sử…
                      </p>
                    )}
                    {history.error && (
                      <p className="form-error" role="alert">
                        {history.error.message}
                      </p>
                    )}
                    {history.data?.data.length === 0 && (
                      <p className="muted-copy">Chưa có lịch sử xử lý.</p>
                    )}
                    {history.data?.data.length > 0 && (
                      <ul className="admin-timeline">
                        {history.data.data.map((entry) => (
                          <li className="admin-timeline__item" key={entry.id}>
                            <span className="admin-timeline__dot" />
                            <div className="admin-timeline__head">
                              <strong>
                                {labels[entry.action] || entry.action}
                              </strong>
                              <span className="admin-timeline__meta">
                                · {entry.actor || entry.actorAdminUserId || 'Hệ thống'} ·{' '}
                                {new Date(
                                  entry.createdAt.endsWith('Z')
                                    ? entry.createdAt
                                    : `${entry.createdAt}Z`,
                                ).toLocaleString('vi-VN')}
                              </span>
                            </div>
                            {entry.verificationMethod && (
                              <p className="admin-timeline__note">
                                Phương thức: {entry.verificationMethod}
                              </p>
                            )}
                            {entry.reason && (
                              <p className="admin-timeline__note">
                                {entry.reason}
                              </p>
                            )}
                          </li>
                        ))}
                      </ul>
                    )}
                    {history.data?.meta &&
                      (historyPage > 1 || history.data.meta.totalPages > 1) && (
                        <div
                          className="admin-pagination"
                          style={{ marginTop: 12, borderRadius: 8 }}
                        >
                          <span>
                            Trang {historyPage} / {history.data.meta.totalPages}
                          </span>
                          <div className="admin-pagination__controls">
                            <button
                              type="button"
                              className="admin-pagination__btn"
                              disabled={historyPage <= 1}
                              onClick={() => setHistoryPage((p) => p - 1)}
                            >
                              Trước
                            </button>
                            <button
                              type="button"
                              className="admin-pagination__btn"
                              disabled={
                                !history.data.meta.totalPages ||
                                historyPage >= history.data.meta.totalPages
                              }
                              onClick={() => setHistoryPage((p) => p + 1)}
                            >
                              Sau
                            </button>
                          </div>
                        </div>
                      )}
                  </div>
                </>
              )}
            </div>

            <footer className="admin-modal__footer">
              <button
                type="button"
                className="admin-btn admin-btn--edit"
                disabled={busy}
                onClick={() => select(null)}
              >
                Đóng hồ sơ
              </button>
            </footer>
          </div>
        </div>
      )}
    </div>
  )
}
