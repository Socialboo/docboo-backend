using System.Text.Json.Serialization;

namespace Domain.Documents;

/// <summary>Inline content inside a block. Discriminated by <c>type</c>; mirrors the editor (Tiptap) node types.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(TextInline), "text")]
[JsonDerivedType(typeof(HardBreakInline), "hardBreak")]
[JsonDerivedType(typeof(CitationInline), "citation")]
[JsonDerivedType(typeof(FootnoteInline), "footnote")]
public abstract record Inline;

public sealed record TextInline : Inline
{
    /// <summary>Plain text. Line breaks are represented by <see cref="HardBreakInline"/>, not by <c>\n</c>.</summary>
    public required string Text { get; init; }

    public required IReadOnlyList<TextMark> Marks { get; init; }
}

public sealed record HardBreakInline : Inline;

/// <summary>Citation of one or more references (author-date system).</summary>
public sealed record CitationInline : Inline
{
    public required IReadOnlyList<string> RefIds { get; init; }

    /// <summary>Page or other locator, e.g. "p. 12".</summary>
    public required string? Locator { get; init; }

    public required CitationMode Mode { get; init; }
}

public sealed record FootnoteInline : Inline
{
    public required string Id { get; init; }

    /// <summary>Footnote text. Footnotes cannot contain other footnotes.</summary>
    public required IReadOnlyList<Inline> Content { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<TextMark>))]
public enum TextMark
{
    [JsonStringEnumMemberName("bold")] Bold,
    [JsonStringEnumMemberName("italic")] Italic,
    [JsonStringEnumMemberName("underline")] Underline,
    [JsonStringEnumMemberName("superscript")] Superscript,
    [JsonStringEnumMemberName("subscript")] Subscript,
}

[JsonConverter(typeof(JsonStringEnumConverter<CitationMode>))]
public enum CitationMode
{
    /// <summary>(SILVA, 2020, p. 12)</summary>
    [JsonStringEnumMemberName("parenthetical")] Parenthetical,

    /// <summary>Silva (2020, p. 12)</summary>
    [JsonStringEnumMemberName("narrative")] Narrative,
}

[JsonConverter(typeof(JsonStringEnumConverter<FontFamilyOption>))]
public enum FontFamilyOption
{
    [JsonStringEnumMemberName("timesNewRoman")] TimesNewRoman,
    [JsonStringEnumMemberName("arial")] Arial,
}
