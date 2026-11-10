using Domain.Profiles;
using Exporter.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Exporter;

public static class ExporterServiceCollectionExtensions
{
    public static IServiceCollection AddExporter(this IServiceCollection services)
    {
        services.AddSingleton<INormProfileCatalog, EmbeddedNormProfileCatalog>();
        return services;
    }
}
