import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'

const userPath = id => `/api/users/${encodeURIComponent(id)}`

export function getProfileStatus(userId, options = {}) {
  return apiRequest(`${userPath(userId)}/profile-status`, options)
}

export async function updateProfile(userId, data) {
  const result = await apiRequest(`${userPath(userId)}/profile`, {
    method: 'PUT',
    body: data,
  })
  invalidateApiCache(`profile:${userId}`)
  return result
}
