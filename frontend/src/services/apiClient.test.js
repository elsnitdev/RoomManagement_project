import test from 'node:test'
import assert from 'node:assert/strict'
import { apiRequest, setAccessToken } from './apiClient.js'

test('apiRequest restores the bearer token from a valid tab session', async () => {
  const previousStorage = globalThis.sessionStorage
  const previousFetch = globalThis.fetch
  let authorization
  globalThis.sessionStorage = {
    getItem: () => JSON.stringify({
      accessToken: 'test-token',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { id: 'landlord-1', role: 'Landlord' },
    }),
  }
  globalThis.fetch = async (_url, options) => {
    authorization = options.headers.get('Authorization')
    return new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } })
  }

  try {
    setAccessToken(null)
    await apiRequest('/api/properties/test/contracts')
    assert.equal(authorization, 'Bearer test-token')
  } finally {
    setAccessToken(null)
    globalThis.sessionStorage = previousStorage
    globalThis.fetch = previousFetch
  }
})
