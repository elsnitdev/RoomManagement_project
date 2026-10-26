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
