import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'

const BASE_PATH = '/api/landlord/tenants'

/**
 * Lấy danh sách hồ sơ người thuê do chủ trọ quản lý (hỗ trợ phân trang và tìm kiếm theo từ khóa).
 * Backend trả về cấu trúc phân trang: { data: TenantProfileResponse[], meta: { total, page, pageSize } }
 */
export function getLandlordTenants({ keyword = '', page = 1, pageSize = 10 } = {}, options = {}) {
  const params = new URLSearchParams()
  if (keyword && keyword.trim()) params.set('keyword', keyword.trim())
  if (page) params.set('page', page.toString())
  if (pageSize) params.set('pageSize', pageSize.toString())

  const queryString = params.toString()
  const path = queryString ? `${BASE_PATH}?${queryString}` : BASE_PATH
  return apiRequest(path, options)
}

/**
 * Lấy chi tiết một hồ sơ người thuê theo ID.
 * Backend trả về cấu trúc: { data: TenantProfileResponse }
 */
export function getLandlordTenantById(id, options = {}) {
  return apiRequest(`${BASE_PATH}/${encodeURIComponent(id)}`, options)
}

/**
 * Tạo hồ sơ người thuê mới (kể cả khi chưa có tài khoản đăng nhập).
 * Backend trả về: { data: TenantProfileResponse, message: string }
 */
export async function createLandlordTenant(payload) {
  const result = await apiRequest(BASE_PATH, {
    method: 'POST',
    body: payload,
  })
  // Hủy toàn bộ cache liên quan đến danh sách người thuê của chủ trọ
  invalidateApiCache('landlord:tenants')
  return result
}

/**
 * Cập nhật thông tin hồ sơ người thuê đã tồn tại.
 * Backend trả về: { data: TenantProfileResponse, message: string }
 */
export async function updateLandlordTenant(id, payload) {
  const result = await apiRequest(`${BASE_PATH}/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body: payload,
  })
  // Hủy cache chi tiết và cache danh sách
  invalidateApiCache(`landlord:tenant:${id}`)
  invalidateApiCache('landlord:tenants')
  return result
}

/**
 * Chuyển đổi lỗi API (ApiError / ProblemDetails) thành { generalError, fieldErrors }
 * tương thích với biểu mẫu quản lý hồ sơ người thuê.
 */
export function mapTenantApiError(error) {
  const fieldErrors = {}
  const details = error?.details || {}
  const code = details.code || ''
  const message = error?.message || details.detail || details.message || 'Đã có lỗi xảy ra khi xử lý hồ sơ người thuê.'

  // 1. Ánh xạ từ ASP.NET ValidationProblemDetails (details.errors) nếu có
  if (details.errors && typeof details.errors === 'object') {
    const fieldKeyMap = {
      FullName: 'fullName',
      fullName: 'fullName',
      PhoneNumber: 'phoneNumber',
      phoneNumber: 'phoneNumber',
      Email: 'email',
      email: 'email',
      CitizenId: 'citizenId',
      citizenId: 'citizenId',
      DateOfBirth: 'dateOfBirth',
      dateOfBirth: 'dateOfBirth',
      Gender: 'gender',
      gender: 'gender',
      PermanentAddress: 'permanentAddress',
      permanentAddress: 'permanentAddress',
    }
    for (const [key, messages] of Object.entries(details.errors)) {
      const targetField = fieldKeyMap[key]
      const firstMsg = Array.isArray(messages) ? messages[0] : messages
      if (targetField && firstMsg) {
        fieldErrors[targetField] = firstMsg
      }
    }
  }

  // 2. Xử lý lỗi xung đột CCCD (409 Conflict / CITIZEN_ID_CONFLICT)
  if (error?.status === 409 || code === 'CITIZEN_ID_CONFLICT') {
    fieldErrors.citizenId = message
  }

  // 3. Phân tích thông điệp lỗi từ AppValidationException (400 Bad Request) để gán đúng trường
  if (error?.status === 400 || code === 'VALIDATION_FAILED' || code === 'VALIDATION_ERROR') {
    const lowerMsg = message.toLowerCase()
    if (lowerMsg.includes('họ và tên') && !fieldErrors.fullName) {
      fieldErrors.fullName = message
    } else if (lowerMsg.includes('số điện thoại') && !fieldErrors.phoneNumber) {
      fieldErrors.phoneNumber = message
    } else if (lowerMsg.includes('email') && !fieldErrors.email) {
      fieldErrors.email = message
    } else if (lowerMsg.includes('cccd') && !fieldErrors.citizenId) {
      fieldErrors.citizenId = message
    } else if (lowerMsg.includes('ngày sinh') && !fieldErrors.dateOfBirth) {
      fieldErrors.dateOfBirth = message
    } else if (lowerMsg.includes('giới tính') && !fieldErrors.gender) {
      fieldErrors.gender = message
    } else if (lowerMsg.includes('địa chỉ') && !fieldErrors.permanentAddress) {
      fieldErrors.permanentAddress = message
    }
  }

  return {
    generalError: message,
    fieldErrors,
  }
}

