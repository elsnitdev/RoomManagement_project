import test from 'node:test'
import assert from 'node:assert/strict'
import { endOfMonth, toCreateContractRequest } from './contractForm.js'

test('endOfMonth handles short, leap, and year-end months', () => {
  assert.equal(endOfMonth('2026-02'), '2026-02-28')
  assert.equal(endOfMonth('2028-02'), '2028-02-29')
  assert.equal(endOfMonth('2026-11'), '2026-11-30')
  assert.equal(endOfMonth('2026-12'), '2026-12-31')
  assert.throws(() => endOfMonth('2026-13'), RangeError)
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
