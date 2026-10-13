using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TaskTrack.Repo.Data;
using TaskTrack.Repo.Models;
using TaskTrack.Service.Services;
using Xunit;

namespace TaskTrack.Tests;

public sealed class TestApplication : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string schema = "test_" + Guid.NewGuid().ToString("N");
    private readonly string baseConnection = Environment.GetEnvironmentVariable("TEST_DATABASE_URL") ?? "Host=localhost;Port=5434;Database=tasktrack_tests;Username=taskadmin;Password=local-ass2-only";
    public string ConnectionString => new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema }.ConnectionString;
    public string JwtSecret { get; } = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    public int AdminId { get; private set; }
    public const string AdminEmail = "admin@integration.test";
    public const string Password = "IntegrationPass@123";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["DATABASE_URL"] = ConnectionString, ["JWT_SECRET"] = JwtSecret,
            ["Jwt:Issuer"] = "TaskTrack.Ass2", ["Jwt:Audience"] = "TaskTrack.Frontend"
        }));
    }
    public async Task InitializeAsync()
    {
        if (!(new NpgsqlConnectionStringBuilder(baseConnection).Database ?? "").Contains("test", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("TEST_DATABASE_URL must name a dedicated test database.");
        await using var connection = new NpgsqlConnection(baseConnection);
        await connection.OpenAsync();
        await new NpgsqlCommand($"CREATE SCHEMA {schema}", connection).ExecuteNonQueryAsync();
        await using var scoped = new NpgsqlConnection(ConnectionString);
        await scoped.OpenAsync();
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database", "TaskManagementDB_Postgres.sql")), scoped).ExecuteNonQueryAsync();
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database", "Assignment2_upgrade.sql")), scoped).ExecuteNonQueryAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskManagementDbContext>();
        var admin = new SystemAccount { FullName = "Integration Admin", Email = AdminEmail, Role = 1, PasswordHash = PasswordHasher.Hash(Password) };
        db.Accounts.Add(admin); await db.SaveChangesAsync(); AdminId = admin.AccountId;
    }
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await using var connection = new NpgsqlConnection(baseConnection);
        await connection.OpenAsync();
        await new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", connection).ExecuteNonQueryAsync();
    }
}
