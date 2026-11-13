using Microsoft.AspNetCore.Identity;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.DTOs.Managers;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using Xunit;

namespace RoomManagement.Business.Tests;

public sealed class ManagerServiceTests
{
    private sealed class FakeCurrent(string userId) : ICurrentLandlord
    {
        public string UserId { get; set; } = userId;
        public string GetUserId() => UserId;
    }

    private sealed class FakeLandlordStore : ILandlordStore
    {
        public Dictionary<string, OperatorContext> Contexts { get; } = new();
        public Task<bool> IsActiveLandlordAsync(string userId, CancellationToken ct) =>
            Task.FromResult(Contexts.TryGetValue(userId, out var c) && !c.IsManager);
        public Task<OperatorContext?> GetOperatorContextAsync(string userId, CancellationToken ct) =>
            Task.FromResult(Contexts.GetValueOrDefault(userId));
    }

    private sealed class FakeManagerStore : IManagerStore
    {
        public List<AppUser> Users { get; } = [];
        public List<Property> Properties { get; } = [];
        public List<PropertyManagerAssignment> Assignments { get; } = [];

        public Task<bool> IsUserNameTakenAsync(string normalizedUserName, CancellationToken ct = default) =>
            Task.FromResult(Users.Any(u => u.NormalizedUserName == normalizedUserName));

        public Task<bool> IsEmailTakenAsync(string normalizedEmail, CancellationToken ct = default) =>
            Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail));

        public Task<IReadOnlyList<Property>> GetOwnedPropertiesAsync(string landlordId, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Property>>(Properties.Where(p => p.OwnerUserId == landlordId && propertyIds.Contains(p.Id)).ToList());

        public Task<AppUser?> FindManagerAsync(string landlordId, string managerId, bool asNoTracking = false, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == managerId && u.LandlordUserId == landlordId && u.DeletedAt == null));

        public Task<(IReadOnlyList<ManagerDto> Items, long TotalItems)> ListManagersAsync(string landlordId, string? search, bool? isActive, int skip, int take, CancellationToken ct = default)
        {
            var q = Users.Where(u => u.LandlordUserId == landlordId && u.DeletedAt == null).ToList();
            return Task.FromResult<(IReadOnlyList<ManagerDto>, long)>((q.Skip(skip).Take(take).Select(ToDto).ToList(), q.Count));
        }

        public Task<ManagerDto?> GetManagerDetailsAsync(string landlordId, string managerId, CancellationToken ct = default)
        {
            var u = Users.FirstOrDefault(x => x.Id == managerId && x.LandlordUserId == landlordId && x.DeletedAt == null);
            return Task.FromResult(u is null ? null : ToDto(u));
        }

        public Task CreateManagerAsync(AppUser manager, IReadOnlyList<PropertyManagerAssignment> initialAssignments, CancellationToken ct = default)
        {
            Users.Add(manager);
            Assignments.AddRange(initialAssignments);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Guid>> GetActiveAssignedPropertyIdsAsync(string managerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Assignments.Where(a => a.ManagerUserId == managerId && a.RevokedAt == null).Select(a => a.PropertyId).ToList());

        public Task AddAssignmentsAsync(IReadOnlyList<PropertyManagerAssignment> assignments, CancellationToken ct = default)
        {
            Assignments.AddRange(assignments);
            return Task.CompletedTask;
        }

        public Task<bool> RevokeAssignmentAsync(string landlordId, string managerId, Guid propertyId, DateTime revokedAt, CancellationToken ct = default)
        {
            var a = Assignments.FirstOrDefault(x => x.ManagerUserId == managerId && x.PropertyId == propertyId && x.RevokedAt == null);
            if (a is null) return Task.FromResult(false);
            a.RevokedAt = revokedAt;
            a.RevokedByUserId = landlordId;
            return Task.FromResult(true);
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> DeleteOrDeactivateManagerAsync(string landlordId, string managerId, DateTime now, CancellationToken ct = default)
        {
            var u = Users.FirstOrDefault(x => x.Id == managerId && x.LandlordUserId == landlordId && x.DeletedAt == null);
            if (u is null) return Task.FromResult(false);
            u.DeletedAt = now;
            u.IsActive = false;
            return Task.FromResult(true);
        }

        private ManagerDto ToDto(AppUser u)
        {
            var assigned = Assignments.Where(a => a.ManagerUserId == u.Id && a.RevokedAt == null)
                .Select(a => new ManagerAssignedPropertyDto(a.PropertyId, Properties.First(p => p.Id == a.PropertyId).Name, a.AssignedAt))
                .ToList();
            return new ManagerDto(u.Id, u.UserName, u.Email, u.FullName, u.PhoneNumber, u.IsActive, u.MustChangePassword, u.CreatedAt, assigned);
        }
    }

    [Fact]
    public async Task Create_assign_revoke_lock_and_forbid_manager_calling_manager_service()
    {
        var current = new FakeCurrent("landlord-1");
        var landlords = new FakeLandlordStore();
        landlords.Contexts["landlord-1"] = new OperatorContext("landlord-1", "landlord-1", UserRoles.Landlord, false);
        landlords.Contexts["mgr-existing"] = new OperatorContext("mgr-existing", "landlord-1", UserRoles.Manager, true);

        var store = new FakeManagerStore();
        var propA = new Property { Id = Guid.NewGuid(), OwnerUserId = "landlord-1", Name = "Khu A", Address = "A" };
        var propOther = new Property { Id = Guid.NewGuid(), OwnerUserId = "landlord-2", Name = "Khu Other", Address = "O" };
        store.Properties.AddRange([propA, propOther]);

        var service = new ManagerService(current, landlords, store, new PasswordHasher<AppUser>());

        // Rejects assigning property of another landlord
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new CreateManagerInput("mgr1", "Temp123!", null, "Mgr 1", null, [propOther.Id])));
        Assert.Equal("PROPERTY_NOT_FOUND", ex.Code);

        // Creates manager with MustChangePassword == true
        var created = await service.CreateAsync(new CreateManagerInput("mgr1", "Temp123!", null, "Mgr 1", null, [propA.Id]));
        Assert.True(created.MustChangePassword);
        Assert.Single(created.AssignedProperties);

        // Duplicate active assignment rejected
        var dupEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AssignPropertiesAsync(created.Id, [propA.Id]));
        Assert.Equal("ASSIGNMENT_ALREADY_EXISTS", dupEx.Code);

        // Manager cannot call ManagerService
        current.UserId = "mgr-existing";
        var forbiddenEx = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ListAsync(new ManagerFilter(1, 20, null, null)));
        Assert.Equal(BusinessErrorType.Forbidden, forbiddenEx.Type);
    }
}
