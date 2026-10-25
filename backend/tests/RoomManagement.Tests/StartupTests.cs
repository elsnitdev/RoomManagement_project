namespace RoomManagement.Tests;

public sealed class StartupTests
{
    [Fact]
    public void Missing_signing_key_prevents_startup()
    {
        using var factory = new ApiFactory();
        factory.Overrides["Jwt:SigningKey"] = "";
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Jwt:SigningKey", error.ToString());
    }

    [Fact]
    public void Configured_seed_failure_prevents_startup()
    {
        using var factory = new ApiFactory();
        factory.Overrides["InitialAdmin:Password"] = TestUserStore.Password;
        factory.Overrides["ConnectionStrings:RoomManagement"] =
            "Server=127.0.0.1,1;Database=UnusedSeedTestDatabase;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=1;ConnectRetryCount=0";
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("SqlException", error.ToString());
    }
}
