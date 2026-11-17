export function endOfMonth(monthValue) {
  if (!/^\d{4}-(0[1-9]|1[0-2])$/.test(monthValue)) {
    throw new RangeError('Tháng kết thúc không hợp lệ.')
  }

  const [year, month] = monthValue.split('-').map(Number)
  const date = new Date(0)
  date.setUTCFullYear(year, month, 0)
  return `${monthValue}-${String(date.getUTCDate()).padStart(2, '0')}`
}

export function toCreateContractRequest(form) {
  return {
    roomId: form.roomId,
    contractCode: form.contractCode.trim(),
    startDate: form.startDate,
    endDate: endOfMonth(form.endMonth),
    monthlyRent: Number(form.monthlyRent),
    depositAmount: Number(form.depositAmount),
    paymentDueDay: form.paymentDueDay ? Number(form.paymentDueDay) : null,
    note: form.note.trim() || null,
  }
}
