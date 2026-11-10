using System.Reflection;
using Application;
using Application.Documents;
using Domain.Documents;
using Exporter;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetDefaultConnectionString();

// Same JSON rules for controllers and for the OpenAPI generator (which reads the minimal-API options),
// so the schema matches what is actually (de)serialized.
builder.Services.AddControllers()
    .AddJsonOptions(o => DocumentJson.Configure(o.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(o => DocumentJson.Configure(o.SerializerOptions));
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi("v1", options =>
{
    options.AddSchemaTransformer<Backend.OpenApiTransformers.RequiredDiscriminatorSchemaTransformer>();
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Docboo API";
        document.Info.Description = "Formatador de trabalhos acadêmicos (ABNT).";
        return Task.CompletedTask;
    });
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddExporter();
builder.Services.Configure<DocumentLimits>(builder.Configuration.GetSection(DocumentLimits.SectionName));

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Kestrel signals client errors (e.g. 413 body too large) with BadHttpRequestException; keep its status.
    StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

app.MapOpenApi("/openapi/{documentName}.json");
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Api:EnableDocsUi"))
{
    app.UseSwaggerUI(o =>
    {
        o.RoutePrefix = "swagger";
        o.SwaggerEndpoint("/openapi/v1.json", "Docboo API v1");
    });
    app.MapScalarApiReference("/scalar", o => o.WithTitle("Docboo API").WithOpenApiRoutePattern("/openapi/{documentName}.json"));
}

// Infrastructure health endpoints (used by Docker healthchecks; not part of the public API contract).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health", new HealthCheckOptions());

app.MapControllers();

// The build-time OpenAPI generator (GetDocument.Insider) runs this entry point without a database.
if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
{
    await app.MigrateDatabaseAsync();
}

await app.RunAsync();

public partial class Program;
