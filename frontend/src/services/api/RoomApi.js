import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'

const propertyPath = propertyId => `/api/properties/${encodeURIComponent(propertyId)}`
const roomsPath = propertyId => `${propertyPath(propertyId)}/rooms`
const roomPath = (propertyId, roomId) => `${roomsPath(propertyId)}/${encodeURIComponent(roomId)}`

function queryString({ page = 1, pageSize = 20, search, status } = {}) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())
  if (status !== undefined && status !== null && status !== '') params.set('status', String(status))
  return params.toString()
}

export function listRooms(propertyId, filters = {}) {
  return apiRequest(`${roomsPath(propertyId)}?${queryString(filters)}`)
}

export function getRoom(propertyId, roomId) {
  return apiRequest(roomPath(propertyId, roomId))
}

export async function createRoom(propertyId, data) {
  const response = await apiRequest(roomsPath(propertyId), { method: 'POST', body: data })
  invalidateApiCache('rooms')
  return response.data
}

export async function updateRoom(propertyId, roomId, data) {
  const response = await apiRequest(roomPath(propertyId, roomId), { method: 'PUT', body: data })
  invalidateApiCache('rooms')
  return response.data
}

export async function deleteRoom(propertyId, roomId) {
  await apiRequest(roomPath(propertyId, roomId), { method: 'DELETE' })
  invalidateApiCache('rooms')
}
