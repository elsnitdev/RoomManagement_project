using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Data;
using RoomManagement.Data.Seeders;
using RoomManagement.Data.Stores;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

public sealed class SqlServerBehaviorTests
{
    [SqlServerFact]
    public async Task Profile_migration_preserves_existing_main_accounts()
    {
        await using var database = await SqlServerDatabase.CreateAsync(migrate: false);
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync("20261022052413_InitialSchema");
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO dbo.Users (Id, UserName, NormalizedUserName, PasswordHash, Role, IsActive, CreatedAt)
            VALUES ('legacy-user', 'legacy', 'LEGACY', 'legacy-hash', 'Tenant', 1, SYSUTCDATETIME())
            """);
        await context.Database.MigrateAsync();
        var user = await context.Users.SingleAsync(u => u.Id == "legacy-user");
        Assert.Equal(ApprovalStatuses.Approved, user.ApprovalStatus);
        Assert.True(user.IsActive);
        Assert.Null(user.CitizenId);
    }

    [SqlServerFact]
    public async Task Registration_persists_pending_and_inactive_and_email_boundary()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new UserService(new UserStore(context), new PasswordHasher<AppUser>(), NullLogger<UserService>.Instance);
        var result = await service.RegisterLandlordAsync(new RegisterLandlordDto
        {
            UserName = "new-landlord", Email = new string('a', 244) + "@example.com", Password = TestUserStore.Password,
            FullName = "Test Landlord", PhoneNumber = "0912345678", CitizenId = "012345678901",
            DateOfBirth = new DateOnly(1990, 1, 1), Address = "Test Address"
        });
        context.ChangeTracker.Clear();
        var saved = await context.Users.SingleAsync(u => u.Id == result.Id);
        Assert.False(saved.IsActive);
        Assert.Equal(ApprovalStatuses.Pending, saved.ApprovalStatus);
        Assert.Equal(256, saved.Email!.Length);
    }

    [SqlServerFact]
    public async Task Concurrent_approve_and_reject_only_allow_one_success()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var landlord = CreateUser();
        landlord.Role = UserRoles.Landlord;
        landlord.ApprovalStatus = ApprovalStatuses.Pending;
        landlord.IsActive = false;
        await using (var seed = database.CreateContext())
        {
            seed.Users.Add(landlord);
            await seed.SaveChangesAsync();
        }

        await using var approveContext = database.CreateContext();
        await using var rejectContext = database.CreateContext();
        var gate = new TwoReadersGate();
        var approve = new LandlordApprovalService(new SynchronizedApprovalStore(new LandlordApprovalStore(approveContext), gate), NullLogger<LandlordApprovalService>.Instance);
        var reject = new LandlordApprovalService(new SynchronizedApprovalStore(new LandlordApprovalStore(rejectContext), gate), NullLogger<LandlordApprovalService>.Instance);
        var outcomes = await Task.WhenAll(
            Record.ExceptionAsync(() => approve.ApproveAsync(landlord.Id)),
            Record.ExceptionAsync(() => reject.RejectAsync(landlord.Id, "Test rejection")));
        Assert.Single(outcomes, error => error is null);
        Assert.Single(outcomes, error => error is AppConflictException);

        await using var verify = database.CreateContext();
        var saved = await verify.Users.SingleAsync(u => u.Id == landlord.Id);
        Assert.NotNull(saved.ReviewedAt);
        Assert.Equal(saved.ApprovalStatus == ApprovalStatuses.Approved, saved.IsActive);
        Assert.Equal(saved.ApprovalStatus == ApprovalStatuses.Rejected ? "Test rejection" : null, saved.RejectionReason);
    }

    [SqlServerFact]
    public async Task Concurrent_duplicate_user_writes_translate_only_unique_failures_to_conflict()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        foreach (var duplicateField in new[] { "username", "email", "citizenId" })
        {
            await using var firstContext = database.CreateContext();
            await using var secondContext = database.CreateContext();
            var first = CreateUser();
            var second = CreateUser();
            if (duplicateField == "username") second.NormalizedUserName = first.NormalizedUserName;
            if (duplicateField == "email") second.NormalizedEmail = first.NormalizedEmail;
            if (duplicateField == "citizenId") first.CitizenId = second.CitizenId = "012345678901";
            var firstStore = new UserStore(firstContext);
            var secondStore = new UserStore(secondContext);
            await firstStore.AddAsync(first);
            await secondStore.AddAsync(second);
            var outcomes = await Task.WhenAll(
                Record.ExceptionAsync(() => firstStore.SaveChangesAsync()),
                Record.ExceptionAsync(() => secondStore.SaveChangesAsync()));
            Assert.Single(outcomes, error => error is null);
            Assert.Single(outcomes, error => error is AppConflictException);
        }

        await using var invalidContext = database.CreateContext();
        var invalid = CreateUser();
        invalid.Role = "InvalidRole";
        var invalidStore = new UserStore(invalidContext);
        await invalidStore.AddAsync(invalid);
        await Assert.ThrowsAsync<DbUpdateException>(() => invalidStore.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Concurrent_profile_citizen_id_updates_report_conflict()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var first = CreateUser();
        var second = CreateUser();
        await using (var seed = database.CreateContext())
        {
            seed.Users.AddRange(first, second);
            await seed.SaveChangesAsync();
        }
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstStore = new UserStore(firstContext);
        var secondStore = new UserStore(secondContext);
        (await firstStore.FindByIdAsync(first.Id))!.CitizenId = "012345678901";
        (await secondStore.FindByIdAsync(second.Id))!.CitizenId = "012345678901";
        var outcomes = await Task.WhenAll(
            Record.ExceptionAsync(() => firstStore.SaveChangesAsync()),
            Record.ExceptionAsync(() => secondStore.SaveChangesAsync()));
        Assert.Single(outcomes, error => error is null);
        Assert.Single(outcomes, error => error is AppConflictException);
    }

    [SqlServerFact]
    public async Task Admin_seeding_rejects_collisions_and_never_resets_existing_admin()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var user = CreateUser();
        user.UserName = "admin";
        user.NormalizedUserName = "ADMIN";
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var settings = new InitialAdminOptions { UserName = "admin", Email = "admin@example.com", Password = TestUserStore.Password };
        var seeder = new AdminUserSeeder(context, new PasswordHasher<AppUser>(), Options.Create(settings), NullLogger<AdminUserSeeder>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
        Assert.Equal(UserRoles.Tenant, (await context.Users.SingleAsync()).Role);

        settings.UserName = "bootstrap-admin";
        await seeder.SeedAsync();
        var admin = await context.Users.SingleAsync(u => u.Role == UserRoles.Admin);
        var originalHash = admin.PasswordHash;
        settings.Password = "AnotherTestPassword!42";
        await seeder.SeedAsync();
        context.ChangeTracker.Clear();
        admin = await context.Users.SingleAsync(u => u.Role == UserRoles.Admin);
        Assert.Equal(originalHash, admin.PasswordHash);
        Assert.True(admin.IsActive);
        Assert.Equal(ApprovalStatuses.Approved, admin.ApprovalStatus);
    }

    [SqlServerFact]
    public async Task Ambiguous_username_and_email_cannot_select_an_arbitrary_account()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var first = CreateUser();
        var second = CreateUser();
        second.UserName = first.Email!;
        second.NormalizedUserName = first.NormalizedEmail!;
        context.Users.AddRange(first, second);
        await context.SaveChangesAsync();
        Assert.Null(await new UserStore(context).FindByLoginAsync(first.NormalizedEmail!));
        Assert.NotNull(await new UserStore(context).FindByLoginAsync(first.NormalizedUserName));
    }

    [SqlServerFact]
    public async Task Tenant_profile_persists_default_created_at_and_null_user_id()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var landlord = CreateUser();
        landlord.Role = UserRoles.Landlord;
        context.Users.Add(landlord);
        await context.SaveChangesAsync();

        var store = new TenantProfileStore(context);
        var profile = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlord.Id,
            FullName = "Khách Hàng Mới",
            PhoneNumber = "0901234567",
            CreatedAt = DateTime.UtcNow
        };
        await store.AddAsync(profile);
        await store.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var saved = await context.TenantProfiles.SingleAsync(p => p.Id == profile.Id);
        Assert.Null(saved.UserId);
        Assert.Equal(landlord.Id, saved.CreatedByUserId);
        Assert.NotEqual(default, saved.CreatedAt);
    }

    [SqlServerFact]
    public async Task Concurrent_tenant_profile_writes_translate_unique_citizen_id_failure_to_conflict()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        await using var seedContext = database.CreateContext();
        var landlord = CreateUser();
        landlord.Role = UserRoles.Landlord;
        seedContext.Users.Add(landlord);
        await seedContext.SaveChangesAsync();

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var first = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlord.Id,
            FullName = "Người 1",
            PhoneNumber = "0901111111",
            CitizenId = "079201012345",
            CreatedAt = DateTime.UtcNow
        };
        var second = new TenantProfile
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = landlord.Id,
            FullName = "Người 2",
            PhoneNumber = "0902222222",
            CitizenId = "079201012345",
            CreatedAt = DateTime.UtcNow
        };

        var firstStore = new TenantProfileStore(firstContext);
        var secondStore = new TenantProfileStore(secondContext);
        await firstStore.AddAsync(first);
        await secondStore.AddAsync(second);

        var outcomes = await Task.WhenAll(
            Record.ExceptionAsync(() => firstStore.SaveChangesAsync()),
            Record.ExceptionAsync(() => secondStore.SaveChangesAsync()));

        Assert.Single(outcomes, error => error is null);
        Assert.Single(outcomes, error => error is AppConflictException);
    }

    [SqlServerFact]
    public async Task Tenant_profile_migration_preserves_existing_profiles()
    {
        await using var database = await SqlServerDatabase.CreateAsync(migrate: false);
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync("20261024150734_AddUserProfilesAndApproval");
        var id = Guid.NewGuid();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO dbo.TenantProfiles (Id, FullName, PhoneNumber, CreatedAt) VALUES ({0}, N'Legacy Tenant', '0901234567', SYSUTCDATETIME())", id);
        await context.Database.MigrateAsync();
        var saved = await context.TenantProfiles.SingleAsync(p => p.Id == id);
        Assert.Equal("Legacy Tenant", saved.FullName);
        Assert.Null(saved.CreatedByUserId);
        Assert.Null(saved.UserId);
    }

    private static AppUser CreateUser()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new AppUser
        {
            Id = Guid.NewGuid().ToString(), UserName = suffix, NormalizedUserName = suffix.ToUpperInvariant(),
            Email = suffix + "@example.com", NormalizedEmail = suffix.ToUpperInvariant() + "@EXAMPLE.COM",
            PasswordHash = "NotUsedForLoginInDatabaseTests", Role = UserRoles.Tenant, IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved, CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class TwoReadersGate
    {
        private int _readers;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync()
        {
            if (Interlocked.Increment(ref _readers) == 2) _ready.SetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private sealed class SynchronizedApprovalStore(ILandlordApprovalStore inner, TwoReadersGate gate) : ILandlordApprovalStore
    {
        public Task<IReadOnlyList<AppUser>> GetPendingLandlordsAsync(CancellationToken cancellationToken = default) => inner.GetPendingLandlordsAsync(cancellationToken);
        public async Task<AppUser?> FindLandlordByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            var user = await inner.FindLandlordByIdAsync(userId, cancellationToken);
            await gate.WaitAsync();
            return user;
        }
        public Task<bool> TryReviewPendingAsync(AppUser reviewedUser, CancellationToken cancellationToken = default) => inner.TryReviewPendingAsync(reviewedUser, cancellationToken);
    }
}
