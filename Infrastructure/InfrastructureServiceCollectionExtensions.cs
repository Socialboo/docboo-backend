using Application.Abstractions;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=docboo;Username=docboo;Password=docboo";

    public static string GetDefaultConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString("Default") ?? DefaultConnectionString;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(configuration.GetDefaultConnectionString()));
        services.AddScoped<IWorkRepository, WorkRepository>();

        services.AddOptions<LocalUserOptions>().Bind(configuration.GetSection(LocalUserOptions.SectionName));
        services.AddSingleton<ICurrentUser, LocalCurrentUser>();
        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations when <c>Database:MigrateOnStartup</c> is true.
    /// Fine for a single API instance; with several replicas, run migrations as a deploy step instead.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue("Database:MigrateOnStartup", true)) return;

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
