export const TENANT_GENDERS = {
  MALE: 'Male',
  FEMALE: 'Female',
  OTHER: 'Other',
}

export const GENDER_OPTIONS = [
  { value: '', label: 'Chưa xác định' },
  { value: TENANT_GENDERS.MALE, label: 'Nam' },
  { value: TENANT_GENDERS.FEMALE, label: 'Nữ' },
  { value: TENANT_GENDERS.OTHER, label: 'Khác' },
]

export const GENDER_LABELS = {
  [TENANT_GENDERS.MALE]: 'Nam',
  [TENANT_GENDERS.FEMALE]: 'Nữ',
  [TENANT_GENDERS.OTHER]: 'Khác',
}

export const PHONE_REGEX = /^0\d{9}$/
export const CITIZEN_ID_REGEX = /^\d{12}$/
export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
