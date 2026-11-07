import test from 'node:test'
import assert from 'node:assert/strict'
import { endOfMonth, toCreateContractRequest, unavailableTenantIds } from './contractForm.js'

test('endOfMonth handles short, leap, and year-end months', () => {
  assert.equal(endOfMonth('2026-02'), '2026-02-28')
  assert.equal(endOfMonth('2028-02'), '2028-02-29')
  assert.equal(endOfMonth('2026-11'), '2026-11-30')
  assert.equal(endOfMonth('2026-12'), '2026-12-31')
  assert.throws(() => endOfMonth('2026-13'), RangeError)
})

test('tenant picker excludes current members and overlapping active contracts', () => {
  const contracts = [
    { id: 'active', status: 'Active', startDate: '2026-01-01', endDate: '2026-12-31', tenants: [
      { tenantId: 'occupied', joinDate: '2026-01-01', leaveDate: null },
      { tenantId: 'left', joinDate: '2026-01-01', leaveDate: '2026-02-01' },
    ] },
    { id: 'draft', status: 'Draft', startDate: '2026-01-01', endDate: '2026-12-31', tenants: [
      { tenantId: 'draft-only', joinDate: '2026-01-01', leaveDate: null },
    ] },
  ]
  const ids = unavailableTenantIds(contracts, '2026-11-07', '2027-01-01', {
    id: 'current', tenants: [{ tenantId: 'already-here', leaveDate: null }],
  })
  assert.deepEqual([...ids].sort(), ['already-here', 'occupied'])
  assert.equal(unavailableTenantIds(contracts, '2027-01-01', '2027-12-31').has('occupied'), false)
})

test('contract request sends the last day of the selected month', () => {
  assert.deepEqual(toCreateContractRequest({
    roomId: '11111111-1111-1111-1111-111111111111',
    contractCode: ' HD-A101-2026 ',
    startDate: '2026-11-06',
    endMonth: '2027-02',
    monthlyRent: '3000000',
    depositAmount: '3000000',
    paymentDueDay: '5',
    note: ' Hợp đồng 4 tháng ',
  }), {
    roomId: '11111111-1111-1111-1111-111111111111',
    contractCode: 'HD-A101-2026',
    startDate: '2026-11-06',
    endDate: '2027-02-28',
    monthlyRent: 3000000,
    depositAmount: 3000000,
    paymentDueDay: 5,
    note: 'Hợp đồng 4 tháng',
  })
})
