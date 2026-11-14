import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'

function invalidateRequestQueries() {
  invalidateApiCache('requests')
  invalidateApiCache('my-requests')
  invalidateApiCache('my-announcements')
  invalidateApiCache('landlord-requests')
  invalidateApiCache('landlord-announcements')
}

function buildQuery(params = {}) {
  const searchParams = new URLSearchParams()
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && String(value).trim() !== '') {
      searchParams.set(key, String(value).trim())
    }
  })
  const qs = searchParams.toString()
  return qs ? `?${qs}` : ''
}

export async function getMyRequestProperties({ signal } = {}) {
  const response = await apiRequest('/api/me/requests/properties', { signal })
  return response.data || []
}

export async function createTenantRequest({ propertyId, title, content }) {
  const body = { title, content }
  if (propertyId) body.propertyId = propertyId
  const response = await apiRequest('/api/me/requests', { method: 'POST', body })
  invalidateRequestQueries()
  return response.data
}

export function getMyRequests({ page = 1, pageSize = 20, status, signal } = {}) {
  return apiRequest(`/api/me/requests${buildQuery({ page, pageSize, status })}`, { signal })
}

export async function getMyRequestById(requestId, { signal } = {}) {
  const response = await apiRequest(`/api/me/requests/${encodeURIComponent(requestId)}`, { signal })
  return response.data
}

export function getMyAnnouncements({ page = 1, pageSize = 20, isRead, signal } = {}) {
  return apiRequest(`/api/me/announcements${buildQuery({ page, pageSize, isRead })}`, { signal })
}

export async function markAnnouncementAsRead(requestId) {
  const response = await apiRequest(`/api/me/announcements/${encodeURIComponent(requestId)}/read`, {
    method: 'POST',
  })
  invalidateRequestQueries()
  return response.data
}

export function getLandlordRequests({
  propertyId,
  status,
  isRead,
  search,
  page = 1,
  pageSize = 20,
  signal,
} = {}) {
  return apiRequest(
    `/api/landlord/requests${buildQuery({ propertyId, status, isRead, search, page, pageSize })}`,
    { signal },
  )
}

export async function getLandlordRequestById(requestId, { signal } = {}) {
  const response = await apiRequest(`/api/landlord/requests/${encodeURIComponent(requestId)}`, {
    signal,
  })
  invalidateRequestQueries()
  return response.data
}

export async function updateLandlordRequestStatus(requestId, status) {
  const response = await apiRequest(
    `/api/landlord/requests/${encodeURIComponent(requestId)}/status`,
    {
      method: 'PATCH',
      body: { status },
    },
  )
  invalidateRequestQueries()
  return response.data
}

export async function getAnnouncementRecipients(propertyId, { signal } = {}) {
  const response = await apiRequest(
    `/api/properties/${encodeURIComponent(propertyId)}/announcement-recipients`,
    { signal },
  )
  return response.data || []
}

export async function createLandlordAnnouncement(
  propertyId,
  { recipientType, receiverUserIds = [], roomIds = [], title, content },
) {
  const response = await apiRequest(
    `/api/properties/${encodeURIComponent(propertyId)}/announcements`,
    {
      method: 'POST',
      body: { recipientType, receiverUserIds, roomIds, title, content },
    },
  )
  invalidateRequestQueries()
  return response.data
}

export function getLandlordAnnouncements(propertyId, { page = 1, pageSize = 20, signal } = {}) {
  return apiRequest(
    `/api/properties/${encodeURIComponent(propertyId)}/announcements${buildQuery({ page, pageSize })}`,
    { signal },
  )
}

export const requestApi = {
  getMyRequestProperties,
  createTenantRequest,
  getMyRequests,
  getMyRequestById,
  getMyAnnouncements,
  markAnnouncementAsRead,
  getLandlordRequests,
  getLandlordRequestById,
  updateLandlordRequestStatus,
  getAnnouncementRecipients,
  createLandlordAnnouncement,
  getLandlordAnnouncements,
}

