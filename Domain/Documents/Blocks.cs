using System.Text.Json.Serialization;

namespace Domain.Documents;

/// <summary>Block-level content inside a section, appendix or annex. Discriminated by <c>type</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ParagraphBlock), "paragraph")]
[JsonDerivedType(typeof(LongQuoteBlock), "longQuote")]
[JsonDerivedType(typeof(ListBlock), "list")]
[JsonDerivedType(typeof(FigureBlock), "figure")]
[JsonDerivedType(typeof(TableBlock), "table")]
public abstract record Block
{
    public required string Id { get; init; }
}

public sealed record ParagraphBlock : Block
{
    public required IReadOnlyList<Inline> Content { get; init; }
}

/// <summary>Direct quotation longer than three lines. Formatting (indent, size, spacing) comes from the profile.</summary>
public sealed record LongQuoteBlock : Block
{
    /// <summary>Quoted text, usually ending with a citation inline.</summary>
    public required IReadOnlyList<Inline> Content { get; init; }
}

public sealed record ListBlock : Block
{
    public required bool Ordered { get; init; }
    public required IReadOnlyList<ListItem> Items { get; init; }
}

public sealed record ListItem
{
    public required IReadOnlyList<Inline> Content { get; init; }
}

public sealed record FigureBlock : Block
{
    /// <summary>Uploaded image id.</summary>
    public required string AssetId { get; init; }

    public required IReadOnlyList<Inline> Caption { get; init; }

    /// <summary>Source line ("Fonte: ..."), without the "Fonte:" prefix.</summary>
    public required IReadOnlyList<Inline> Source { get; init; }

    /// <summary>Width relative to the text area, 10..100.</summary>
    public required int WidthPercent { get; init; }
}

public sealed record TableBlock : Block
{
    public required IReadOnlyList<Inline> Title { get; init; }

    /// <summary>Source line ("Fonte: ..."), without the "Fonte:" prefix.</summary>
    public required IReadOnlyList<Inline> Source { get; init; }

    public required IReadOnlyList<TableRow> Rows { get; init; }
}

public sealed record TableRow
{
    /// <summary>Header rows repeat on page breaks.</summary>
    public required bool Header { get; init; }

    public required IReadOnlyList<TableCell> Cells { get; init; }
}

public sealed record TableCell
{
    public required IReadOnlyList<Inline> Content { get; init; }
    public required int ColSpan { get; init; }
    public required int RowSpan { get; init; }
}
