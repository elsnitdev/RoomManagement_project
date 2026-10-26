const apiBaseUrl = (import.meta.env?.VITE_API_BASE_URL || '').replace(/\/$/, '')

let accessToken = null

export class ApiError extends Error {
  constructor(message, status, details) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

export function setAccessToken(token) {
  accessToken = token || null
}

function errorMessage(data, status) {
  if (typeof data === 'object' && data !== null) {
    if (data.message) return data.message
    const validationMessage = Object.values(data.errors || {}).flat()[0]
    if (validationMessage) return validationMessage
    if (data.title) return data.title
  }
  return status === 401
    ? 'Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.'
    : 'Không thể xử lý yêu cầu. Vui lòng thử lại.'
}

export async function apiRequest(path, { method = 'GET', body, headers, signal, auth = true } = {}) {
  const requestHeaders = new Headers(headers)
  let requestBody

  if (body !== undefined) {
    if (typeof FormData !== 'undefined' && body instanceof FormData) {
      requestBody = body
    } else {
      requestHeaders.set('Content-Type', 'application/json')
      requestBody = JSON.stringify(body)
    }
  }

  if (auth && accessToken) requestHeaders.set('Authorization', `Bearer ${accessToken}`)

  let response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method,
      headers: requestHeaders,
      body: requestBody,
      signal,
    })
  } catch (error) {
    if (error.name === 'AbortError') throw error
    throw new ApiError('Không kết nối được máy chủ. Vui lòng thử lại sau.', 0, error)
  }

  const contentType = response.headers.get('content-type') || ''
  const responseText = response.status === 204 ? '' : await response.text()
  let data = responseText || null
  if (responseText && contentType.includes('json')) {
    try {
      data = JSON.parse(responseText)
    } catch {
      data = responseText
    }
  }

  if (!response.ok) {
    throw new ApiError(errorMessage(data, response.status), response.status, data)
  }

  return data
}
