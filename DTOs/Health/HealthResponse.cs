namespace DTOs.Health;

/// <summary>Liveness/readiness snapshot returned by <c>GET /api/health</c>.</summary>
/// <param name="Status">Overall status: <c>Healthy</c>, <c>Degraded</c> or <c>Unhealthy</c>.</param>
/// <param name="Version">API assembly version.</param>
/// <param name="Checks">Individual dependency checks (database, etc.).</param>
public sealed record HealthResponse(string Status, string Version, IReadOnlyList<HealthCheckEntry> Checks);

/// <param name="Name">Check name.</param>
/// <param name="Status">Check status.</param>
/// <param name="DurationMs">How long the check took, in milliseconds.</param>
public sealed record HealthCheckEntry(string Name, string Status, double DurationMs);
