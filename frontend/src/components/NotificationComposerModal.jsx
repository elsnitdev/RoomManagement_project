import { useEffect, useMemo, useRef, useState } from 'react'
import {
  AlertCircle,
  AlertTriangle,
  Building2,
  Check,
  Globe,
  RefreshCw,
  Search,
  Send,
  UserRound,
  Users,
  X,
} from 'lucide-react'
import { notificationApi } from '../services/api/notificationApi'
import { removeVietnameseAccents } from './common/SearchableSelect'

function createUuid() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (ch) => {
    const r = Math.floor(Math.random() * 16)
    const v = ch === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

export default function NotificationComposerModal({ open, role, onClose, onSent, notify }) {
  const isAdmin = role === 'Admin'

  const [scope, setScope] = useState(isAdmin ? 'AllSystem' : 'AllActiveTenants')
  const [targetsLoading, setTargetsLoading] = useState(false)
  const [targetsError, setTargetsError] = useState('')

  // Admin target state
  const [adminTargets, setAdminTargets] = useState({
    totalActiveSystemUsers: 0,
    totalActiveLandlords: 0,
    landlords: [],
  })
  const [selectedLandlordId, setSelectedLandlordId] = useState('')
  const [landlordSearch, setLandlordSearch] = useState('')

  // Operator (Landlord / Manager) target state
  const [operatorTargets, setOperatorTargets] = useState({
    totalActiveTenants: 0,
    properties: [],
    tenants: [],
  })
  const [selectedPropertyId, setSelectedPropertyId] = useState('')
  const [propertySearch, setPropertySearch] = useState('')
  const [selectedTenantId, setSelectedTenantId] = useState('')
  const [tenantPropertyFilter, setTenantPropertyFilter] = useState('')
  const [tenantSearch, setTenantSearch] = useState('')

  // Message state
  const [title, setTitle] = useState('')
  const [message, setMessage] = useState('')
  const [formError, setFormError] = useState('')
  const [confirmingSend, setConfirmingSend] = useState(false)
  const [sending, setSending] = useState(false)

  const idempotencyKeyRef = useRef(null)

  const invalidateDraftState = () => {
    idempotencyKeyRef.current = null
    setConfirmingSend(false)
    setFormError('')
  }

  const fetchTargets = useMemo(
    () => async (signal) => {
      setTargetsLoading(true)
      setTargetsError('')
      try {
        if (isAdmin) {
          const res = await notificationApi.getAdminTargets('', { signal })
          const data = res?.data || {}
          setAdminTargets({
            totalActiveSystemUsers: data.totalActiveSystemUsers ?? 0,
            totalActiveLandlords: data.totalActiveLandlords ?? 0,
            landlords: data.landlords || [],
          })
        } else {
          const res = await notificationApi.getOperatorTargets('', { signal })
          const data = res?.data || {}
          const props = data.properties || []
          setOperatorTargets({
            totalActiveTenants: data.totalActiveTenants ?? 0,
            properties: props,
            tenants: data.tenants || [],
          })
          setSelectedPropertyId((prev) =>
            prev && props.some((p) => p.propertyId === prev) ? prev : props[0]?.propertyId || ''
          )
        }
      } catch (e) {
        if (!signal?.aborted) {
          setTargetsError(e.message || 'Không thể tải danh sách đối tượng nhận thông báo.')
        }
      } finally {
        if (!signal?.aborted) {
          setTargetsLoading(false)
        }
      }
    },
    [isAdmin]
  )

  useEffect(() => {
    if (!open) return undefined

    setScope(isAdmin ? 'AllSystem' : 'AllActiveTenants')
    setSelectedLandlordId('')
    setLandlordSearch('')
    setSelectedPropertyId('')
    setPropertySearch('')
    setSelectedTenantId('')
    setTenantPropertyFilter('')
    setTenantSearch('')
    setTitle('')
    setMessage('')
    setFormError('')
    setConfirmingSend(false)
    idempotencyKeyRef.current = null

    const controller = new AbortController()
    fetchTargets(controller.signal)
    return () => controller.abort()
  }, [open, isAdmin, fetchTargets])

  useEffect(() => {
    if (!open) return undefined
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    function handleKeyDown(event) {
      if (event.key === 'Escape' && !sending) {
        onClose?.()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [open, sending, onClose])

  const filteredLandlords = useMemo(() => {
    const q = removeVietnameseAccents(landlordSearch)
    if (!q) return adminTargets.landlords
    return adminTargets.landlords.filter((l) => {
      const haystack = [l.displayName, l.userName, l.landlordCode, l.email, l.phoneNumber]
        .filter(Boolean)
        .join(' ')
      return removeVietnameseAccents(haystack).includes(q)
    })
  }, [adminTargets.landlords, landlordSearch])

  const filteredProperties = useMemo(() => {
    const q = removeVietnameseAccents(propertySearch)
    if (!q) return operatorTargets.properties
    return operatorTargets.properties.filter((p) => {
      const haystack = [p.name, p.address].filter(Boolean).join(' ')
      return removeVietnameseAccents(haystack).includes(q)
    })
  }, [operatorTargets.properties, propertySearch])

  const filteredTenants = useMemo(() => {
    const q = removeVietnameseAccents(tenantSearch)
    return operatorTargets.tenants.filter((t) => {
      if (
        tenantPropertyFilter &&
        !(t.activeStays || []).some((s) => s.propertyId === tenantPropertyFilter)
      ) {
        return false
      }
      if (!q) return true
      const stayText = (t.activeStays || [])
        .map((s) => `${s.propertyName} ${s.roomNumber}`)
        .join(' ')
      const haystack = [t.fullName, t.phoneNumber, t.email, stayText].filter(Boolean).join(' ')
      return removeVietnameseAccents(haystack).includes(q)
    })
  }, [operatorTargets.tenants, tenantPropertyFilter, tenantSearch])

  const selectedLandlord = useMemo(
    () => adminTargets.landlords.find((l) => l.userId === selectedLandlordId) || null,
    [adminTargets.landlords, selectedLandlordId]
  )

  const selectedProperty = useMemo(
    () => operatorTargets.properties.find((p) => p.propertyId === selectedPropertyId) || null,
    [operatorTargets.properties, selectedPropertyId]
  )

  const selectedTenant = useMemo(
    () => operatorTargets.tenants.find((t) => t.userId === selectedTenantId) || null,
    [operatorTargets.tenants, selectedTenantId]
  )

  const audienceSummary = useMemo(() => {
    if (isAdmin) {
      if (scope === 'AllSystem') {
        const count = adminTargets.totalActiveSystemUsers
        return {
          label: 'Toàn bộ người dùng đang hoạt động trên hệ thống',
          count,
          canSend: count > 0,
          warning:
            !targetsLoading && count === 0
              ? 'Hiện chưa có tài khoản nào đang hoạt động để nhận thông báo.'
              : '',
        }
      }
      if (scope === 'AllLandlords') {
        const count = adminTargets.totalActiveLandlords
        return {
          label: 'Tất cả chủ trọ đã được duyệt và đang hoạt động',
          count,
          canSend: count > 0,
          warning:
            !targetsLoading && count === 0
              ? 'Hiện chưa có chủ trọ nào đang hoạt động để nhận thông báo.'
              : '',
        }
      }
      return selectedLandlord
        ? {
            label: `Chủ trọ: ${selectedLandlord.displayName} (@${selectedLandlord.userName})`,
            count: 1,
            canSend: true,
            warning: '',
          }
        : {
            label: 'Chưa chọn chủ trọ cụ thể',
            count: 0,
            canSend: false,
            warning: 'Vui lòng chọn 1 chủ trọ từ danh sách bên trên.',
          }
    }

    if (scope === 'AllActiveTenants') {
      const count = operatorTargets.totalActiveTenants
      return {
        label: 'Tất cả người thuê có hợp đồng đang hoạt động',
        count,
        canSend: count > 0,
        warning:
          !targetsLoading && count === 0
            ? 'Hiện chưa có người thuê nào có hợp đồng đang hoạt động và đã liên kết tài khoản.'
            : '',
      }
    }

    if (scope === 'SpecificProperty') {
      if (!selectedProperty) {
        return {
          label: 'Chưa chọn khu trọ cụ thể',
          count: 0,
          canSend: false,
          warning:
            !targetsLoading && operatorTargets.properties.length === 0
              ? 'Bạn chưa quản lý khu trọ nào đang hoạt động.'
              : 'Vui lòng chọn 1 khu trọ để gửi thông báo.',
        }
      }
      const count = selectedProperty.activeTenantCount ?? 0
      return {
        label: `Khu trọ: ${selectedProperty.name}`,
        count,
        canSend: count > 0,
        warning:
          !targetsLoading && count === 0
            ? 'Khu trọ này hiện chưa có người thuê nào có hợp đồng đang hoạt động.'
            : '',
      }
    }

    return selectedTenant
      ? {
          label: `Cá nhân: ${selectedTenant.fullName}`,
          count: 1,
          canSend: true,
          warning: '',
        }
      : {
          label: 'Chưa chọn người thuê cụ thể',
          count: 0,
          canSend: false,
          warning: 'Vui lòng chọn 1 người thuê có hợp đồng hoạt động từ danh sách bên trên.',
        }
  }, [
    isAdmin,
    scope,
    targetsLoading,
    adminTargets,
    selectedLandlord,
    operatorTargets,
    selectedProperty,
    selectedTenant,
  ])

  if (!open) return null

  function handleScopeSelect(nextScope) {
    if (nextScope === scope || sending) return
    setScope(nextScope)
    invalidateDraftState()
  }

  function handleStepOneSubmit(event) {
    event.preventDefault()
    if (targetsLoading) return
    if (!audienceSummary.canSend) {
      setFormError(audienceSummary.warning || 'Chưa có người nhận hợp lệ để gửi thông báo.')
      return
    }
    if (!title.trim()) {
      setFormError('Vui lòng nhập tiêu đề thông báo.')
      return
    }
    if (!message.trim()) {
      setFormError('Vui lòng nhập nội dung thông báo.')
      return
    }
    setFormError('')
    setConfirmingSend(true)
  }

  async function handleConfirmSend() {
    if (sending) return
    setSending(true)
    setFormError('')
    try {
      if (!idempotencyKeyRef.current) {
        idempotencyKeyRef.current = createUuid()
      }
      const trimmedTitle = title.trim()
      const trimmedMessage = message.trim()

      let response
      if (isAdmin) {
        response = await notificationApi.sendAdmin({
          scope,
          targetUserId: scope === 'SpecificLandlord' ? selectedLandlordId : null,
          title: trimmedTitle,
          message: trimmedMessage,
          idempotencyKey: idempotencyKeyRef.current,
        })
      } else {
        let propertyIdToSend = null
        if (scope === 'SpecificProperty') {
          propertyIdToSend = selectedPropertyId || null
        } else if (scope === 'SpecificTenant' && tenantPropertyFilter && selectedTenant) {
          const matchesFilter = (selectedTenant.activeStays || []).some(
            (s) => s.propertyId === tenantPropertyFilter
          )
          if (matchesFilter) propertyIdToSend = tenantPropertyFilter
        }

        response = await notificationApi.sendOperator({
          scope,
          propertyId: propertyIdToSend,
          targetUserId: scope === 'SpecificTenant' ? selectedTenantId : null,
          title: trimmedTitle,
          message: trimmedMessage,
          idempotencyKey: idempotencyKeyRef.current,
        })
      }

      const result = response?.data || {}
      if (result.duplicate) {
        notify?.('Thông báo này đã được gửi trước đó.')
      } else {
        notify?.(`Đã gửi thông báo tới ${result.recipientCount ?? audienceSummary.count} người nhận.`)
      }
      onSent?.(result)
      onClose?.()
    } catch (e) {
      setFormError(e.message || 'Không thể gửi thông báo. Vui lòng thử lại.')
    } finally {
      setSending(false)
    }
  }

  const scopeOptions = isAdmin
    ? [
        {
          id: 'AllSystem',
          title: 'Toàn bộ hệ thống',
          desc: 'Tất cả chủ trọ, quản lý và người thuê đang hoạt động',
          badge: `${adminTargets.totalActiveSystemUsers} người`,
          Icon: Globe,
        },
        {
          id: 'AllLandlords',
          title: 'Tất cả chủ trọ',
          desc: 'Mọi tài khoản chủ trọ đã duyệt và đang hoạt động',
          badge: `${adminTargets.totalActiveLandlords} chủ trọ`,
          Icon: Building2,
        },
        {
          id: 'SpecificLandlord',
          title: '1 chủ trọ cụ thể',
          desc: 'Gửi trực tiếp tới một chủ trọ được chọn',
          badge: selectedLandlord ? 'Đã chọn 1' : 'Chọn 1 người',
          Icon: UserRound,
        },
      ]
    : [
        {
          id: 'AllActiveTenants',
          title: 'Tất cả người thuê',
          desc: 'Người thuê có hợp đồng đang hoạt động trong phạm vi của bạn',
          badge: `${operatorTargets.totalActiveTenants} người`,
          Icon: Users,
        },
        {
          id: 'SpecificProperty',
          title: '1 khu trọ cụ thể',
          desc: 'Tất cả người thuê hợp đồng hoạt động tại khu trọ đã chọn',
          badge: `${operatorTargets.properties.length} khu trọ`,
          Icon: Building2,
        },
        {
          id: 'SpecificTenant',
          title: '1 cá nhân cụ thể',
          desc: 'Gửi riêng cho một người thuê đang có hợp đồng hoạt động',
          badge: selectedTenant ? 'Đã chọn 1' : 'Chọn 1 người',
          Icon: UserRound,
        },
      ]

  return (
    <div
      className="nt-backdrop"
      role="presentation"
      onClick={(event) => {
        if (event.target === event.currentTarget && !sending) {
          onClose?.()
        }
      }}
    >
      <div
        className="nt-modal nt-modal--composer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="nt-compose-title"
      >
        <div className="nt-modal-header">
          <div className="nt-modal-header-main">
            <span className="nt-eyebrow">PHÁT HÀNH THÔNG BÁO</span>
            <h2 id="nt-compose-title" className="nt-modal-title">
              Soạn và gửi thông báo
            </h2>
          </div>
          <button
            type="button"
            className="nt-modal-close"
            aria-label="Đóng biểu mẫu gửi thông báo"
            disabled={sending}
            onClick={() => onClose?.()}
          >
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleStepOneSubmit} className="nt-composer-form">
          <div className="nt-modal-body">
            <div className="nt-form">
              {/* 1. Scope selector */}
              <div className="nt-field">
                <span className="nt-label">1. Chọn phạm vi người nhận</span>
                <div className="nt-scope-grid" role="radiogroup" aria-label="Phạm vi gửi thông báo">
                  {scopeOptions.map((opt) => {
                    const ScopeIcon = opt.Icon
                    const active = scope === opt.id
                    return (
                      <button
                        key={opt.id}
                        type="button"
                        role="radio"
                        aria-checked={active}
                        className={`nt-scope-card${active ? ' is-selected' : ''}`}
                        onClick={() => handleScopeSelect(opt.id)}
                      >
                        <div className="nt-scope-card-head">
                          <span className="nt-scope-icon" aria-hidden="true">
                            <ScopeIcon size={16} />
                          </span>
                          <span className="nt-scope-count">{opt.badge}</span>
                        </div>
                        <strong className="nt-scope-title">{opt.title}</strong>
                        <span className="nt-scope-desc">{opt.desc}</span>
                      </button>
                    )
                  })}
                </div>
              </div>

              {targetsError && (
                <div className="nt-alert nt-alert--inline" role="alert">
                  <AlertCircle size={16} aria-hidden="true" />
                  <span style={{ flex: 1 }}>{targetsError}</span>
                  <button
                    type="button"
                    className="nt-btn nt-btn--subtle nt-btn--sm"
                    onClick={() => fetchTargets()}
                  >
                    <RefreshCw size={13} aria-hidden="true" />
                    <span>Tải lại</span>
                  </button>
                </div>
              )}

              {/* 2. Contextual Target Picker */}
              {isAdmin && scope === 'SpecificLandlord' && (
                <div className="nt-picker">
                  <div className="nt-picker-head">
                    <span className="nt-label">2. Chọn chủ trọ nhận thông báo</span>
                    {selectedLandlord && (
                      <span className="nt-picker-selected-chip">
                        <span>{selectedLandlord.displayName}</span>
                        <button
                          type="button"
                          aria-label="Bỏ chọn chủ trọ"
                          onClick={() => {
                            setSelectedLandlordId('')
                            invalidateDraftState()
                          }}
                        >
                          <X size={12} />
                        </button>
                      </span>
                    )}
                  </div>

                  <div className="nt-picker-search">
                    <Search size={15} aria-hidden="true" />
                    <input
                      type="text"
                      value={landlordSearch}
                      onChange={(e) => setLandlordSearch(e.target.value)}
                      placeholder="Tìm theo tên, @username, mã chủ trọ, email, SĐT..."
                      aria-label="Tìm kiếm chủ trọ"
                    />
                    {landlordSearch && (
                      <button
                        type="button"
                        className="nt-picker-search-clear"
                        aria-label="Xóa từ khóa"
                        onClick={() => setLandlordSearch('')}
                      >
                        <X size={13} />
                      </button>
                    )}
                  </div>

                  {targetsLoading ? (
                    <div className="nt-picker-state">Đang tải danh sách chủ trọ…</div>
                  ) : filteredLandlords.length === 0 ? (
                    <div className="nt-picker-state">
                      Không tìm thấy chủ trọ đang hoạt động phù hợp.
                    </div>
                  ) : (
                    <div className="nt-picker-list" role="radiogroup" aria-label="Danh sách chủ trọ">
                      {filteredLandlords.map((l) => {
                        const checked = selectedLandlordId === l.userId
                        return (
                          <label
                            key={l.userId}
                            className={`nt-picker-option${checked ? ' is-selected' : ''}`}
                          >
                            <input
                              type="radio"
                              name="nt-landlord-target"
                              checked={checked}
                              onChange={() => {
                                setSelectedLandlordId(l.userId)
                                invalidateDraftState()
                              }}
                            />
                            <div className="nt-picker-option-main">
                              <div className="nt-picker-option-top">
                                <strong>{l.displayName}</strong>
                                <span className="nt-picker-handle">@{l.userName}</span>
                                {l.landlordCode && (
                                  <span className="nt-picker-code">{l.landlordCode}</span>
                                )}
                              </div>
                              <div className="nt-picker-option-sub">
                                {[l.phoneNumber, l.email].filter(Boolean).join(' · ') ||
                                  'Chưa cập nhật liên hệ'}
                              </div>
                            </div>
                            <span className="nt-picker-meta-pill">
                              {l.activePropertyCount} khu trọ
                            </span>
                            {checked && (
                              <Check size={16} className="nt-picker-check" aria-hidden="true" />
                            )}
                          </label>
                        )
                      })}
                    </div>
                  )}
                </div>
              )}

              {!isAdmin && scope === 'SpecificProperty' && (
                <div className="nt-picker">
                  <div className="nt-picker-head">
                    <span className="nt-label">2. Chọn khu trọ áp dụng</span>
                  </div>

                  {operatorTargets.properties.length > 4 && (
                    <div className="nt-picker-search">
                      <Search size={15} aria-hidden="true" />
                      <input
                        type="text"
                        value={propertySearch}
                        onChange={(e) => setPropertySearch(e.target.value)}
                        placeholder="Tìm theo tên hoặc địa chỉ khu trọ..."
                        aria-label="Tìm kiếm khu trọ"
                      />
                    </div>
                  )}

                  {targetsLoading ? (
                    <div className="nt-picker-state">Đang tải danh sách khu trọ…</div>
                  ) : filteredProperties.length === 0 ? (
                    <div className="nt-picker-state">
                      Không có khu trọ đang hoạt động trong phạm vi quản lý của bạn.
                    </div>
                  ) : (
                    <div className="nt-picker-list" role="radiogroup" aria-label="Danh sách khu trọ">
                      {filteredProperties.map((prop) => {
                        const checked = selectedPropertyId === prop.propertyId
                        const zero = (prop.activeTenantCount ?? 0) === 0
                        return (
                          <label
                            key={prop.propertyId}
                            className={`nt-picker-option${checked ? ' is-selected' : ''}`}
                          >
                            <input
                              type="radio"
                              name="nt-property-target"
                              checked={checked}
                              onChange={() => {
                                setSelectedPropertyId(prop.propertyId)
                                invalidateDraftState()
                              }}
                            />
                            <div className="nt-picker-option-main">
                              <div className="nt-picker-option-top">
                                <strong>{prop.name}</strong>
                              </div>
                              {prop.address && (
                                <div className="nt-picker-option-sub">{prop.address}</div>
                              )}
                            </div>
                            <span
                              className={`nt-picker-meta-pill${zero ? ' is-zero' : ''}`}
                            >
                              {prop.activeTenantCount} người thuê
                            </span>
                            {checked && (
                              <Check size={16} className="nt-picker-check" aria-hidden="true" />
                            )}
                          </label>
                        )
                      })}
                    </div>
                  )}
                </div>
              )}

              {!isAdmin && scope === 'SpecificTenant' && (
                <div className="nt-picker">
                  <div className="nt-picker-head">
                    <span className="nt-label">2. Chọn người thuê nhận thông báo</span>
                    {selectedTenant && (
                      <span className="nt-picker-selected-chip">
                        <span>{selectedTenant.fullName}</span>
                        <button
                          type="button"
                          aria-label="Bỏ chọn người thuê"
                          onClick={() => {
                            setSelectedTenantId('')
                            invalidateDraftState()
                          }}
                        >
                          <X size={12} />
                        </button>
                      </span>
                    )}
                  </div>

                  <div className="nt-picker-controls">
                    {operatorTargets.properties.length > 1 && (
                      <select
                        className="nt-select nt-picker-filter"
                        aria-label="Lọc người thuê theo khu trọ"
                        value={tenantPropertyFilter}
                        onChange={(e) => {
                          setTenantPropertyFilter(e.target.value)
                          invalidateDraftState()
                        }}
                      >
                        <option value="">Tất cả khu trọ</option>
                        {operatorTargets.properties.map((p) => (
                          <option key={p.propertyId} value={p.propertyId}>
                            {p.name} ({p.activeTenantCount})
                          </option>
                        ))}
                      </select>
                    )}

                    <div className="nt-picker-search" style={{ flex: 1 }}>
                      <Search size={15} aria-hidden="true" />
                      <input
                        type="text"
                        value={tenantSearch}
                        onChange={(e) => setTenantSearch(e.target.value)}
                        placeholder="Tìm theo tên, SĐT, phòng, khu trọ..."
                        aria-label="Tìm kiếm người thuê"
                      />
                      {tenantSearch && (
                        <button
                          type="button"
                          className="nt-picker-search-clear"
                          aria-label="Xóa từ khóa"
                          onClick={() => setTenantSearch('')}
                        >
                          <X size={13} />
                        </button>
                      )}
                    </div>
                  </div>

                  {targetsLoading ? (
                    <div className="nt-picker-state">
                      Đang tải danh sách người thuê hợp đồng hoạt động…
                    </div>
                  ) : filteredTenants.length === 0 ? (
                    <div className="nt-picker-state">
                      Không có người thuê nào có hợp đồng đang hoạt động khớp bộ lọc.
                    </div>
                  ) : (
                    <div
                      className="nt-picker-list"
                      role="radiogroup"
                      aria-label="Danh sách người thuê"
                    >
                      {filteredTenants.map((tenant) => {
                        const checked = selectedTenantId === tenant.userId
                        return (
                          <label
                            key={tenant.userId}
                            className={`nt-picker-option${checked ? ' is-selected' : ''}`}
                          >
                            <input
                              type="radio"
                              name="nt-tenant-target"
                              checked={checked}
                              onChange={() => {
                                setSelectedTenantId(tenant.userId)
                                invalidateDraftState()
                              }}
                            />
                            <div className="nt-picker-option-main">
                              <div className="nt-picker-option-top">
                                <strong>{tenant.fullName}</strong>
                                {tenant.phoneNumber && (
                                  <span className="nt-picker-handle">{tenant.phoneNumber}</span>
                                )}
                              </div>
                              <div className="nt-picker-stays">
                                {(tenant.activeStays || []).map((stay) => (
                                  <span key={stay.contractId} className="nt-stay-chip">
                                    {stay.propertyName} · P.{stay.roomNumber}
                                  </span>
                                ))}
                              </div>
                            </div>
                            {checked && (
                              <Check size={16} className="nt-picker-check" aria-hidden="true" />
                            )}
                          </label>
                        )
                      })}
                    </div>
                  )}
                </div>
              )}

              {/* Live Audience Summary */}
              <div
                className={`nt-audience-summary${audienceSummary.warning ? ' nt-audience-summary--warning' : ''}`}
              >
                <div className="nt-audience-summary-main">
                  {audienceSummary.warning ? (
                    <AlertTriangle size={16} aria-hidden="true" />
                  ) : (
                    <Users size={16} aria-hidden="true" />
                  )}
                  <div>
                    <strong>Đối tượng nhận: {audienceSummary.label}</strong>
                    {audienceSummary.warning && (
                      <div className="nt-audience-warning">{audienceSummary.warning}</div>
                    )}
                  </div>
                </div>
                <span className="nt-audience-count-pill">
                  {targetsLoading ? '…' : `${audienceSummary.count} người nhận`}
                </span>
              </div>

              {/* 3. Title & Message */}
              <label className="nt-field">
                <span className="nt-label">Tiêu đề thông báo</span>
                <input
                  className="nt-input"
                  value={title}
                  onChange={(e) => {
                    setTitle(e.target.value)
                    invalidateDraftState()
                  }}
                  maxLength={250}
                  placeholder="Nhập tiêu đề ngắn gọn, rõ ràng…"
                  required
                />
                <span className="nt-char-hint">{title.length}/250</span>
              </label>

              <label className="nt-field">
                <span className="nt-label">Nội dung chi tiết</span>
                <textarea
                  className="nt-textarea"
                  value={message}
                  onChange={(e) => {
                    setMessage(e.target.value)
                    invalidateDraftState()
                  }}
                  maxLength={4000}
                  placeholder="Nhập nội dung thông báo sẽ gửi tới hộp thư người nhận…"
                  required
                />
                <span className="nt-char-hint">{message.length}/4000</span>
              </label>

              {formError && (
                <div className="nt-alert nt-alert--inline" role="alert">
                  <AlertCircle size={16} aria-hidden="true" />
                  <span>{formError}</span>
                </div>
              )}

              {confirmingSend && (
                <div className="nt-confirm-box">
                  <div className="nt-confirm-copy">
                    <strong>Xác nhận phát hành thông báo</strong>
                    <span>
                      Gửi “{title.trim()}” tới <strong>{audienceSummary.label}</strong> (
                      {audienceSummary.count} người nhận)?
                    </span>
                  </div>
                  <div className="nt-confirm-actions">
                    <button
                      type="button"
                      className="nt-btn nt-btn--subtle"
                      disabled={sending}
                      onClick={() => setConfirmingSend(false)}
                    >
                      Quay lại
                    </button>
                    <button
                      type="button"
                      className="nt-btn nt-btn--primary"
                      disabled={sending}
                      onClick={handleConfirmSend}
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
              onClick={() => onClose?.()}
            >
              Đóng
            </button>
            {!confirmingSend && (
              <button
                type="submit"
                className="nt-btn nt-btn--primary"
                disabled={sending || targetsLoading || !audienceSummary.canSend}
              >
                <Send size={15} aria-hidden="true" />
                <span>Tiếp tục gửi</span>
              </button>
            )}
          </div>
        </form>
      </div>
    </div>
  )
}
