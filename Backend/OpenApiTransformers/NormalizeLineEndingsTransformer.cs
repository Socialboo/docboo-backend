using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Backend.OpenApiTransformers;

/// <summary>
/// XML doc comments spanning several lines are joined with the OS newline, so the generated
/// openapi.json would differ between Windows (CRLF) and Linux/CI (LF). Normalizing to LF keeps the
/// versioned document (and the frontend's generated types) identical on every machine.
/// </summary>
internal sealed class NormalizeLineEndingsTransformer : IOpenApiSchemaTransformer, IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        schema.Description = Normalize(schema.Description);
        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.Summary = Normalize(operation.Summary);
        operation.Description = Normalize(operation.Description);
        return Task.CompletedTask;
    }

    private static string? Normalize(string? text) => text?.Replace("\r\n", "\n");
}
