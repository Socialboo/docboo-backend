using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace IntegrationTests;

/// <summary>
/// Runs the real API (all layers, migrations on startup) against a throwaway PostgreSQL container.
/// Requires Docker.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:latest").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>Isolated folder for exported files, deleted after the run.</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "docboo-tests-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, recursive: true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Storage:Root", StorageRoot);
    }
}
