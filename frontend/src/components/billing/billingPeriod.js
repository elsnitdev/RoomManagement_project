export function previousBillingMonth() {
  const vietnamToday = new Date(Date.now() + 7 * 3600_000);
  return new Date(Date.UTC(vietnamToday.getUTCFullYear(), vietnamToday.getUTCMonth() - 1, 1))
    .toISOString().slice(0, 7);
}
