import { apiRequest } from '../apiClient'

export function login(credentials) {
  return apiRequest('/api/auth/login', {
    method: 'POST',
    body: credentials,
    auth: false,
  })
}

export function registerLandlord(data) {
  return apiRequest('/api/auth/register-landlord', {
    method: 'POST',
    body: data,
    auth: false,
  })
}

export function checkTenantRegistration(landlordCode, phoneNumber) {
  return apiRequest('/api/auth/tenant-registration/check', {
    method: 'POST', body: { landlordCode, phoneNumber }, auth: false,
  })
}

export function requestTenantOtp(landlordCode, phoneNumber) {
  return apiRequest('/api/auth/tenant-registration/request-otp', {
    method: 'POST', body: { landlordCode, phoneNumber }, auth: false,
  })
}

export function verifyTenantOtp(landlordCode, phoneNumber, challengeId, code) {
  return apiRequest('/api/auth/tenant-registration/verify-otp', {
    method: 'POST', body: { landlordCode, phoneNumber, challengeId, code }, auth: false,
  })
}

export function registerTenant(data) {
  return apiRequest('/api/auth/register-tenant', {
    method: 'POST', body: data, auth: false,
  })
}
