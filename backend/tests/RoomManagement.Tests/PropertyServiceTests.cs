using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

public sealed class PropertyServiceTests
{
    [SqlServerFact]
    public async Task Migration_clones_legacy_catalog_and_keeps_existing_assignments()
    {
        await using var database = await SqlServerDatabase.CreateAsync(migrate: false);
        await using var db = database.CreateContext();
        await db.GetService<IMigrator>().MigrateAsync("20261109163319_ActivePropertyRulesVersion");
        var propertyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var oldServiceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        // Seed through the old schema. The current EF model already contains the new columns.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.Users (Id, UserName, NormalizedUserName, PasswordHash, Role)
            VALUES (N'legacy-service-owner', N'legacy-service-owner', N'LEGACY-SERVICE-OWNER', N'test', N'Landlord')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.Properties (Id, OwnerUserId, Name, Address)
            VALUES ({propertyId}, N'legacy-service-owner', N'Khu cũ', N'A')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.Rooms (Id, PropertyId, RoomNumber, BasePrice, MaxOccupants)
            VALUES ({roomId}, {propertyId}, N'A101', 1000000, 2)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.Services (Id, Name, Unit, DefaultPrice)
            VALUES ({oldServiceId}, N'Internet', N'phòng', 100000)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.RoomServices (Id, RoomId, ServiceId, StartDate)
            VALUES ({Guid.NewGuid()}, {roomId}, {oldServiceId}, '2026-01-01')
            """);

        await db.GetService<IMigrator>().MigrateAsync();
        db.ChangeTracker.Clear();
        var assignment = await db.RoomServices.SingleAsync();
        var scoped = await db.Services.SingleAsync(x => x.Id == assignment.ServiceId);
        Assert.Equal(propertyId, scoped.PropertyId);
        Assert.Equal(1, assignment.Quantity);
        Assert.Equal(100_000m, assignment.CustomPrice);
        Assert.True(await db.Services.AnyAsync(x => x.Id == oldServiceId && x.PropertyId == null));
    }

    [SqlServerFact]
    public async Task Catalog_is_property_scoped_and_new_assignments_start_next_month()
    {
        await using var database = await SqlServerDatabase.CreateAsync();
        var firstProperty = Guid.NewGuid();
        var secondProperty = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = database.CreateContext();
        db.Users.Add(new AppUser { Id = "service-owner", UserName = "service-owner",
            NormalizedUserName = "SERVICE-OWNER", PasswordHash = "test", Role = UserRoles.Landlord,
            IsActive = true, CreatedAt = now });
        db.Properties.AddRange(
            new Property { Id = firstProperty, OwnerUserId = "service-owner", Name = "Khu A",
                Address = "A", IsActive = true, CreatedAt = now },
            new Property { Id = secondProperty, OwnerUserId = "service-owner", Name = "Khu B",
                Address = "B", IsActive = true, CreatedAt = now });
        db.Rooms.Add(new Room { Id = roomId, PropertyId = firstProperty, RoomNumber = "A101",
            BasePrice = 1_000_000m, MaxOccupants = 2, CreatedAt = now });
        await db.SaveChangesAsync();

        var service = new BillingInputService(new CurrentLandlord(), new LandlordStore(db), new BillingInputStore(db));
        var first = await service.CreateServiceAsync(firstProperty,
            new CreateServiceInput("Gửi xe", null, "xe", 80_000m), CancellationToken.None);
        var second = await service.CreateServiceAsync(secondProperty,
            new CreateServiceInput("Gửi xe", null, "xe", 90_000m), CancellationToken.None);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Single(await service.ListServicesAsync(firstProperty, CancellationToken.None));

        var nextMonth = new DateOnly(VietnamDate.Today.Year, VietnamDate.Today.Month, 1).AddMonths(1);
        var tooSoon = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AssignRoomServiceAsync(firstProperty, roomId,
                new AssignRoomServiceInput(first.Id, null, nextMonth.AddMonths(-1), null, 3), CancellationToken.None));
        Assert.Equal("ROOM_SERVICE_INVALID", tooSoon.Code);
        var otherProperty = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AssignRoomServiceAsync(firstProperty, roomId,
                new AssignRoomServiceInput(second.Id, null, nextMonth, null, 3), CancellationToken.None));
        Assert.Equal("SERVICE_NOT_FOUND", otherProperty.Code);

        var assigned = await service.AssignRoomServiceAsync(firstProperty, roomId,
            new AssignRoomServiceInput(first.Id, null, nextMonth, null, 3), CancellationToken.None);
        Assert.Equal(3, assigned.Assignment.Quantity);
        Assert.Equal(80_000m, assigned.Assignment.CustomPrice);
        Assert.Equal(1, await db.RoomServices.CountAsync());
    }

    private sealed class CurrentLandlord : ICurrentLandlord
    {
        public string GetUserId() => "service-owner";
    }
}
