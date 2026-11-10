export function paymentAppliedToInvoice(summary) {
  return Math.min(Math.max(0, Number(summary.totalPaid || 0)),
    Math.max(0, Number(summary.effectiveTotal || 0) - Number(summary.amountDue || 0) - Number(summary.totalDepositApplied || 0)))
}
