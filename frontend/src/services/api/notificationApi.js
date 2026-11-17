import { apiRequest } from '../apiClient'

export const notificationApi = {
  list: (page = 1, unreadOnly = false) =>
    apiRequest(`/api/me/notifications?page=${page}&pageSize=20${unreadOnly ? '&unreadOnly=true' : ''}`),
  unreadCount: () => apiRequest('/api/me/notifications/unread-count'),
  read: (id) => apiRequest(`/api/me/notifications/${encodeURIComponent(id)}/read`, { method: 'POST' }),
  readAll: () => apiRequest('/api/me/notifications/read-all', { method: 'POST' }),
  adminBroadcast: (body) => apiRequest('/api/admin/notifications/broadcast', { method: 'POST', body }),
  managerBroadcast: (propertyId, body) => apiRequest(`/api/properties/${encodeURIComponent(propertyId)}/manager-announcements`, { method: 'POST', body }),
}
