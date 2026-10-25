using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomManagement.Business.Interfaces;

namespace RoomManagement.Tests;

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "OnlyForAutomatedTests-A-64-byte-signing-key-with-no-production-use!";
    public TestUserStore Store { get; } = new();
    public Dictionary<string, string?> Overrides { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Program reads this setting before ConfigureAppConfiguration runs for minimal hosting.
        builder.UseSetting("ConnectionStrings:RoomManagement", Overrides.GetValueOrDefault("ConnectionStrings:RoomManagement")
            ?? "Server=localhost;Database=UnusedAuthTestDatabase;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=1");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RoomManagement"] = "Server=localhost;Database=UnusedAuthTestDatabase;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=1",
            ["InitialAdmin:Password"] = "",
            ["Jwt:Issuer"] = "RoomManagement.Tests",
            ["Jwt:Audience"] = "RoomManagement.Tests.Client",
            ["Jwt:SigningKey"] = SigningKey,
            ["Jwt:AccessTokenMinutes"] = "15"
        }).AddInMemoryCollection(Overrides));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserStore>();
            services.RemoveAll<ILandlordApprovalStore>();
            services.AddSingleton<IUserStore>(Store);
            services.AddSingleton<ILandlordApprovalStore>(Store);
        });
    }
}
