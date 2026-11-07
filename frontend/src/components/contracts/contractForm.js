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

export function unavailableTenantIds(contracts, startDate, endDate, currentContract) {
  const unavailable = new Set(
    (currentContract?.tenants || []).filter(member => !member.leaveDate).map(member => member.tenantId),
  )
  if (!startDate) return unavailable
  for (const contract of contracts) {
    if (contract.status !== 'Active' || contract.id === currentContract?.id ||
        contract.startDate > (endDate || '9999-12-31') ||
        (contract.endDate && contract.endDate < startDate)) continue
    for (const member of contract.tenants || []) {
      if (member.joinDate <= (endDate || '9999-12-31') &&
          (!member.leaveDate || member.leaveDate >= startDate)) unavailable.add(member.tenantId)
    }
  }
  return unavailable
}
