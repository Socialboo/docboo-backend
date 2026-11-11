using System.Globalization;
using System.Text.RegularExpressions;
using Domain.Documents;

namespace Exporter.References;

/// <summary>
/// Formats a CSL-JSON item as a reference entry in the style of the Brazilian norm for references
/// (authors in uppercase family names, highlighted title in bold, place: publisher, year, access data).
/// Missing data uses the conventional placeholders [S. l.], [s. n.] and [s. d.].
/// </summary>
public sealed partial class ReferenceFormatter(ReferenceCatalog catalog)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Months = ["jan.", "fev.", "mar.", "abr.", "maio", "jun.", "jul.", "ago.", "set.", "out.", "nov.", "dez."];

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex Digits();

    public IReadOnlyList<Segment> Format(CslItem item)
    {
        var b = new Builder();
        var authors = Names(item.Author);
        var editors = Names(item.Editor);

        if (authors.Length > 0) b.Sentence(authors);
        else if (editors.Length > 0) b.Sentence($"{editors} (ed.)");

        switch (item.Type)
        {
            case "article-journal" or "article-magazine" or "article-newspaper" or "article":
                // Periodical: the journal title is the highlighted element, followed by a comma.
                b.Sentence(item.Title);
                b.Commas(new Segment(item.ContainerTitle?.Trim().TrimEnd('.') ?? "", Bold: true),
                    item.PublisherPlace, Prefixed("v.", item.Volume), Prefixed("n.", item.Issue), Prefixed("p.", item.Page), catalog.YearText(item));
                b.End();
                break;

            case "chapter" or "paper-conference" or "entry-encyclopedia" or "entry-dictionary":
                b.Sentence(item.Title);
                b.Text("In: ");
                if (authors.Length > 0 && editors.Length > 0) b.Sentence($"{editors} (ed.)");
                b.Bold(item.ContainerTitle);
                b.Sentence(Edition(item.Edition));
                b.Imprint(item, catalog);
                b.Sentence(Prefixed("p.", item.Page));
                break;

            case "thesis":
                b.HighlightedTitle(item.Title);
                b.Sentence(catalog.YearText(item));
                b.Sentence(string.Join(" – ", new[] { item.Genre ?? "Trabalho acadêmico", JoinComma(item.Publisher, item.PublisherPlace) }
                    .Where(s => !string.IsNullOrWhiteSpace(s))));
                break;

            case "webpage" or "post-weblog" or "post":
                b.HighlightedTitle(item.Title);
                b.Sentence(JoinComma(item.PublisherPlace, catalog.YearText(item)));
                break;

            default: // book, report, manuscript, ...
                b.HighlightedTitle(item.Title);
                b.Sentence(Edition(item.Edition));
                b.Imprint(item, catalog);
                break;
        }

        if (!string.IsNullOrWhiteSpace(item.Doi)) b.Sentence($"DOI: {item.Doi.Trim()}");
        if (!string.IsNullOrWhiteSpace(item.Url))
        {
            b.Sentence($"Disponível em: {item.Url.Trim()}");
            if (AccessDate(item.Accessed) is { } accessed) b.Sentence($"Acesso em: {accessed}");
        }

        return b.Segments;
    }

    /// <summary>"SOUZA, Ana; PEREIRA, Carlos" — every author listed.</summary>
    public static string Names(IReadOnlyList<CslName>? names) =>
        names is null
            ? ""
            : string.Join("; ", names.Select(n => n.Literal is not null
                    ? n.Literal.ToUpper(PtBr)
                    : string.IsNullOrWhiteSpace(n.Given)
                        ? (n.Family ?? "").ToUpper(PtBr)
                        : $"{(n.Family ?? "").ToUpper(PtBr)}, {n.Given}")
                .Where(s => s.Length > 0));

    private static string? Edition(string? edition)
    {
        if (string.IsNullOrWhiteSpace(edition) || edition.Trim() == "1") return null;
        return Digits().IsMatch(edition.Trim()) ? $"{edition.Trim()}. ed" : edition.Trim();
    }

    private static string? Prefixed(string prefix, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"{prefix} {value.Trim()}";

    private static string? JoinComma(params string?[] parts)
    {
        var present = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
        return present.Count == 0 ? null : string.Join(", ", present);
    }

    private static string? AccessDate(CslDate? date)
    {
        if (date?.DateParts is not [{ Count: 3 } parts, ..]) return date?.Literal;
        var (year, month, day) = (parts[0], parts[1], parts[2]);
        return month is >= 1 and <= 12 ? $"{day} {Months[month - 1]} {year}" : null;
    }

    private sealed class Builder
    {
        public List<Segment> Segments { get; } = [];

        public void Text(string text) => Segments.Add(new Segment(text));

        /// <summary>Appends text and closes it with a period (no double periods).</summary>
        public void Sentence(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var t = text.Trim();
            Segments.Add(new Segment(t.EndsWith('.') ? t + " " : t + ". "));
        }

        public void Bold(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Segments.Add(new Segment(text.Trim().TrimEnd('.'), Bold: true));
            Segments.Add(new Segment(". "));
        }

        /// <summary>Title in bold; a subtitle after ": " stays regular.</summary>
        public void HighlightedTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            var t = title.Trim().TrimEnd('.');
            var colon = t.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0)
            {
                Segments.Add(new Segment(t[..colon], Bold: true));
                Segments.Add(new Segment(t[colon..] + ". "));
            }
            else
            {
                Segments.Add(new Segment(t, Bold: true));
                Segments.Add(new Segment(". "));
            }
        }

        /// <summary>"Place: Publisher, year."</summary>
        public void Imprint(CslItem item, ReferenceCatalog catalog)
        {
            var place = string.IsNullOrWhiteSpace(item.PublisherPlace) ? "[S. l.]" : item.PublisherPlace.Trim();
            var publisher = string.IsNullOrWhiteSpace(item.Publisher) ? "[s. n.]" : item.Publisher.Trim();
            Sentence($"{place}: {publisher}, {catalog.YearText(item)}");
        }

        public void Commas(Segment first, params string?[] parts)
        {
            var present = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (first.Text.Length > 0)
            {
                Segments.Add(first);
                if (present.Count > 0) Segments.Add(new Segment(", "));
            }
            if (present.Count > 0) Segments.Add(new Segment(string.Join(", ", present)));
        }

        public void End() => Segments.Add(new Segment(". "));
    }
}

/// <summary>Plain-text helpers used by the generator and tests.</summary>
public static class SegmentExtensions
{
    public static string ToPlainText(this IEnumerable<Segment> segments) => string.Concat(segments.Select(s => s.Text)).TrimEnd();
}
