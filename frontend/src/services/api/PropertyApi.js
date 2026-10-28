import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'

const basePath = '/api/properties'
const itemPath = id => `${basePath}/${encodeURIComponent(id)}`

function invalidatePropertyQueries() {
  invalidateApiCache('properties')
  invalidateApiCache('property-list')
  invalidateApiCache('property')
}

function queryString({ page = 1, pageSize = 20, search, isActive } = {}) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())
  if (isActive !== undefined && isActive !== null) params.set('isActive', String(isActive))
  return params.toString()
}

export function listProperties(filters = {}) {
  return apiRequest(`${basePath}?${queryString(filters)}`)
}

export function getProperty(id) {
  return apiRequest(itemPath(id))
}

export async function createProperty(data) {
  const response = await apiRequest(basePath, { method: 'POST', body: data })
  invalidatePropertyQueries()
  return response.data
}

export async function updateProperty(id, data) {
  const response = await apiRequest(itemPath(id), { method: 'PUT', body: data })
  invalidatePropertyQueries()
  return response.data
}

export async function deleteProperty(id) {
  await apiRequest(itemPath(id), { method: 'DELETE' })
  invalidatePropertyQueries()
}
