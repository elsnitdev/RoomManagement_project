import { useEffect, useState } from 'react'
import { listProperties } from '../services/api/PropertyApi'
import { requestApi } from '../services/api/requestApi'
import { Badge, Button, Empty, Field, Heading, Icon } from '../ui'

const statusInfo = {
  Open: { label: 'Chờ xử lý', badge: 'rejected' },
  InProgress: { label: 'Đang xử lý', badge: 'pending' },
  Resolved: { label: 'Đã giải quyết', badge: 'approved' },
  Closed: { label: 'Đã đóng', badge: 'vacant' },
}

const recipientTypeLabels = {
  Single: 'Một người thuê (SINGLE)',
  Multiple: 'Nhiều người thuê (MULTIPLE)',
  All: 'Toàn khu trọ (ALL)',
}

function formatDateTime(value) {
  if (!value) return '—'
  try {
    return new Intl.DateTimeFormat('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(value))
  } catch {
    return String(value)
  }
}

export default function LandlordRequestsPage({ notify }) {
  const [subTab, setSubTab] = useState('requests')
  const [properties, setProperties] = useState([])

  // Sub-tab 1: Tenant Requests
  const [filterPropertyId, setFilterPropertyId] = useState('')
  const [filterStatus, setFilterStatus] = useState('')
  const [filterRead, setFilterRead] = useState('')
  const [searchQuery, setSearchQuery] = useState('')
  const [requests, setRequests] = useState([])
  const [loadingRequests, setLoadingRequests] = useState(true)
  const [requestError, setRequestError] = useState('')
  const [selectedRequest, setSelectedRequest] = useState(null)
  const [updatingStatus, setUpdatingStatus] = useState(false)

  // Sub-tab 2: Announcements
  const [annPropertyId, setAnnPropertyId] = useState('')
  const [recipientType, setRecipientType] = useState('All')
  const [eligibleRecipients, setEligibleRecipients] = useState([])
  const [selectedUserIds, setSelectedUserIds] = useState([])
  const [annTitle, setAnnTitle] = useState('')
  const [annContent, setAnnContent] = useState('')
  const [sendingAnn, setSendingAnn] = useState(false)
  const [annError, setAnnError] = useState('')
  const [announcements, setAnnouncements] = useState([])
  const [loadingAnnouncements, setLoadingAnnouncements] = useState(false)

  useEffect(() => {
    listProperties({ page: 1, pageSize: 100, isActive: true })
      .then(res => {
        const list = res?.data || []
        setProperties(list)
        if (list.length > 0) {
          setAnnPropertyId(list[0].id)
        }
      })
      .catch(() => {})
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    setLoadingRequests(true)
    setRequestError('')
    requestApi
      .getLandlordRequests({
        propertyId: filterPropertyId || undefined,
        status: filterStatus || undefined,
        isRead: filterRead === '' ? undefined : filterRead === 'true',
        search: searchQuery || undefined,
        page: 1,
        pageSize: 50,
        signal: controller.signal,
      })
      .then(res => {
        const items = res?.data || []
        setRequests(items)
        if (selectedRequest) {
          const match = items.find(x => x.id === selectedRequest.id)
          if (match) setSelectedRequest(match)
        }
      })
      .catch(e => {
        if (!controller.signal.aborted) {
          setRequestError(e.message || 'Không thể tải danh sách yêu cầu.')
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoadingRequests(false)
        }
      })
    return () => controller.abort()
  }, [filterPropertyId, filterStatus, filterRead, searchQuery])

  useEffect(() => {
    if (!annPropertyId || subTab !== 'announcements') return
    const controller = new AbortController()
    setLoadingAnnouncements(true)
    Promise.all([
      requestApi.getAnnouncementRecipients(annPropertyId, { signal: controller.signal }),
      requestApi.getLandlordAnnouncements(annPropertyId, { page: 1, pageSize: 50, signal: controller.signal }),
    ])
      .then(([recList, annRes]) => {
        setEligibleRecipients(recList || [])
        setSelectedUserIds([])
        setAnnouncements(annRes?.data || [])
      })
      .catch(e => {
        if (!controller.signal.aborted) {
          setAnnError(e.message || 'Không thể tải dữ liệu thông báo của khu trọ.')
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoadingAnnouncements(false)
        }
      })
    return () => controller.abort()
  }, [annPropertyId, subTab])

  async function handleSelectRequest(item) {
    setSelectedRequest(item)
    try {
      const detail = await requestApi.getLandlordRequestById(item.id)
      setSelectedRequest(detail)
      setRequests(current => current.map(r => (r.id === detail.id ? detail : r)))
    } catch (e) {
      notify?.(e.message || 'Không thể tải chi tiết yêu cầu.')
    }
  }

  async function handleChangeStatus(requestId, newStatus) {
    setUpdatingStatus(true)
    try {
      const updated = await requestApi.updateLandlordRequestStatus(requestId, newStatus)
      setSelectedRequest(updated)
      setRequests(current => current.map(r => (r.id === updated.id ? updated : r)))
      notify?.(`Đã cập nhật trạng thái xử lý: ${statusInfo[newStatus]?.label || newStatus}.`)
    } catch (e) {
      notify?.(e.message || 'Không thể cập nhật trạng thái.')
    } finally {
      setUpdatingStatus(false)
    }
  }

  function toggleRecipient(userId) {
    if (recipientType === 'Single') {
      setSelectedUserIds([userId])
      return
    }
    setSelectedUserIds(current =>
      current.includes(userId) ? current.filter(id => id !== userId) : [...current, userId],
    )
  }

  async function handleSendAnnouncement(event) {
    event.preventDefault()
    if (!annPropertyId) {
      setAnnError('Vui lòng chọn khu trọ để gửi thông báo.')
      return
    }
    if (!annTitle.trim()) {
      setAnnError('Tiêu đề thông báo không được để trống.')
      return
    }
    if (!annContent.trim()) {
      setAnnError('Nội dung thông báo không được để trống.')
      return
    }
    if ((recipientType === 'Single' || recipientType === 'Multiple') && selectedUserIds.length === 0) {
      setAnnError('Vui lòng chọn người thuê nhận thông báo.')
      return
    }

    setSendingAnn(true)
    setAnnError('')
    try {
      const created = await requestApi.createLandlordAnnouncement(annPropertyId, {
        recipientType,
        receiverUserIds: recipientType === 'All' ? [] : selectedUserIds,
        title: annTitle.trim(),
        content: annContent.trim(),
      })
      setAnnouncements(current => [created, ...current])
      setAnnTitle('')
      setAnnContent('')
      setSelectedUserIds([])
      notify?.(`Đã phát hành thông báo tới ${created.totalRecipients} người thuê.`)
    } catch (e) {
      setAnnError(e.message || 'Không thể gửi thông báo.')
    } finally {
      setSendingAnn(false)
    }
  }

  return (
    <>
      <Heading
        eyebrow="TƯƠNG TÁC 2 CHIỀU"
        title="Yêu cầu & Thông báo"
        description="Tiếp nhận xử lý yêu cầu từ người thuê và phát hành thông báo chung cho khu trọ."
        action={
          <div className="segmented" role="tablist" aria-label="Chế độ quản lý">
            <button
              type="button"
              className={subTab === 'requests' ? 'selected' : ''}
              onClick={() => setSubTab('requests')}
            >
              Yêu cầu từ người thuê ({requests.length})
            </button>
            <button
              type="button"
              className={subTab === 'announcements' ? 'selected' : ''}
              onClick={() => setSubTab('announcements')}
            >
              Gửi thông báo khu trọ
            </button>
          </div>
        }
      />

      {subTab === 'requests' ? (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: selectedRequest ? 'minmax(0, 1.35fr) minmax(320px, 1fr)' : '1fr',
            gap: '18px',
            alignItems: 'start',
          }}
        >
          <section className="content-card">
            <div className="card-toolbar card-toolbar--wrap" style={{ marginBottom: '16px' }}>
              <label className="filter-select">
                <Icon name="building" size={16} />
                <select value={filterPropertyId} onChange={e => setFilterPropertyId(e.target.value)}>
                  <option value="">Tất cả khu trọ</option>
                  {properties.map(p => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
                <Icon name="down" size={14} />
              </label>

              <label className="filter-select">
                <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
                  <option value="">Mọi trạng thái xử lý</option>
                  <option value="Open">Chờ xử lý (Open)</option>
                  <option value="InProgress">Đang xử lý (InProgress)</option>
                  <option value="Resolved">Đã giải quyết (Resolved)</option>
                  <option value="Closed">Đã đóng (Closed)</option>
                </select>
                <Icon name="down" size={14} />
              </label>

              <label className="filter-select">
                <select value={filterRead} onChange={e => setFilterRead(e.target.value)}>
                  <option value="">Tất cả (Chưa đọc & Đã đọc)</option>
                  <option value="false">Chưa xem</option>
                  <option value="true">Đã xem</option>
                </select>
                <Icon name="down" size={14} />
              </label>

              <div className="search-box">
                <Icon name="search" size={17} />
                <input
                  placeholder="Tìm theo tiêu đề, tên, SĐT..."
                  value={searchQuery}
                  onChange={e => setSearchQuery(e.target.value)}
                />
              </div>
            </div>

            {loadingRequests ? (
              <Empty title="Đang tải danh sách yêu cầu..." description="Vui lòng chờ trong giây lát." />
            ) : requestError ? (
              <Empty title="Không thể tải yêu cầu" description={requestError} />
            ) : requests.length ? (
              <div className="request-grid">
                {requests.map(item => {
                  const st = statusInfo[item.status] || { label: item.status, badge: 'pending' }
                  const isSelected = selectedRequest?.id === item.id
                  return (
                    <article
                      className="request-card"
                      key={item.id}
                      onClick={() => handleSelectRequest(item)}
                      style={{
                        cursor: 'pointer',
                        border: isSelected ? '2px solid #2563eb' : undefined,
                        background: !item.isRead ? '#f8fbff' : undefined,
                      }}
                    >
                      <div className="request-top">
                        <span className="request-id">
                          {item.propertyName} · Phòng {item.roomName}
                        </span>
                        <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                          <Badge kind={item.isRead ? 'vacant' : 'rejected'}>
                            {item.isRead ? 'Đã xem' : 'Mới / Chưa xem'}
                          </Badge>
                          <Badge kind={st.badge}>{st.label}</Badge>
                        </div>
                      </div>
                      <h2>{item.title}</h2>
                      <p style={{ margin: '6px 0', color: '#475569' }}>
                        Người gửi: <strong>{item.senderFullName}</strong>
                        {item.senderPhoneNumber ? ` (${item.senderPhoneNumber})` : ''}
                      </p>
                      <p
                        style={{
                          fontSize: '13px',
                          color: '#64748b',
                          display: '-webkit-box',
                          WebkitLineClamp: 2,
                          WebkitBoxOrient: 'vertical',
                          overflow: 'hidden',
                        }}
                      >
                        {item.content}
                      </p>
                      <div className="request-bottom">
                        <span>Gửi lúc {formatDateTime(item.createdAt)}</span>
                        <button
                          type="button"
                          className="text-link"
                          onClick={e => {
                            e.stopPropagation()
                            handleSelectRequest(item)
                          }}
                        >
                          Xem & xử lý <Icon name="chevron" size={15} />
                        </button>
                      </div>
                    </article>
                  )
                })}
              </div>
            ) : (
              <Empty
                title="Không có yêu cầu nào"
                description="Hiện chưa có yêu cầu nào từ người thuê khớp với bộ lọc."
              />
            )}
          </section>

          {selectedRequest && (
            <aside className="content-card" style={{ position: 'sticky', top: '20px' }}>
              <div className="detail-header" style={{ marginBottom: '12px' }}>
                <div>
                  <span style={{ fontSize: '11px', fontWeight: 700, color: '#2563eb' }}>
                    CHI TIẾT YÊU CẦU
                  </span>
                  <h2 style={{ margin: '4px 0 0' }}>{selectedRequest.title}</h2>
                </div>
                <button
                  type="button"
                  className="icon-button"
                  aria-label="Đóng chi tiết"
                  onClick={() => setSelectedRequest(null)}
                >
                  <Icon name="close" />
                </button>
              </div>

              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginBottom: '14px' }}>
                <Badge kind={selectedRequest.isRead ? 'approved' : 'pending'}>
                  {selectedRequest.isRead
                    ? `Đã xem lúc ${formatDateTime(selectedRequest.readAt)}`
                    : 'Đang đánh dấu đã xem...'}
                </Badge>
                <Badge kind={statusInfo[selectedRequest.status]?.badge || 'pending'}>
                  {statusInfo[selectedRequest.status]?.label || selectedRequest.status}
                </Badge>
              </div>

              <div
                style={{
                  display: 'grid',
                  gap: '8px',
                  fontSize: '13px',
                  padding: '12px',
                  background: '#f8fafc',
                  borderRadius: '8px',
                  marginBottom: '14px',
                }}
              >
                <div>
                  <strong>Khu trọ:</strong> {selectedRequest.propertyName}
                </div>
                <div>
                  <strong>Phòng:</strong> {selectedRequest.roomName}
                </div>
                <div>
                  <strong>Người gửi:</strong> {selectedRequest.senderFullName}{' '}
                  {selectedRequest.senderPhoneNumber ? `· ${selectedRequest.senderPhoneNumber}` : ''}
                </div>
                <div>
                  <strong>Thời gian gửi:</strong> {formatDateTime(selectedRequest.createdAt)}
                </div>
              </div>

              <div style={{ marginBottom: '18px' }}>
                <strong style={{ display: 'block', fontSize: '12px', marginBottom: '6px', color: '#475569' }}>
                  NỘI DUNG CHI TIẾT
                </strong>
                <div
                  style={{
                    padding: '14px',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    whiteSpace: 'pre-wrap',
                    fontSize: '13px',
                    lineHeight: 1.6,
                  }}
                >
                  {selectedRequest.content}
                </div>
              </div>

              <div>
                <strong style={{ display: 'block', fontSize: '12px', marginBottom: '8px', color: '#475569' }}>
                  CẬP NHẬT TRẠNG THÁI XỬ LÝ
                </strong>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '8px' }}>
                  {['Open', 'InProgress', 'Resolved', 'Closed'].map(stKey => {
                    const active = selectedRequest.status === stKey
                    return (
                      <button
                        key={stKey}
                        type="button"
                        disabled={updatingStatus || active}
                        className={active ? 'button button--primary' : 'button'}
                        style={{
                          padding: '8px 10px',
                          fontSize: '12px',
                          opacity: active ? 1 : 0.9,
                        }}
                        onClick={() => handleChangeStatus(selectedRequest.id, stKey)}
                      >
                        {statusInfo[stKey]?.label || stKey}
                      </button>
                    )
                  })}
                </div>
              </div>
            </aside>
          )}
        </div>
      ) : (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'minmax(0, 1.05fr) minmax(0, 1.2fr)',
            gap: '18px',
            alignItems: 'start',
          }}
        >
          <section className="content-card">
            <h2 style={{ marginTop: 0, marginBottom: '6px' }}>Phát hành thông báo mới</h2>
            <p style={{ margin: '0 0 16px', fontSize: '13px', color: '#64748b' }}>
              Nội dung thông báo được lưu một lần và tự động phân phối tới danh sách người thuê đang ở hợp đồng hiệu lực.
            </p>

            <form onSubmit={handleSendAnnouncement} className="form-grid">
              <Field label="Khu trọ áp dụng" required>
                <select
                  value={annPropertyId}
                  onChange={e => setAnnPropertyId(e.target.value)}
                  required
                >
                  <option value="">-- Chọn khu trọ --</option>
                  {properties.map(p => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </Field>

              <Field label="Phạm vi người nhận" required>
                <select
                  value={recipientType}
                  onChange={e => {
                    setRecipientType(e.target.value)
                    setSelectedUserIds([])
                  }}
                >
                  <option value="All">Toàn bộ người thuê trong khu trọ (ALL)</option>
                  <option value="Single">Một người thuê cụ thể (SINGLE)</option>
                  <option value="Multiple">Nhiều người thuê được chọn (MULTIPLE)</option>
                </select>
              </Field>

              {(recipientType === 'Single' || recipientType === 'Multiple') && (
                <div style={{ gridColumn: '1 / -1' }}>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, marginBottom: '8px' }}>
                    Chọn người thuê đang ở ({selectedUserIds.length} đã chọn)
                  </label>
                  {eligibleRecipients.length ? (
                    <div
                      style={{
                        maxHeight: '210px',
                        overflowY: 'auto',
                        border: '1px solid #cbd5e1',
                        borderRadius: '8px',
                        padding: '8px',
                        display: 'grid',
                        gap: '6px',
                      }}
                    >
                      {eligibleRecipients.map(rec => {
                        const checked = selectedUserIds.includes(rec.userId)
                        return (
                          <label
                            key={rec.userId}
                            style={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: '10px',
                              padding: '8px 10px',
                              borderRadius: '6px',
                              background: checked ? '#eff6ff' : '#fff',
                              cursor: 'pointer',
                              fontSize: '13px',
                            }}
                          >
                            <input
                              type={recipientType === 'Single' ? 'radio' : 'checkbox'}
                              name="announcementRecipient"
                              checked={checked}
                              onChange={() => toggleRecipient(rec.userId)}
                            />
                            <span>
                              <strong>Phòng {rec.roomNumber}</strong> · {rec.fullName}
                              {rec.phoneNumber ? ` (${rec.phoneNumber})` : ''}
                            </span>
                          </label>
                        )
                      })}
                    </div>
                  ) : (
                    <p style={{ fontSize: '12px', color: '#b91c1c', margin: 0 }}>
                      Khu trọ này hiện chưa có người thuê nào có hợp đồng Active đã liên kết tài khoản.
                    </p>
                  )}
                </div>
              )}

              <div style={{ gridColumn: '1 / -1' }}>
                <Field label="Tiêu đề thông báo" required>
                  <input
                    required
                    maxLength={250}
                    value={annTitle}
                    onChange={e => {
                      setAnnTitle(e.target.value)
                      setAnnError('')
                    }}
                    placeholder="Ví dụ: Lịch bảo trì hệ thống điện nước định kỳ"
                  />
                </Field>
              </div>

              <div style={{ gridColumn: '1 / -1' }}>
                <Field label="Nội dung thông báo" required>
                  <textarea
                    required
                    maxLength={4000}
                    rows={5}
                    style={{
                      width: '100%',
                      padding: '10px 12px',
                      borderRadius: '8px',
                      border: '1px solid #cbd5e1',
                      font: 'inherit',
                    }}
                    value={annContent}
                    onChange={e => {
                      setAnnContent(e.target.value)
                      setAnnError('')
                    }}
                    placeholder="Nhập nội dung thông báo gửi đến người thuê..."
                  />
                </Field>
              </div>

              {annError && (
                <p style={{ gridColumn: '1 / -1', color: '#dc2626', fontSize: '13px', margin: 0 }}>
                  {annError}
                </p>
              )}

              <div className="form-action" style={{ gridColumn: '1 / -1' }}>
                <Button type="submit" disabled={sendingAnn}>
                  {sendingAnn ? 'Đang gửi...' : 'Phát hành thông báo'}
                </Button>
              </div>
            </form>
          </section>

          <section className="content-card">
            <div className="card-title-row" style={{ marginBottom: '14px' }}>
              <div>
                <h2>Lịch sử thông báo đã phát hành</h2>
                <p>Theo dõi tiến độ đọc của người thuê trong khu trọ.</p>
              </div>
              <span>{announcements.length} thông báo</span>
            </div>

            {loadingAnnouncements ? (
              <Empty title="Đang tải lịch sử thông báo..." description="Vui lòng chờ giây lát." />
            ) : announcements.length ? (
              <div style={{ display: 'grid', gap: '12px' }}>
                {announcements.map(ann => (
                  <article
                    key={ann.id}
                    style={{
                      padding: '14px 16px',
                      border: '1px solid #e2e8f0',
                      borderRadius: '10px',
                      background: '#fff',
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        gap: '10px',
                        alignItems: 'center',
                        marginBottom: '6px',
                      }}
                    >
                      <strong style={{ fontSize: '15px' }}>{ann.title}</strong>
                      <Badge kind={ann.totalRecipients > 0 && ann.readCount >= ann.totalRecipients ? 'approved' : 'pending'}>
                        Đã đọc {ann.readCount}/{ann.totalRecipients} người
                      </Badge>
                    </div>
                    <p
                      style={{
                        margin: '6px 0 10px',
                        fontSize: '13px',
                        color: '#334155',
                        whiteSpace: 'pre-wrap',
                      }}
                    >
                      {ann.content}
                    </p>
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        fontSize: '12px',
                        color: '#64748b',
                      }}
                    >
                      <span>{recipientTypeLabels[ann.recipientType] || ann.recipientType}</span>
                      <span>Gửi lúc {formatDateTime(ann.createdAt)}</span>
                    </div>
                  </article>
                ))}
              </div>
            ) : (
              <Empty
                title="Chưa có thông báo nào"
                description="Khi bạn phát hành thông báo cho khu trọ, lịch sử và thống kê số người đã đọc sẽ hiển thị tại đây."
              />
            )}
          </section>
        </div>
      )}
    </>
  )
}

