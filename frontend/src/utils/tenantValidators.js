import { PHONE_REGEX, CITIZEN_ID_REGEX, EMAIL_REGEX, TENANT_GENDERS } from '../constants/tenants'

/**
 * Kiểm tra tính hợp lệ của dữ liệu hồ sơ người thuê trước khi gửi lên API / lưu trữ.
 * Khớp hoàn toàn với quy tắc kiểm tra tại TenantProfileRules của Backend:
 * - Họ tên: Bắt buộc, không để trống, tối đa 150 ký tự.
 * - Số điện thoại: Bắt buộc, 10 số bắt đầu bằng số 0.
 * - Email: Tùy chọn, đúng định dạng nếu có, tối đa 150 ký tự.
 * - CCCD: Tùy chọn, đúng 12 chữ số nếu có.
 * - Ngày sinh: Tùy chọn, phải trong quá khứ nếu có.
 * - Giới tính: Tùy chọn, nếu có phải thuộc Male, Female, Other.
 * - Địa chỉ thường trú: Tùy chọn, tối đa 300 ký tự nếu có.
 */
export function validateTenantProfile(draft) {
  const errors = {}

  const fullName = (draft.fullName || '').trim()
  if (!fullName) {
    errors.fullName = 'Họ và tên là bắt buộc.'
  } else if (fullName.length > 150) {
    errors.fullName = 'Họ và tên không được vượt quá 150 ký tự.'
  }

  const phoneNumber = (draft.phoneNumber || '').trim()
  if (!phoneNumber) {
    errors.phoneNumber = 'Số điện thoại là bắt buộc.'
  } else if (!PHONE_REGEX.test(phoneNumber)) {
    errors.phoneNumber = 'Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0 (ví dụ: 0912345678).'
  }

  const email = (draft.email || '').trim()
  if (email) {
    if (email.length > 150) {
      errors.email = 'Email không được vượt quá 150 ký tự.'
    } else if (!EMAIL_REGEX.test(email)) {
      errors.email = 'Địa chỉ email không đúng định dạng.'
    }
  }

  const citizenId = (draft.citizenId || '').trim()
  if (citizenId) {
    if (!CITIZEN_ID_REGEX.test(citizenId)) {
      errors.citizenId = 'Số CCCD phải gồm đúng 12 chữ số.'
    }
  }

  if (draft.dateOfBirth) {
    const dob = new Date(draft.dateOfBirth)
    const today = new Date()
    today.setHours(0, 0, 0, 0)
    if (isNaN(dob.getTime()) || dob >= today) {
      errors.dateOfBirth = 'Ngày sinh phải là một ngày hợp lệ trong quá khứ.'
    }
  }

  if (draft.gender && !Object.values(TENANT_GENDERS).includes(draft.gender)) {
    errors.gender = 'Giới tính không hợp lệ (chỉ chấp nhận Nam, Nữ hoặc Khác).'
  }

  const permanentAddress = (draft.permanentAddress || '').trim()
  if (permanentAddress && permanentAddress.length > 300) {
    errors.permanentAddress = 'Địa chỉ thường trú không được vượt quá 300 ký tự.'
  }

  return {
    isValid: Object.keys(errors).length === 0,
    errors,
  }
}
