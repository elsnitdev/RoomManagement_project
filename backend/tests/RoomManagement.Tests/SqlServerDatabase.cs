using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Data;

namespace RoomManagement.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerDatabase.EnvironmentVariable)))
            Skip = "Set ROOMMANAGEMENT_TEST_SQLSERVER to a dedicated test SQL Server connection to run real database tests.";
    }
}

internal sealed class SqlServerDatabase : IAsyncDisposable
{
    public const string EnvironmentVariable = "ROOMMANAGEMENT_TEST_SQLSERVER";
    private readonly string _name = "RoomManagement_Tests_" + Guid.NewGuid().ToString("N");
    private readonly string _masterConnection;
    private bool _created;
    public string ConnectionString { get; }

    private SqlServerDatabase(string connectionString)
    {
        var connection = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        _masterConnection = connection.ConnectionString;
        connection.InitialCatalog = _name;
        ConnectionString = connection.ConnectionString;
    }

    public static async Task<SqlServerDatabase> CreateAsync(bool migrate = true)
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable)
            ?? throw new InvalidOperationException("A test SQL Server connection must be explicitly configured.");
        var database = new SqlServerDatabase(configured);
        try
        {
            await using var connection = new SqlConnection(database._masterConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{database._name}]";
            await command.ExecuteNonQueryAsync();
            database._created = true;
            if (migrate)
            {
                await using var context = database.CreateContext();
                await context.Database.MigrateAsync();
            }
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public RoomManagementDbContext CreateContext() => new(new DbContextOptionsBuilder<RoomManagementDbContext>()
        .UseSqlServer(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        if (!_created) return;
        // Only drop the random database this fixture successfully created; never a supplied catalog.
        if (!Regex.IsMatch(_name, "^RoomManagement_Tests_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Refusing to drop a database outside the test naming boundary.");
        await using var connection = new SqlConnection(_masterConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]";
        await command.ExecuteNonQueryAsync();
        _created = false;
    }
}
