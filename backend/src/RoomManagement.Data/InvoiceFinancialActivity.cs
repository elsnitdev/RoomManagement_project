using Microsoft.EntityFrameworkCore;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

internal static class InvoiceFinancialActivity
{
    internal static async Task<bool> ExistsAsync(RoomManagementDbContext db, Invoice invoice, CancellationToken ct) =>
        await db.Payments.AnyAsync(x => x.InvoiceId == invoice.Id, ct) ||
        await db.Refunds.AnyAsync(x => x.InvoiceId == invoice.Id, ct) ||
        await db.InvoiceAdjustments.AnyAsync(x => x.InvoiceId == invoice.Id, ct) ||
        await db.DepositApplications.AnyAsync(x => x.TargetInvoiceId == invoice.Id, ct) ||
        invoice.InvoiceType == InvoiceType.Deposit &&
            await db.DepositApplications.AnyAsync(x => x.ContractId == invoice.ContractId, ct);
}
