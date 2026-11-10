using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Documents;

/// <summary>
/// Bibliographic item in CSL-JSON format (https://citeproc-js.readthedocs.io/en/latest/csl-json/markup.html).
/// The common fields are typed; any other CSL field is preserved in <see cref="Extra"/>.
/// </summary>
public sealed record CslItem
{
    public required string Id { get; init; }

    /// <summary>CSL item type: book, article-journal, chapter, thesis, webpage, ...</summary>
    public required string Type { get; init; }

    public string? Title { get; init; }
    public IReadOnlyList<CslName>? Author { get; init; }
    public IReadOnlyList<CslName>? Editor { get; init; }
    public CslDate? Issued { get; init; }
    public CslDate? Accessed { get; init; }

    [JsonPropertyName("container-title")] public string? ContainerTitle { get; init; }
    public string? Publisher { get; init; }
    [JsonPropertyName("publisher-place")] public string? PublisherPlace { get; init; }
    public string? Edition { get; init; }
    public string? Volume { get; init; }
    public string? Issue { get; init; }
    public string? Page { get; init; }
    public string? Genre { get; init; }
    public string? Number { get; init; }

    [JsonPropertyName("URL")] public string? Url { get; init; }
    [JsonPropertyName("DOI")] public string? Doi { get; init; }
    [JsonPropertyName("ISBN")] public string? Isbn { get; init; }
    [JsonPropertyName("ISSN")] public string? Issn { get; init; }

    /// <summary>Other CSL fields, kept as-is.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed record CslName
{
    public string? Family { get; init; }
    public string? Given { get; init; }

    /// <summary>Institutional author or a name that must not be split.</summary>
    public string? Literal { get; init; }
}

public sealed record CslDate
{
    /// <summary>[[year, month?, day?]] (a second entry denotes a range).</summary>
    [JsonPropertyName("date-parts")] public IReadOnlyList<IReadOnlyList<int>>? DateParts { get; init; }

    public string? Literal { get; init; }
}
