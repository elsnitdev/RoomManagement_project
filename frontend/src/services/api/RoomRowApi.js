import { apiRequest } from '../apiClient'

const rowsPath = propertyId => `/api/properties/${encodeURIComponent(propertyId)}/rows`

export async function listRoomRows(propertyId) {
  const response = await apiRequest(rowsPath(propertyId))
  return response.data
}

export async function createRoomRow(propertyId, data) {
  const response = await apiRequest(rowsPath(propertyId), { method: 'POST', body: data })
  return response.data
}

export async function updateRoomRow(propertyId, rowId, data) {
  const response = await apiRequest(`${rowsPath(propertyId)}/${encodeURIComponent(rowId)}`, { method: 'PUT', body: data })
  return response.data
}
