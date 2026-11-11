using Application.Documents;
using Application.Exports;
using Application.Works;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<DocumentLimits>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DocumentValidator>();
        services.AddScoped<WorkService>();
        services.AddOptions<ExportOptions>();
        services.AddScoped<ExportService>();
        services.AddScoped<ExportJobProcessor>();
        return services;
    }
}
