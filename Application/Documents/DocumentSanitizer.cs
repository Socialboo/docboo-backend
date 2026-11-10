using System.Text;
using Domain.Documents;

namespace Application.Documents;

/// <summary>
/// Normalizes user-provided text in a document before validation and storage:
/// removes control characters and invalid UTF-16, applies Unicode NFC, trims single-line fields,
/// drops empty text runs and duplicate marks. The model has no HTML, so there is no markup to strip;
/// structure is only accepted through the typed model.
/// </summary>
public static class DocumentSanitizer
{
    public static DocumentModel Sanitize(DocumentModel doc) => doc with
    {
        ProfileId = Line(doc.ProfileId),
        Metadata = Metadata(doc.Metadata),
        PreTextual = doc.PreTextual with
        {
            AbstractPt = Abstract(doc.PreTextual.AbstractPt),
            AbstractEn = Abstract(doc.PreTextual.AbstractEn),
        },
        Textual = new Textual { Sections = doc.Textual.Sections.Select(Section).ToList() },
        PostTextual = new PostTextual
        {
            Appendices = doc.PostTextual.Appendices.Select(Attachment).ToList(),
            Annexes = doc.PostTextual.Annexes.Select(Attachment).ToList(),
        },
        References = doc.References.Select(Reference).ToList(),
    };

    private static DocumentMetadata Metadata(DocumentMetadata m) => m with
    {
        Institution = Line(m.Institution),
        Faculty = OptionalLine(m.Faculty),
        Course = Line(m.Course),
        Authors = LineList(m.Authors),
        Title = Line(m.Title),
        Subtitle = OptionalLine(m.Subtitle),
        Advisor = Line(m.Advisor),
        CoAdvisor = OptionalLine(m.CoAdvisor),
        City = Line(m.City),
        Nature = Line(m.Nature),
    };

    private static AbstractElement Abstract(AbstractElement a) => a with
    {
        Text = MultiLine(a.Text),
        Keywords = LineList(a.Keywords),
    };

    private static Section Section(Section s) => s with
    {
        Id = Line(s.Id),
        Title = Line(s.Title),
        Blocks = s.Blocks.Select(Block).ToList(),
        Children = s.Children.Select(Section).ToList(),
    };

    private static Attachment Attachment(Attachment a) => a with
    {
        Id = Line(a.Id),
        Title = Line(a.Title),
        Blocks = a.Blocks.Select(Block).ToList(),
    };

    private static Block Block(Block block) => block switch
    {
        ParagraphBlock p => p with { Id = Line(p.Id), Content = Inlines(p.Content) },
        LongQuoteBlock q => q with { Id = Line(q.Id), Content = Inlines(q.Content) },
        ListBlock l => l with
        {
            Id = Line(l.Id),
            Items = l.Items.Select(i => i with { Content = Inlines(i.Content) }).ToList(),
        },
        FigureBlock f => f with
        {
            Id = Line(f.Id),
            AssetId = Line(f.AssetId),
            Caption = Inlines(f.Caption),
            Source = Inlines(f.Source),
        },
        TableBlock t => t with
        {
            Id = Line(t.Id),
            Title = Inlines(t.Title),
            Source = Inlines(t.Source),
            Rows = t.Rows.Select(r => r with
            {
                Cells = r.Cells.Select(c => c with { Content = Inlines(c.Content) }).ToList(),
            }).ToList(),
        },
        _ => throw new NotSupportedException($"Unknown block type {block.GetType().Name}."),
    };

    private static IReadOnlyList<Inline> Inlines(IReadOnlyList<Inline> inlines)
    {
        var result = new List<Inline>(inlines.Count);
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline t:
                    var text = Clean(t.Text, allowNewlines: false);
                    if (text.Length == 0) continue; // ProseMirror never has empty text nodes.
                    result.Add(t with { Text = text, Marks = t.Marks.Distinct().Order().ToList() });
                    break;
                case CitationInline c:
                    result.Add(c with
                    {
                        RefIds = c.RefIds.Select(Line).ToList(),
                        Locator = OptionalLine(c.Locator),
                    });
                    break;
                case FootnoteInline f:
                    result.Add(f with { Id = Line(f.Id), Content = Inlines(f.Content) });
                    break;
                default:
                    result.Add(inline);
                    break;
            }
        }
        return result;
    }

    private static CslItem Reference(CslItem r) => r with
    {
        Id = Line(r.Id),
        Type = Line(r.Type),
        Title = OptionalLine(r.Title),
        Author = r.Author?.Select(Name).ToList(),
        Editor = r.Editor?.Select(Name).ToList(),
        ContainerTitle = OptionalLine(r.ContainerTitle),
        Publisher = OptionalLine(r.Publisher),
        PublisherPlace = OptionalLine(r.PublisherPlace),
        Edition = OptionalLine(r.Edition),
        Volume = OptionalLine(r.Volume),
        Issue = OptionalLine(r.Issue),
        Page = OptionalLine(r.Page),
        Genre = OptionalLine(r.Genre),
        Number = OptionalLine(r.Number),
        Url = OptionalLine(r.Url),
        Doi = OptionalLine(r.Doi),
        Isbn = OptionalLine(r.Isbn),
        Issn = OptionalLine(r.Issn),
    };

    private static CslName Name(CslName n) => n with
    {
        Family = OptionalLine(n.Family),
        Given = OptionalLine(n.Given),
        Literal = OptionalLine(n.Literal),
    };

    private static IReadOnlyList<string> LineList(IReadOnlyList<string> values) =>
        values.Select(Line).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList();

    private static string Line(string value) => Clean(value, allowNewlines: false).Trim();

    private static string? OptionalLine(string? value)
    {
        if (value is null) return null;
        var line = Line(value);
        return line.Length == 0 ? null : line;
    }

    private static string MultiLine(string value) => Clean(value, allowNewlines: true).Trim();

    /// <summary>Removes control characters (tabs become spaces) and lone surrogates, then normalizes to NFC.</summary>
    public static string Clean(string value, bool allowNewlines)
    {
        if (value.Length == 0) return value;

        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    sb.Append(c).Append(value[++i]);
                }
                continue; // lone high surrogate
            }
            if (char.IsLowSurrogate(c)) continue; // lone low surrogate

            if (c == '\r')
            {
                if (allowNewlines && !(i + 1 < value.Length && value[i + 1] == '\n')) sb.Append('\n');
                else if (!allowNewlines) sb.Append(' ');
                continue;
            }
            if (c == '\n')
            {
                sb.Append(allowNewlines ? '\n' : ' ');
                continue;
            }
            if (c == '\t')
            {
                sb.Append(' ');
                continue;
            }
            if (char.IsControl(c) || c is (char)0x2028 or (char)0x2029 or (char)0xFEFF) continue;

            sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
