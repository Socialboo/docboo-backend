using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Documents;

/// <summary>
/// Canonical JSON settings for the document model. Used by the API, persistence (JSONB) and tests,
/// so the same document always serializes the same way.
/// </summary>
public static class DocumentJson
{
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>Applies the document-model rules to an existing options instance (e.g. ASP.NET Core's).</summary>
    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.NumberHandling = JsonNumberHandling.Strict;
        // Editors do not guarantee that "type" is the first property.
        options.AllowOutOfOrderMetadataProperties = true;
        // Unknown fields are rejected instead of silently dropped (CslItem keeps its own extension data).
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.MaxDepth = 64;
        return options;
    }

    public static string Serialize(DocumentModel document) => JsonSerializer.Serialize(document, Options);

    public static DocumentModel Deserialize(string json) =>
        JsonSerializer.Deserialize<DocumentModel>(json, Options)
        ?? throw new JsonException("Document JSON is null.");
}
