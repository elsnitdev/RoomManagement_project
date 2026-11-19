import { apiRequest } from '../apiClient'

function buildQuery(params = {}) {
  const sp = new URLSearchParams()
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && String(value).trim() !== '') {
      sp.set(key, String(value).trim())
    }
  })
  const qs = sp.toString()
  return qs ? `?${qs}` : ''
}

export const notificationApi = {
  list: (page = 1, unreadOnly = false, pageSize = 20) =>
    apiRequest(`/api/me/notifications?page=${page}&pageSize=${pageSize}${unreadOnly ? '&unreadOnly=true' : ''}`),
  listSent: (page = 1, pageSize = 20) =>
    apiRequest(`/api/me/notifications/sent?page=${page}&pageSize=${pageSize}`),
  unreadCount: () => apiRequest('/api/me/notifications/unread-count'),
  read: (id) => apiRequest(`/api/me/notifications/${encodeURIComponent(id)}/read`, { method: 'POST' }),
  readAll: () => apiRequest('/api/me/notifications/read-all', { method: 'POST' }),

  getAdminTargets: (search = '', { signal } = {}) =>
    apiRequest(`/api/admin/notifications/targets${buildQuery({ search })}`, { signal }),
  sendAdmin: (body) =>
    apiRequest('/api/admin/notifications/send', { method: 'POST', body }),

  getOperatorTargets: (search = '', { signal } = {}) =>
    apiRequest(`/api/operator/notifications/targets${buildQuery({ search })}`, { signal }),
  sendOperator: (body) =>
    apiRequest('/api/operator/notifications/send', { method: 'POST', body }),

  // Legacy endpoints preserved for compatibility
  adminBroadcast: (body) => apiRequest('/api/admin/notifications/broadcast', { method: 'POST', body }),
  managerBroadcast: (propertyId, body) =>
    apiRequest(`/api/properties/${encodeURIComponent(propertyId)}/manager-announcements`, { method: 'POST', body }),
}
