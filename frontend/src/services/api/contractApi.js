import { apiRequest } from '../apiClient'
import { uploadWithTimeout } from './uploadWithTimeout'

const path = propertyId => `/api/properties/${encodeURIComponent(propertyId)}/contracts`
const one = (propertyId, contractId) => `${path(propertyId)}/${encodeURIComponent(contractId)}`
const data = response => response.data
async function allPages(base, signal) {
  const items = []
  for (let page = 1; ; page += 1) {
    const response = await apiRequest(`${base}?page=${page}&pageSize=100`, { signal })
    items.push(...(response.data || []))
    if (response.meta?.totalPages ? page >= response.meta.totalPages : items.length >= (response.meta?.totalItems ?? response.meta?.total ?? items.length) || !response.data?.length) return { data: items }
  }
}

export const contractApi = {
  list: (propertyId, signal) => allPages(path(propertyId), signal),
  detail: (propertyId, contractId, signal) => apiRequest(one(propertyId, contractId), { signal }).then(data),
  create: (propertyId, body) => apiRequest(path(propertyId), { method: 'POST', body }).then(data),
  addUser: (propertyId, contractId, body) => apiRequest(`${one(propertyId, contractId)}/users`, { method: 'POST', body }).then(data),
  addTenant: (propertyId, contractId, body) => apiRequest(`${one(propertyId, contractId)}/tenants`, { method: 'POST', body }).then(data),
  setContact: (propertyId, contractId, linkId) => apiRequest(`${one(propertyId, contractId)}/contact/${encodeURIComponent(linkId)}`, { method: 'PUT' }).then(data),
  activate: (propertyId, contractId) => apiRequest(`${one(propertyId, contractId)}/activate`, { method: 'POST' }).then(data),
  cancel: (propertyId, contractId) => apiRequest(`${one(propertyId, contractId)}/cancel`, { method: 'POST' }).then(data),
  terminate: (propertyId, contractId, reason) => apiRequest(`${one(propertyId, contractId)}/terminate`, { method: 'POST', body: { reason } }).then(data),
  handover: (propertyId, contractId, handoverOn) => apiRequest(`${one(propertyId, contractId)}/handover`, { method: 'POST', body: { handoverOn } }).then(data),
  readings: (propertyId, contractId, signal) => apiRequest(`${one(propertyId, contractId)}/meter-readings`, { signal }).then(data),
  addReading: (propertyId, contractId, body) => apiRequest(`${one(propertyId, contractId)}/meter-readings`, { method: 'POST', body }).then(data),
  applyDeposit: (propertyId, contractId, body) => apiRequest(`${one(propertyId, contractId)}/apply-deposit`, { method: 'POST', body }).then(data),
  completeSettlement: (propertyId, contractId, body) => apiRequest(`${one(propertyId, contractId)}/complete-settlement`, { method: 'POST', body }).then(data),
  settlementOverview: (propertyId, contractId, signal) => apiRequest(`${one(propertyId, contractId)}/settlement-overview`, { signal }).then(data),
  settlementRefundEvidence: (propertyId, contractId, signal) => apiRequest(`${one(propertyId, contractId)}/settlement/refund-evidence`, { signal }).then(data),
  uploadSettlementRefundEvidence: async (propertyId, contractId, selection) => {
    const body = new FormData()
    body.append('imageId', selection.id)
    body.append('file', selection.file)
    return data(await uploadWithTimeout(signal => apiRequest(`${one(propertyId, contractId)}/settlement/refund-evidence`, { method: 'POST', body, signal })))
  },
  settlementRefundEvidenceContent: (propertyId, contractId, imageId, signal) => apiRequest(`${one(propertyId, contractId)}/settlement/refund-evidence/${encodeURIComponent(imageId)}/content`, { signal, responseType: 'blob' }),
  leave: (propertyId, contractId, linkId, leaveDate) => apiRequest(`${one(propertyId, contractId)}/tenants/${encodeURIComponent(linkId)}/leave`, { method: 'POST', body: { leaveDate } }).then(data),
  mine: signal => allPages('/api/me/contracts', signal),
  myDetail: (contractId, signal) => apiRequest(`/api/me/contracts/${encodeURIComponent(contractId)}`, { signal }).then(data),
  rules: (propertyId, signal) => apiRequest(`/api/properties/${encodeURIComponent(propertyId)}/rules`, { signal }).then(data),
  saveRules: (propertyId, body) => apiRequest(`/api/properties/${encodeURIComponent(propertyId)}/rules`, { method: 'PUT', body }).then(data),
  myRules: signal => apiRequest('/api/me/contract-rules', { signal }).then(data),
  acceptRules: (contractId, linkId, versionId) => apiRequest(`/api/me/contract-rules/${encodeURIComponent(contractId)}/tenants/${encodeURIComponent(linkId)}/accept`, { method: 'POST', body: { versionId } }).then(data),
  myEvidence: (contractId, linkId, signal) => apiRequest(`/api/me/contract-rules/${encodeURIComponent(contractId)}/tenants/${encodeURIComponent(linkId)}/evidence`, { signal }).then(data),
  uploadEvidence: (contractId, linkId, file) => { const body = new FormData(); body.append('file', file); return apiRequest(`/api/me/contract-rules/${encodeURIComponent(contractId)}/tenants/${encodeURIComponent(linkId)}/evidence`, { method: 'POST', body }).then(data) },
  myEvidenceContent: (contractId, linkId, id) => apiRequest(`/api/me/contract-rules/${encodeURIComponent(contractId)}/tenants/${encodeURIComponent(linkId)}/evidence/${encodeURIComponent(id)}/content`, { responseType: 'blob' }),
  tenantEvidence: (propertyId, contractId, linkId, signal) => apiRequest(`${one(propertyId, contractId)}/tenants/${encodeURIComponent(linkId)}/rules-evidence`, { signal }).then(data),
  tenantEvidenceContent: (propertyId, contractId, linkId, id) => apiRequest(`${one(propertyId, contractId)}/tenants/${encodeURIComponent(linkId)}/rules-evidence/${encodeURIComponent(id)}/content`, { responseType: 'blob' }),
}
