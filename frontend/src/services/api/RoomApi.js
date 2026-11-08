import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'
import { readAuthSession } from '../../contexts/authSession.js'

const propertyPath = propertyId => `/api/properties/${encodeURIComponent(propertyId)}`
const roomsPath = propertyId => `${propertyPath(propertyId)}/rooms`
const roomPath = (propertyId, roomId) => `${roomsPath(propertyId)}/${encodeURIComponent(roomId)}`

function queryString({ page = 1, pageSize = 20, search, status, rowId, floor } = {}) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search?.trim()) params.set('search', search.trim())
  if (status !== undefined && status !== null && status !== '') params.set('status', String(status))
  if (rowId) params.set('rowId', rowId)
  if (floor !== undefined && floor !== null && floor !== '') params.set('floor', String(floor))
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

export async function previewRoomBatch(propertyId, plan) {
  const response = await apiRequest(`${roomsPath(propertyId)}/batch/preview`, { method: 'POST', body: plan })
  return response.data
}

export async function createRoomBatch(propertyId, plan) {
  const response = await apiRequest(`${roomsPath(propertyId)}/batch`, { method: 'POST', body: plan })
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

const imagesPath = (propertyId, roomId) => `${roomPath(propertyId, roomId)}/images`

export async function listRoomImages(propertyId, roomId) {
  return (await apiRequest(imagesPath(propertyId, roomId))).data
}

export async function uploadRoomImage(propertyId, roomId, file) {
  const body = new FormData()
  body.append('file', file)
  return (await apiRequest(imagesPath(propertyId, roomId), { method: 'POST', body })).data
}

export async function deleteRoomImage(propertyId, roomId, imageId) {
  await apiRequest(`${imagesPath(propertyId, roomId)}/${encodeURIComponent(imageId)}`, { method: 'DELETE' })
}

export async function reorderRoomImages(propertyId, roomId, imageIds) {
  await apiRequest(`${imagesPath(propertyId, roomId)}/order`, { method: 'PUT', body: { imageIds } })
}

export async function loadRoomImage(propertyId, roomId, imageId, signal) {
  const base = (import.meta.env?.VITE_API_BASE_URL || '').replace(/\/$/, '')
  const response = await fetch(`${base}${imagesPath(propertyId, roomId)}/${encodeURIComponent(imageId)}/content`, {
    headers: { Authorization: `Bearer ${readAuthSession()?.accessToken || ''}` }, signal,
  })
  if (!response.ok) throw new Error(`Không tải được ảnh (${response.status}).`)
  return URL.createObjectURL(await response.blob())
}
