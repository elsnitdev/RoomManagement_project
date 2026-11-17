using System.Data;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class ContractDraftExpiryService(RoomManagementDbContext db)
{
    public async Task<(int Cancelled, int NeedsReview)> CancelInactiveAsync(DateTime nowUtc, CancellationToken ct)
    {
        var cutoff = nowUtc.AddDays(-5);
        var candidates = await db.Contracts.AsNoTracking()
            .Where(c => c.Status == ContractStatus.Draft && (c.UpdatedAt ?? c.CreatedAt) <= cutoff)
            .OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.RoomId }).ToListAsync(ct);
        var cancelled = 0;
        var needsReview = 0;
        foreach (var candidate in candidates)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.Rooms.FromSqlInterpolated(
                $"SELECT * FROM dbo.Rooms WITH (UPDLOCK, HOLDLOCK) WHERE Id = {candidate.RoomId}")
                .AsNoTracking().SingleAsync(ct);
            var contract = await db.Contracts.FromSqlInterpolated(
                $"SELECT * FROM dbo.Contracts WITH (UPDLOCK, HOLDLOCK) WHERE Id = {candidate.Id}")
                .SingleAsync(ct);
            var lastActivity = contract.UpdatedAt ?? contract.CreatedAt;
            if (contract.UpdatedAt is null)
            {
                var lastMember = await db.ContractTenants.Where(t => t.ContractId == contract.Id)
                    .MaxAsync(t => (DateTime?)t.CreatedAt, ct);
                var lastDocument = await db.ContractDocuments.Where(d => d.ContractId == contract.Id)
                    .MaxAsync(d => (DateTime?)d.UploadedAt, ct);
                if (lastMember > lastActivity) lastActivity = lastMember.Value;
                if (lastDocument > lastActivity) lastActivity = lastDocument.Value;
            }
            if (contract.Status != ContractStatus.Draft || lastActivity > cutoff)
            {
                await transaction.CommitAsync(ct);
                continue;
            }

            var deposit = await db.Invoices.SingleOrDefaultAsync(i => i.ContractId == contract.Id &&
                i.InvoiceType == InvoiceType.Deposit && i.Status != InvoiceStatus.Cancelled, ct);
            if (deposit is not null && await InvoiceFinancialActivity.ExistsAsync(db, deposit, ct))
            {
                var ownership = await (from room in db.Rooms.AsNoTracking()
                                     join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                                     where room.Id == contract.RoomId
                                     select new { property.OwnerUserId, PropertyId = property.Id }).SingleAsync(ct);
                var eventKey = $"draft-expiry-review:{contract.Id}";
                if (!await db.UserNotifications.AnyAsync(n => n.UserId == ownership.OwnerUserId && n.EventKey == eventKey, ct))
                    db.UserNotifications.Add(new UserNotification
                    {
                        Id = Guid.NewGuid(), UserId = ownership.OwnerUserId, Type = "ContractDraftNeedsReview",
                        Title = $"Bản nháp {contract.ContractCode} cần đối soát cọc",
                        Message = "Bản nháp không được cập nhật trong 5 ngày và có giao dịch cọc. Vui lòng đối soát trước khi hủy.",
                        ContractId = contract.Id, PropertyId = ownership.PropertyId,
                        EventKey = eventKey, CreatedAt = nowUtc
                    });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                needsReview++;
                continue;
            }

            if (deposit is not null) deposit.Status = InvoiceStatus.Cancelled;
            contract.Status = ContractStatus.Cancelled;
            contract.ClosedOn = VietnamDate.Today;
            contract.ClosedAt = nowUtc;
            contract.ClosureReason = "Tự hủy do không cập nhật trong 5 ngày.";
            contract.UpdatedAt = nowUtc;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            cancelled++;
        }
        return (cancelled, needsReview);
    }
}
