import { readAuthSession } from '../contexts/authSession.js'

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
  if (status === 503 && data?.code === 'SMS_NOT_CONFIGURED') {
    return 'Chưa cấu hình dịch vụ SMS nên chưa thể xác minh số điện thoại. Vui lòng liên hệ quản trị viên.'
  }
  if (status === 502 || status === 503 || status === 504) {
    return 'Máy chủ chưa sẵn sàng. Vui lòng thử lại sau.'
  }
  if (typeof data === 'object' && data !== null) {
    if (data.message) return data.message
    const validationMessage = Object.values(data.errors || {}).flat()[0]
    if (validationMessage) return validationMessage
    if (data.detail) return data.detail
    if (data.title) return data.title
  }
  return status === 401
    ? 'Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.'
    : 'Không thể xử lý yêu cầu. Vui lòng thử lại.'
}

export async function apiRequest(path, { method = 'GET', body, headers, signal, auth = true, responseType } = {}) {
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

  const token = auth ? readAuthSession()?.accessToken || accessToken : null
  if (token) requestHeaders.set('Authorization', `Bearer ${token}`)

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

  if (response.ok && responseType === 'blob') return response.blob()
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
    if (auth && response.status === 401) {
      setAccessToken(null)
      window.dispatchEvent(new Event('auth:unauthorized'))
    }
    throw new ApiError(errorMessage(data, response.status), response.status, data)
  }

  return data
}
