using Application.Exports;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Backend.Health;

/// <summary>Degraded (not unhealthy) without LibreOffice: everything but PDF export keeps working.</summary>
internal sealed class LibreOfficeHealthCheck(IPdfConverter converter) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(converter.IsAvailable
            ? HealthCheckResult.Healthy("LibreOffice disponível.")
            : HealthCheckResult.Degraded("LibreOffice (soffice) não encontrado: exportação em PDF indisponível."));
}
