using Domain.Profiles;
using Exporter.Docx;
using Exporter.Pdf;
using Exporter.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Exporter;

public static class ExporterServiceCollectionExtensions
{
    public static IServiceCollection AddExporter(this IServiceCollection services)
    {
        services.AddSingleton<EmbeddedNormProfileCatalog>();
        services.AddSingleton<INormProfileCatalog>(sp => sp.GetRequiredService<EmbeddedNormProfileCatalog>());
        services.AddSingleton<INormProfileStore>(sp => sp.GetRequiredService<EmbeddedNormProfileCatalog>());
        services.AddSingleton<DocxDocumentExporter>();
        services.AddOptions<LibreOfficeOptions>();
        services.AddSingleton<LibreOfficeConverter>();
        return services;
    }
}
