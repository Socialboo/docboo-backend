using System.Text.Json.Serialization;
using Domain.Documents;
using Domain.Profiles;

namespace Exporter.Profiles;

/// <summary>
/// Formatting rules of a norm (ABNT, institution variants, APA, ...). Loaded from
/// Exporter/Profiles/*.json; the generator reads every measure from here, never from code.
/// Lengths are in centimeters and font sizes in points, as written in the norms.
/// </summary>
public sealed record NormProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int MaxSectionDepth { get; init; }

    /// <summary>BCP 47 language of the generated document (spell-checking, hyphenation).</summary>
    public required string Language { get; init; }

    public required PageRules Page { get; init; }
    public required FontRules Fonts { get; init; }
    public required ParagraphRules Body { get; init; }

    /// <summary>Footnotes, captions, sources and the nature of the work.</summary>
    public required TextRules SmallText { get; init; }

    public required LongQuoteRules LongQuote { get; init; }

    /// <summary>Titles above figures/tables ("Figura 1 – ...") and the source line below them.</summary>
    public required CaptionRules Captions { get; init; }

    /// <summary>Reference list entries.</summary>
    public required ReferenceListRules References { get; init; }

    public required HeadingRules Headings { get; init; }
    public required PageNumberingRules PageNumbering { get; init; }
    public required PreTextualRules PreTextual { get; init; }
    public required PostTextualRules PostTextual { get; init; }

    public NormProfileInfo ToInfo() => new(Id, Name, Description, MaxSectionDepth);

    /// <summary>Font family for a work, honoring the work's override when the profile offers it.</summary>
    public string ResolveFontFamily(ProfileOverrides overrides)
    {
        var key = overrides.FontFamily switch
        {
            FontFamilyOption.TimesNewRoman => "timesNewRoman",
            FontFamilyOption.Arial => "arial",
            _ => Fonts.Default,
        };
        return Fonts.Families.TryGetValue(key, out var family) ? family : Fonts.Families[Fonts.Default];
    }

    public HeadingLevelRules Heading(int level) =>
        Headings.Levels.FirstOrDefault(l => l.Level == level)
        ?? throw new ArgumentOutOfRangeException(nameof(level), level, "Heading level not defined by the profile.");
}

public sealed record PageRules
{
    public required double WidthCm { get; init; }
    public required double HeightCm { get; init; }
    public required double MarginTopCm { get; init; }
    public required double MarginLeftCm { get; init; }
    public required double MarginBottomCm { get; init; }
    public required double MarginRightCm { get; init; }

    /// <summary>Distance from the top edge of the page to the header (where the page number sits).</summary>
    public required double HeaderDistanceCm { get; init; }

    public required double FooterDistanceCm { get; init; }
}

public sealed record FontRules
{
    /// <summary>Key in <see cref="Families"/> used when the work does not override it.</summary>
    public required string Default { get; init; }

    /// <summary>Allowed families: option key → font name.</summary>
    public required IReadOnlyDictionary<string, string> Families { get; init; }
}

public sealed record TextRules
{
    public required double FontSizePt { get; init; }

    /// <summary>Line spacing multiple (1.0 = single, 1.5 = one and a half).</summary>
    public required double LineSpacing { get; init; }
}

public sealed record ParagraphRules
{
    public required double FontSizePt { get; init; }
    public required double LineSpacing { get; init; }
    public required double FirstLineIndentCm { get; init; }
    public required TextAlignment Alignment { get; init; }
}

public sealed record LongQuoteRules
{
    public required double LeftIndentCm { get; init; }
    public required double FontSizePt { get; init; }
    public required double LineSpacing { get; init; }
}

public sealed record CaptionRules
{
    public required TextAlignment Alignment { get; init; }
    public required TextAlignment SourceAlignment { get; init; }

    /// <summary>Label and SEQ field name of figures (also used by the list of figures).</summary>
    public required string FigureLabel { get; init; }

    public required string TableLabel { get; init; }

    /// <summary>Between the number and the title ("Figura 1 – Título").</summary>
    public required string Separator { get; init; }

    /// <summary>Prefix of the source line ("Fonte: ...").</summary>
    public required string SourceLabel { get; init; }
}

public sealed record ReferenceListRules
{
    public required double LineSpacing { get; init; }

    /// <summary>Space between entries (a blank single-spaced line).</summary>
    public required double SpaceAfterPt { get; init; }
}

public sealed record HeadingRules
{
    /// <summary>Progressive numbering (1, 1.1, 1.1.1, ...).</summary>
    public required bool Numbered { get; init; }

    public required double SpaceBeforePt { get; init; }
    public required double SpaceAfterPt { get; init; }
    public required IReadOnlyList<HeadingLevelRules> Levels { get; init; }
}

public sealed record HeadingLevelRules
{
    public required int Level { get; init; }
    public required bool Bold { get; init; }
    public required bool Italic { get; init; }
    public required bool AllCaps { get; init; }

    /// <summary>Starts the heading on a new page (primary sections).</summary>
    public required bool PageBreakBefore { get; init; }
}

public sealed record PageNumberingRules
{
    /// <summary>First page included in the count.</summary>
    public required PageCountStart CountFrom { get; init; }

    /// <summary>First part of the document where the number is printed.</summary>
    public required PageNumberVisibleFrom VisibleFrom { get; init; }

    public required PageNumberPosition Position { get; init; }
    public required double FontSizePt { get; init; }
}

public sealed record PreTextualRules
{
    /// <summary>Indent of the "nature of the work" block on the title page (from the left margin).</summary>
    public required double NatureIndentCm { get; init; }

    public required string AbstractTitlePt { get; init; }
    public required string AbstractTitleForeign { get; init; }
    public required string KeywordsLabelPt { get; init; }
    public required string KeywordsLabelForeign { get; init; }
    public required string AdvisorLabel { get; init; }
    public required string CoAdvisorLabel { get; init; }
    public required string TocTitle { get; init; }
    public required string ListOfFiguresTitle { get; init; }
    public required string ListOfTablesTitle { get; init; }
}

public sealed record PostTextualRules
{
    public required string ReferencesTitle { get; init; }

    /// <summary>"APÊNDICE" in "APÊNDICE A – Título".</summary>
    public required string AppendixLabel { get; init; }

    public required string AnnexLabel { get; init; }
    public required string LabelSeparator { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<TextAlignment>))]
public enum TextAlignment
{
    [JsonStringEnumMemberName("left")] Left,
    [JsonStringEnumMemberName("center")] Center,
    [JsonStringEnumMemberName("right")] Right,
    [JsonStringEnumMemberName("justify")] Justify,
}

[JsonConverter(typeof(JsonStringEnumConverter<PageCountStart>))]
public enum PageCountStart
{
    /// <summary>The cover is not counted; the title page is page 1.</summary>
    [JsonStringEnumMemberName("titlePage")] TitlePage,

    /// <summary>The cover is page 1.</summary>
    [JsonStringEnumMemberName("cover")] Cover,
}

[JsonConverter(typeof(JsonStringEnumConverter<PageNumberVisibleFrom>))]
public enum PageNumberVisibleFrom
{
    /// <summary>Printed from the first page of the textual part (introduction) onwards.</summary>
    [JsonStringEnumMemberName("textual")] Textual,

    /// <summary>Printed on every counted page.</summary>
    [JsonStringEnumMemberName("countStart")] CountStart,
}

[JsonConverter(typeof(JsonStringEnumConverter<PageNumberPosition>))]
public enum PageNumberPosition
{
    [JsonStringEnumMemberName("topRight")] TopRight,
    [JsonStringEnumMemberName("bottomCenter")] BottomCenter,
}
