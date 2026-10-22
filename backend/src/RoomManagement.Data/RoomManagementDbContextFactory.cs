using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoomManagement.Data;

public sealed class RoomManagementDbContextFactory : IDesignTimeDbContextFactory<RoomManagementDbContext>
{
    public RoomManagementDbContext CreateDbContext(string[] args)
    {
        // Use the same configuration key as the API, including for database update.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RoomManagement");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__RoomManagement before running dotnet ef commands.");
        }

        return new RoomManagementDbContext(
            new DbContextOptionsBuilder<RoomManagementDbContext>()
                .UseSqlServer(connectionString)
                .Options);
    }
}
