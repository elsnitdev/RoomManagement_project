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

export function listUsers({ role = '', keyword = '', page = 1, pageSize = 20 } = {}, options = {}) {
  const params = new URLSearchParams()
  if (role) params.set('role', role)
  if (keyword && keyword.trim()) params.set('keyword', keyword.trim())
  if (page) params.set('page', String(page))
  if (pageSize) params.set('pageSize', String(pageSize))
  const qs = params.toString()
  return apiRequest(qs ? `/api/admin/users?${qs}` : '/api/admin/users', options)
}

export async function lockUser(id) {
  const result = await apiRequest(`/api/admin/users/${encodeURIComponent(id)}/lock`, { method: 'POST' })
  invalidateApiCache('admin:users')
  return result
}

export async function unlockUser(id) {
  const result = await apiRequest(`/api/admin/users/${encodeURIComponent(id)}/unlock`, { method: 'POST' })
  invalidateApiCache('admin:users')
  return result
}

export async function deleteUser(id) {
  const result = await apiRequest(`/api/admin/users/${encodeURIComponent(id)}`, { method: 'DELETE' })
  invalidateApiCache('admin:users')
  return result
}

export async function updateUser(id, payload) {
  const result = await apiRequest(`/api/admin/users/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body: payload,
  })
  invalidateApiCache('admin:users')
  return result
}
