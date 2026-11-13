using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;

namespace RoomManagement.Data;

public sealed class LandlordStore : ILandlordStore
{
    private readonly RoomManagementDbContext _db;

    public LandlordStore(RoomManagementDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<bool> IsActiveLandlordAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return _db.Users.AnyAsync(
            user => user.Id == userId
                && user.Role == UserRoles.Landlord
                && user.IsActive
                && user.DeletedAt == null
                && user.ApprovalStatus == ApprovalStatuses.Approved,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperatorContext?> GetOperatorContextAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId
                && u.IsActive
                && u.DeletedAt == null
                && !u.MustChangePassword
                && u.ApprovalStatus == ApprovalStatuses.Approved)
            .Select(u => new { u.Id, u.Role, u.LandlordUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        if (user.Role == UserRoles.Landlord)
            return new OperatorContext(user.Id, user.Id, UserRoles.Landlord, IsManager: false);

        if (user.Role == UserRoles.Manager && !string.IsNullOrWhiteSpace(user.LandlordUserId))
        {
            var landlordActive = await _db.Users.AsNoTracking().AnyAsync(
                l => l.Id == user.LandlordUserId
                    && l.Role == UserRoles.Landlord
                    && l.IsActive
                    && l.DeletedAt == null
                    && l.ApprovalStatus == ApprovalStatuses.Approved,
                cancellationToken);

            if (!landlordActive)
                return null;

            return new OperatorContext(user.Id, user.LandlordUserId, UserRoles.Manager, IsManager: true);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<bool> HasPropertyAccessAsync(
        string actorUserId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        var context = await GetOperatorContextAsync(actorUserId, cancellationToken);
        if (context is null)
            return false;

        if (!context.IsManager)
        {
            return await _db.Properties.AsNoTracking().AnyAsync(
                p => p.Id == propertyId && p.OwnerUserId == context.OwnerLandlordUserId,
                cancellationToken);
        }

        return await _db.Properties.AsNoTracking().AnyAsync(
            p => p.Id == propertyId
                && p.OwnerUserId == context.OwnerLandlordUserId
                && _db.PropertyManagerAssignments.Any(a =>
                    a.PropertyId == p.Id
                    && a.ManagerUserId == context.ActorUserId
                    && a.RevokedAt == null),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasContractAccessAsync(
        string actorUserId,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var propertyId = await (
            from c in _db.Contracts.AsNoTracking()
            join r in _db.Rooms.AsNoTracking() on c.RoomId equals r.Id
            where c.Id == contractId
            select (Guid?)r.PropertyId)
            .FirstOrDefaultAsync(cancellationToken);

        return propertyId.HasValue && await HasPropertyAccessAsync(actorUserId, propertyId.Value, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasInvoiceAccessAsync(
        string actorUserId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var propertyId = await (
            from i in _db.Invoices.AsNoTracking()
            join r in _db.Rooms.AsNoTracking() on i.RoomId equals r.Id
            where i.Id == invoiceId
            select (Guid?)r.PropertyId)
            .FirstOrDefaultAsync(cancellationToken);

        return propertyId.HasValue && await HasPropertyAccessAsync(actorUserId, propertyId.Value, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasPaymentAccessAsync(
        string actorUserId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var propertyId = await (
            from p in _db.Payments.AsNoTracking()
            join i in _db.Invoices.AsNoTracking() on p.InvoiceId equals i.Id
            join r in _db.Rooms.AsNoTracking() on i.RoomId equals r.Id
            where p.Id == paymentId
            select (Guid?)r.PropertyId)
            .FirstOrDefaultAsync(cancellationToken);

        return propertyId.HasValue && await HasPropertyAccessAsync(actorUserId, propertyId.Value, cancellationToken);
    }
}
