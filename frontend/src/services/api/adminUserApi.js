import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'

const pendingPath = id => `/api/admin/users/pending-landlords/${encodeURIComponent(id)}`

export function getPendingLandlords(options = {}) {
  return apiRequest('/api/admin/users/pending-landlords', options)
}

export async function approveLandlord(id) {
  const result = await apiRequest(`${pendingPath(id)}/approve`, { method: 'POST' })
  invalidateApiCache('admin:pending-landlords')
  return result
}

export async function rejectLandlord(id, reason) {
  const result = await apiRequest(`${pendingPath(id)}/reject`, {
    method: 'POST',
    body: { reason },
  })
  invalidateApiCache('admin:pending-landlords')
  return result
}

export function createUser(data) {
  return apiRequest('/api/admin/users', {
    method: 'POST',
    body: data,
  })
}
