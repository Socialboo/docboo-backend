using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Backend.OpenApiTransformers;

/// <summary>
/// System.Text.Json always writes the discriminator (e.g. <c>"type": "paragraph"</c>) of polymorphic
/// types, but the generated schema leaves it optional. Marking it required lets the TypeScript client
/// narrow unions such as <c>Block</c> and <c>Inline</c> on <c>type</c>.
/// </summary>
internal sealed class RequiredDiscriminatorSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var baseType = context.JsonTypeInfo.Type.BaseType;
        var polymorphic = baseType?.GetCustomAttribute<JsonPolymorphicAttribute>();
        if (polymorphic is null) return Task.CompletedTask;

        var discriminator = polymorphic.TypeDiscriminatorPropertyName ?? "$type";
        if (schema.Properties?.ContainsKey(discriminator) == true)
        {
            schema.Required ??= new HashSet<string>();
            schema.Required.Add(discriminator);
        }
        return Task.CompletedTask;
    }
}
