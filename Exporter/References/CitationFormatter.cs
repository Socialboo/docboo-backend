using Domain.Documents;

namespace Exporter.References;

/// <summary>
/// Author-date citations in the text:
/// parenthetical "(SOUZA; PEREIRA, 2021, p. 12)" and narrative "Souza e Pereira (2021, p. 12)".
/// Several references in one citation are separated by ";" and the locator goes with the last one.
/// </summary>
public sealed class CitationFormatter(ReferenceCatalog catalog)
{
    public string Format(CitationInline citation)
    {
        var items = citation.RefIds.Select(id => catalog.Find(id)).ToList();
        var locator = string.IsNullOrWhiteSpace(citation.Locator) ? "" : $", {citation.Locator.Trim()}";

        if (citation.Mode == CitationMode.Parenthetical)
        {
            var parts = items.Select((item, i) =>
            {
                var label = item is null ? "[?]" : $"{ReferenceCatalog.CitationLabel(item, uppercase: true)}, {catalog.YearText(item)}";
                return i == items.Count - 1 ? label + locator : label;
            });
            return $"({string.Join("; ", parts)})";
        }

        var narrative = items.Select((item, i) =>
        {
            if (item is null) return "[?]";
            var tail = i == items.Count - 1 ? locator : "";
            return $"{ReferenceCatalog.CitationLabel(item, uppercase: false)} ({catalog.YearText(item)}{tail})";
        }).ToList();

        return narrative.Count switch
        {
            1 => narrative[0],
            _ => string.Join(", ", narrative[..^1]) + " e " + narrative[^1],
        };
    }
}
