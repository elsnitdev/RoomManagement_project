using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace RoomManagement.Data;

public sealed class RoomManagementDbContextFactory : IDesignTimeDbContextFactory<RoomManagementDbContext>
{
    public RoomManagementDbContext CreateDbContext(string[] args)
    {
        // Environment variables can override the local file for CI and deployment.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RoomManagement");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var directory = FindApiDirectory();
            foreach (var fileName in new[] { "appsettings.Local.json", "appsettings.Development.json", "appsettings.json" })
            {
                var path = Path.Combine(directory, fileName);
                if (!File.Exists(path)) continue;
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("ConnectionStrings", out var connections)
                    && connections.TryGetProperty("RoomManagement", out var database))
                {
                    connectionString = database.GetString();
                    if (!string.IsNullOrWhiteSpace(connectionString)) break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:RoomManagement in appsettings.Local.json or an environment variable.");

        return new RoomManagementDbContext(
            new DbContextOptionsBuilder<RoomManagementDbContext>()
                .UseSqlServer(connectionString)
                .Options);
    }

    private static string FindApiDirectory()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            foreach (var relativePath in new[] { "", "src/RoomManagement.API", "backend/src/RoomManagement.API" })
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(Path.Combine(candidate, "appsettings.json"))) return candidate;
            }
        }
        throw new InvalidOperationException("Cannot locate RoomManagement.API/appsettings.json for design-time database configuration.");
    }
}
