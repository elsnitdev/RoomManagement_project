import { apiRequest } from '../apiClient'

const id = value => encodeURIComponent(value)
const property = propertyId => `/api/properties/${id(propertyId)}`
const room = (propertyId, roomId) => `${property(propertyId)}/rooms/${id(roomId)}`
const params = values => {
  const query = new URLSearchParams()
  Object.entries(values).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) query.set(key, String(value))
  })
  return query.toString()
}
const data = response => response.data
async function allPages(path, signal) {
  const items = []
  for (let page = 1; ; page += 1) {
    const response = await apiRequest(`${path}?${params({ page, pageSize: 100 })}`, { signal })
    items.push(...(response.data || []))
    if (page >= (response.meta?.totalPages || 1)) return { data: items }
  }
}

export const billingApi = {
  properties: (signal) => allPages('/api/properties', signal),
  rooms: (propertyId, signal) => allPages(`${property(propertyId)}/rooms`, signal),
  contracts: (propertyId, signal) => allPages(`${property(propertyId)}/contracts`, signal),
  rates: (propertyId, signal) => apiRequest(`${property(propertyId)}/utility-rates`, { signal }).then(data),
  createRate: (propertyId, body) => apiRequest(`${property(propertyId)}/utility-rates`, { method: 'POST', body }).then(data),
  readings: (propertyId, roomId, signal) => apiRequest(`${room(propertyId, roomId)}/meter-readings`, { signal }).then(data),
  createReading: (propertyId, roomId, body) => apiRequest(`${room(propertyId, roomId)}/meter-readings`, { method: 'POST', body }).then(data),
  services: (signal) => apiRequest('/api/services', { signal }).then(data),
  createService: (body) => apiRequest('/api/services', { method: 'POST', body }).then(data),
  roomServices: (propertyId, roomId, signal) => apiRequest(`${room(propertyId, roomId)}/services`, { signal }).then(data),
  assignService: (propertyId, roomId, body) => apiRequest(`${room(propertyId, roomId)}/services`, { method: 'POST', body }).then(data),
  invoices: (propertyId, page = 1, signal) => apiRequest(`${property(propertyId)}/invoices?${params({ page, pageSize: 20 })}`, { signal }),
  preview: (propertyId, body) => apiRequest(`${property(propertyId)}/invoices/monthly/preview`, { method: 'POST', body }).then(data),
  createMonthly: (propertyId, body) => apiRequest(`${property(propertyId)}/invoices/monthly`, { method: 'POST', body }).then(data),
  createDeposit: (contractId) => apiRequest(`/api/contracts/${id(contractId)}/invoices/deposit`, { method: 'POST' }).then(data),
  invoice: (invoiceId, signal) => apiRequest(`/api/invoices/${id(invoiceId)}`, { signal }).then(data),
  issue: (invoiceId) => apiRequest(`/api/invoices/${id(invoiceId)}/issue`, { method: 'POST' }).then(data),
  cancel: (invoiceId) => apiRequest(`/api/invoices/${id(invoiceId)}/cancel`, { method: 'POST' }).then(data),
  myInvoices: (page = 1, signal) => apiRequest(`/api/me/invoices?${params({ page, pageSize: 20 })}`, { signal }),
  myInvoice: (invoiceId, signal) => apiRequest(`/api/me/invoices/${id(invoiceId)}`, { signal }).then(data),
}
