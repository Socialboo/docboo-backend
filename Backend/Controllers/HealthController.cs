using System.Reflection;
using DTOs.Health;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Backend.Controllers;

/// <summary>Health information for the frontend and operators.</summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    /// <summary>Runs all registered health checks and returns their status.</summary>
    [HttpGet(Name = "GetHealth")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken ct)
    {
        var report = await healthChecks.CheckHealthAsync(ct);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var body = new HealthResponse(
            report.Status.ToString(),
            version,
            report.Entries
                .Select(e => new HealthCheckEntry(e.Key, e.Value.Status.ToString(), e.Value.Duration.TotalMilliseconds))
                .ToList());

        return report.Status == HealthStatus.Unhealthy
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, body)
            : Ok(body);
    }
}
