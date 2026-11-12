import { apiRequest } from '../apiClient'
import { uploadWithTimeout } from './uploadWithTimeout'

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
    if (response.meta?.totalPages ? page >= response.meta.totalPages : items.length >= (response.meta?.totalItems ?? response.meta?.total ?? items.length) || !response.data?.length) return { data: items }
  }
}

export const billingApi = {
  properties: (signal) => allPages('/api/properties', signal),
  updatePaymentAccount: (propertyId, body) => apiRequest(`${property(propertyId)}/payment-account`, { method: 'PUT', body }).then(data),
  rooms: (propertyId, signal) => allPages(`${property(propertyId)}/rooms`, signal),
  contracts: (propertyId, signal) => allPages(`${property(propertyId)}/contracts`, signal),
  rates: (propertyId, signal) => apiRequest(`${property(propertyId)}/utility-rates`, { signal }).then(data),
  createRate: (propertyId, body) => apiRequest(`${property(propertyId)}/utility-rates`, { method: 'POST', body }).then(data),
  reviseRate: (propertyId, body) => apiRequest(`${property(propertyId)}/utility-rates/revisions`, { method: 'POST', body }).then(data),
  readings: (propertyId, roomId, signal) => apiRequest(`${room(propertyId, roomId)}/meter-readings`, { signal }).then(data),
  createReading: (propertyId, roomId, body) => apiRequest(`${room(propertyId, roomId)}/meter-readings`, { method: 'POST', body }).then(data),
  services: (signal) => apiRequest('/api/services', { signal }).then(data),
  createService: (body) => apiRequest('/api/services', { method: 'POST', body }).then(data),
  propertyServices: (propertyId, signal) => apiRequest(`${property(propertyId)}/services`, { signal }).then(data),
  createPropertyService: (propertyId, body) => apiRequest(`${property(propertyId)}/services`, { method: 'POST', body }).then(data),
  updateService: (propertyId, serviceId, body) => apiRequest(`${property(propertyId)}/services/${id(serviceId)}`, { method: 'PUT', body }).then(data),
  roomServices: (propertyId, roomId, signal) => apiRequest(`${room(propertyId, roomId)}/services`, { signal }).then(data),
  assignService: (propertyId, roomId, body) => apiRequest(`${room(propertyId, roomId)}/services`, { method: 'POST', body }).then(data),
  reviseRoomService: (propertyId, roomId, assignmentId, body) => apiRequest(`${room(propertyId, roomId)}/services/${id(assignmentId)}`, { method: 'PUT', body }).then(data),
  invoices: (propertyId, page = 1, signal, roomId) => apiRequest(`${property(propertyId)}/invoices?${params({ page, pageSize: 20, roomId })}`, { signal }),
  preview: (propertyId, body) => apiRequest(`${property(propertyId)}/invoices/monthly/preview`, { method: 'POST', body }).then(data),
  createMonthly: (propertyId, body) => apiRequest(`${property(propertyId)}/invoices/monthly`, { method: 'POST', body }).then(data),
  createDeposit: (contractId) => apiRequest(`/api/contracts/${id(contractId)}/invoices/deposit`, { method: 'POST' }).then(data),
  depositInvoice: (contractId, signal) => apiRequest(`/api/contracts/${id(contractId)}/invoices/deposit`, { signal }).then(data),
  invoice: (invoiceId, signal) => apiRequest(`/api/invoices/${id(invoiceId)}`, { signal }).then(data),
  issue: (invoiceId) => apiRequest(`/api/invoices/${id(invoiceId)}/issue`, { method: 'POST' }).then(data),
  cancel: (invoiceId) => apiRequest(`/api/invoices/${id(invoiceId)}/cancel`, { method: 'POST' }).then(data),
  myInvoices: (page = 1, signal) => apiRequest(`/api/me/invoices?${params({ page, pageSize: 20 })}`, { signal }),
  myInvoice: (invoiceId, signal) => apiRequest(`/api/me/invoices/${id(invoiceId)}`, { signal }).then(data),
  myPaymentAccount: (invoiceId, signal) => apiRequest(`/api/me/invoices/${id(invoiceId)}/payment-account`, { signal }).then(data),
  myPayments: (invoiceId, signal) => apiRequest(`/api/me/invoices/${id(invoiceId)}/payments`, { signal }).then(data),
  submitPayment: (invoiceId, body) => apiRequest(`/api/me/invoices/${id(invoiceId)}/payments`, { method: 'POST', body }).then(data),
  landlordPayments: (invoiceId, signal) => apiRequest(`/api/invoices/${id(invoiceId)}/payments`, { signal }).then(data),
  reviewPayment: (paymentId, body) => apiRequest(`/api/payments/${id(paymentId)}/review`, { method: 'POST', body }).then(data),
  recordReceived: (invoiceId, body) => apiRequest(`/api/invoices/${id(invoiceId)}/payments/received`, { method: 'POST', body }).then(data),
  overpaymentRefunds: (invoiceId, signal) => apiRequest(`/api/invoices/${id(invoiceId)}/overpayment-refunds`, { signal }).then(data),
  recordOverpaymentRefund: (invoiceId, body) => apiRequest(`/api/invoices/${id(invoiceId)}/overpayment-refunds`, { method: 'POST', body }).then(data),
  paymentEvidence: (paymentId, signal) => apiRequest(`/api/payments/${id(paymentId)}/evidence`, { signal }).then(data),
  uploadPaymentEvidence: async (paymentId, selection, timeoutMs = 20000) => {
    const body = new FormData()
    body.append('imageId', selection.id)
    body.append('file', selection.file)
    return data(await uploadWithTimeout(signal =>
      apiRequest(`/api/payments/${id(paymentId)}/evidence`, { method: 'POST', body, signal }), timeoutMs))
  },
  paymentEvidenceContent: (paymentId, imageId, signal) => apiRequest(`/api/payments/${id(paymentId)}/evidence/${id(imageId)}/content`, { signal, responseType: 'blob' }),
}
