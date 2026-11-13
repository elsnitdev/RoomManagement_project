import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'

const BASE_PATH = '/api/landlord/managers'

export function listManagers({ page = 1, pageSize = 20, search = '', isActive } = {}, options = {}) {
  const params = new URLSearchParams()
  if (page) params.set('page', String(page))
  if (pageSize) params.set('pageSize', String(pageSize))
  if (search && search.trim()) params.set('search', search.trim())
  if (typeof isActive === 'boolean') params.set('isActive', String(isActive))

  const qs = params.toString()
  return apiRequest(qs ? `${BASE_PATH}?${qs}` : BASE_PATH, options)
}

export function getManager(managerId, options = {}) {
  return apiRequest(`${BASE_PATH}/${encodeURIComponent(managerId)}`, options)
}

export async function createManager(payload) {
  const result = await apiRequest(BASE_PATH, {
    method: 'POST',
    body: payload,
  })
  invalidateApiCache('landlord:managers')
  return result
}

export async function assignManagerProperties(managerId, propertyIds) {
  const result = await apiRequest(`${BASE_PATH}/${encodeURIComponent(managerId)}/properties`, {
    method: 'POST',
    body: { propertyIds },
  })
  invalidateApiCache('landlord:managers')
  return result
}

export async function revokeManagerProperty(managerId, propertyId) {
  const result = await apiRequest(
    `${BASE_PATH}/${encodeURIComponent(managerId)}/properties/${encodeURIComponent(propertyId)}`,
    { method: 'DELETE' },
  )
  invalidateApiCache('landlord:managers')
  return result
}

export async function lockManager(managerId) {
  const result = await apiRequest(`${BASE_PATH}/${encodeURIComponent(managerId)}/lock`, {
    method: 'POST',
  })
  invalidateApiCache('landlord:managers')
  return result
}

export async function unlockManager(managerId) {
  const result = await apiRequest(`${BASE_PATH}/${encodeURIComponent(managerId)}/unlock`, {
    method: 'POST',
  })
  invalidateApiCache('landlord:managers')
  return result
}

export async function deleteManager(managerId) {
  const result = await apiRequest(`${BASE_PATH}/${encodeURIComponent(managerId)}`, {
    method: 'DELETE',
  })
  invalidateApiCache('landlord:managers')
  return result
}
