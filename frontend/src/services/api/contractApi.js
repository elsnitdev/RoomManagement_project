import { apiRequest } from '../apiClient'

const path = propertyId => `/api/properties/${encodeURIComponent(propertyId)}/contracts`
const one = (propertyId, contractId) => `${path(propertyId)}/${encodeURIComponent(contractId)}`
const data = response => response.data
async function allPages(base, signal) {
  const items = []
  for (let page = 1; ; page += 1) {
    const response = await apiRequest(`${base}?page=${page}&pageSize=100`, { signal })
    items.push(...(response.data || []))
    if (page >= (response.meta?.totalPages || 1)) return { data: items }
  }
}

export const contractApi = {
  list: (propertyId, signal) => allPages(path(propertyId), signal),
  detail: (propertyId, contractId, signal) => apiRequest(one(propertyId, contractId), { signal }).then(data),
  create: (propertyId, body) => apiRequest(path(propertyId), { method: 'POST', body }).then(data),
  addUser: (propertyId, contractId, body) => apiRequest(`${one(propertyId, contractId)}/users`, { method: 'POST', body }).then(data),
  activate: (propertyId, contractId) => apiRequest(`${one(propertyId, contractId)}/activate`, { method: 'POST' }).then(data),
  cancel: (propertyId, contractId) => apiRequest(`${one(propertyId, contractId)}/cancel`, { method: 'POST' }).then(data),
  terminate: (propertyId, contractId, reason) => apiRequest(`${one(propertyId, contractId)}/terminate`, { method: 'POST', body: { reason } }).then(data),
  leave: (propertyId, contractId, linkId, leaveDate) => apiRequest(`${one(propertyId, contractId)}/tenants/${encodeURIComponent(linkId)}/leave`, { method: 'POST', body: { leaveDate } }).then(data),
  mine: signal => allPages('/api/me/contracts', signal),
  myDetail: (contractId, signal) => apiRequest(`/api/me/contracts/${encodeURIComponent(contractId)}`, { signal }).then(data),
}
