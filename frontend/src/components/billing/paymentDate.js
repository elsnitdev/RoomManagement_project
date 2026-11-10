const zone = 'Asia/Ho_Chi_Minh'

export const vietnamToday = () => new Intl.DateTimeFormat('sv-SE', {
  timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit',
}).format(new Date())

export const paymentDateToIso = date => new Date(`${date}T00:00:00+07:00`).toISOString()

export function formatVietnamDate(value, withTime = false) {
  if (!value) return '—'
  const utc = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`
  const date = new Date(utc)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('vi-VN', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', ...(withTime ? { hour: '2-digit', minute: '2-digit' } : {}) }).format(date)
}
