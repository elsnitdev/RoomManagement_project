import { apiRequest } from '../apiClient'
import { invalidateApiCache } from '../apiCache'

const BASE_PATH = '/api/landlord/tenants'

/**
 * Lấy danh sách hồ sơ người thuê do chủ trọ quản lý (hỗ trợ phân trang và tìm kiếm theo từ khóa).
 * Backend trả về cấu trúc phân trang: { data: TenantProfileResponse[], meta: { total, page, pageSize, totalPages } }
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
