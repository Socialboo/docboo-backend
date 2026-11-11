using System.Globalization;
using System.Text;
using Domain.Documents;

namespace Exporter.References;

/// <summary>A piece of formatted text; <see cref="Bold"/> marks the highlighted element of a reference.</summary>
public sealed record Segment(string Text, bool Bold = false);

/// <summary>
/// The work's references prepared for the author-date system: alphabetical order, citation labels and
/// "a/b" suffixes for the same author and year (e.g. 2021a, 2021b), shared by citations and the list.
/// </summary>
public sealed class ReferenceCatalog
{
    private static readonly CompareInfo Compare = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
    private const CompareOptions SortOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private readonly Dictionary<string, CslItem> _byId;
    private readonly Dictionary<string, string> _yearSuffix = new(StringComparer.Ordinal);

    public ReferenceCatalog(IReadOnlyList<CslItem> references)
    {
        _byId = references.ToDictionary(r => r.Id, StringComparer.Ordinal);
        Ordered = references
            .OrderBy(SortKey, Comparer<string>.Create((a, b) => Compare.Compare(a, b, SortOptions)))
            .ThenBy(r => YearOf(r) ?? int.MaxValue)
            .ThenBy(r => r.Title ?? "", Comparer<string>.Create((a, b) => Compare.Compare(a, b, SortOptions)))
            .ToList();

        // Same citation label and year → a, b, c ... in list order.
        foreach (var group in Ordered.GroupBy(r => (CitationLabel(r, uppercase: true), YearOf(r))).Where(g => g.Count() > 1))
        {
            var i = 0;
            foreach (var item in group) _yearSuffix[item.Id] = ((char)('a' + i++)).ToString();
        }
    }

    /// <summary>References in the order of the reference list.</summary>
    public IReadOnlyList<CslItem> Ordered { get; }

    public CslItem? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Year as printed (with disambiguation suffix) or "[s. d.]".</summary>
    public string YearText(CslItem item) =>
        YearOf(item) is { } year ? $"{year}{_yearSuffix.GetValueOrDefault(item.Id)}" : "[s. d.]";

    public static int? YearOf(CslItem item) =>
        item.Issued?.DateParts is [{ Count: > 0 } first, ..] ? first[0] : null;

    /// <summary>
    /// Author part of a citation. Up to three authors are listed; four or more become "First et al.".
    /// Parenthetical citations use uppercase family names (SOUZA; PEREIRA), narrative ones as written
    /// (Souza e Pereira). Without author, the first word of the title followed by "...".
    /// </summary>
    public static string CitationLabel(CslItem item, bool uppercase)
    {
        var names = (item.Author is { Count: > 0 } ? item.Author : item.Editor ?? [])
            .Select(n => n.Literal ?? n.Family ?? n.Given ?? "")
            .Where(n => n.Length > 0)
            .ToList();

        if (names.Count == 0)
        {
            var firstWord = (item.Title ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return (uppercase ? firstWord.ToUpper(CultureInfo.GetCultureInfo("pt-BR")) : firstWord) + "...";
        }

        if (uppercase) names = names.Select(n => n.ToUpper(CultureInfo.GetCultureInfo("pt-BR"))).ToList();
        if (names.Count >= 4) return $"{names[0]} et al.";
        if (uppercase) return string.Join("; ", names);
        return names.Count switch
        {
            1 => names[0],
            2 => $"{names[0]} e {names[1]}",
            _ => $"{names[0]}, {names[1]} e {names[2]}",
        };
    }

    private static string SortKey(CslItem item)
    {
        var first = item.Author is { Count: > 0 } ? item.Author[0] : item.Editor is { Count: > 0 } ? item.Editor[0] : null;
        var key = first is null ? item.Title ?? "" : first.Literal ?? $"{first.Family} {first.Given}";
        return RemoveDiacritics(key).Trim();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}
