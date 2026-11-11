using Application.Abstractions;
using Application.Exports;
using Exporter.Pdf;
using Infrastructure.Exports;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Storage;
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

        // Exports: Open XML + LibreOffice behind application ports, disk storage, in-process queue.
        services.AddScoped<IExportJobRepository, ExportJobRepository>();
        services.AddScoped<Application.Assets.IAssetRepository, AssetRepository>();
        services.AddOptions<Application.Assets.AssetOptions>().Bind(configuration.GetSection(Application.Assets.AssetOptions.SectionName));
        services.AddOptions<ExportOptions>().Bind(configuration.GetSection(ExportOptions.SectionName));
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        services.AddOptions<LibreOfficeOptions>().Bind(configuration.GetSection(LibreOfficeOptions.SectionName));
        services.AddSingleton<IFileStorage, DiskFileStorage>();
        services.AddSingleton<IDocumentGenerator, DocxGeneratorAdapter>();
        services.AddSingleton<IPdfConverter, LibreOfficePdfConverterAdapter>();
        services.AddSingleton<ChannelExportQueue>();
        services.AddSingleton<IExportQueue>(sp => sp.GetRequiredService<ChannelExportQueue>());
        return services;
    }

    /// <summary>Background export worker and expired-file cleanup (Channel-based; swap for Hangfire later).</summary>
    public static IServiceCollection AddExportWorkers(this IServiceCollection services)
    {
        services.AddHostedService<ExportWorker>();
        services.AddHostedService<ExportCleanupService>();
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
