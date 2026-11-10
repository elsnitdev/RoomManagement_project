using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

internal static class InvoiceAccessQueries
{
    /// <summary>Hóa đơn thuộc khu trọ của chủ trọ.</summary>
    internal static IQueryable<Invoice> OwnedInvoices(RoomManagementDbContext db, string ownerId) =>
        from invoice in db.Invoices
        join room in db.Rooms on invoice.RoomId equals room.Id
        join property in db.Properties on room.PropertyId equals property.Id
        where property.OwnerUserId == ownerId
        select invoice;

    /// <summary>Hóa đơn đã phát hành thuộc hợp đồng mà tài khoản người thuê tham gia.</summary>
    internal static IQueryable<Invoice> TenantInvoices(RoomManagementDbContext db, string tenantUserId) =>
        db.Invoices.Where(invoice => invoice.Status == InvoiceStatus.Issued &&
            db.ContractTenants.Any(link => link.ContractId == invoice.ContractId &&
                (invoice.InvoiceType == InvoiceType.Deposit ||
                 (invoice.InvoiceType == InvoiceType.Monthly
                    ? invoice.BillingYear != null && invoice.BillingMonth != null &&
                      (link.JoinDate.Year < invoice.BillingYear ||
                       link.JoinDate.Year == invoice.BillingYear && link.JoinDate.Month <= invoice.BillingMonth) &&
                      (link.LeaveDate == null || link.LeaveDate.Value.Year > invoice.BillingYear ||
                       link.LeaveDate.Value.Year == invoice.BillingYear && link.LeaveDate.Value.Month >= invoice.BillingMonth)
                    : link.JoinDate <= invoice.IssueDate &&
                      (link.LeaveDate == null || link.LeaveDate >= invoice.IssueDate))) &&
                db.TenantProfiles.Any(profile => profile.Id == link.TenantId &&
                    profile.UserId == tenantUserId)));
}
