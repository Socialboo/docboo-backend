namespace Exporter.Docx;

/// <summary>
/// Content the generator could not render (yet). Reported back to the student with the export
/// instead of failing it. Messages are user-facing (pt-BR).
/// </summary>
public sealed class ExportWarnings
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    public void Add(string code) => _counts[code] = _counts.GetValueOrDefault(code) + 1;

    public IReadOnlyList<string> ToMessages() =>
        _counts.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => Message(kv.Key, kv.Value))
            .ToList();

    private static string Message(string code, int count) => code switch
    {
        "imageMissing" => $"{count} figura(s) sem imagem: envie a imagem novamente.",
        _ => $"{count} elemento(s) '{code}' não exportado(s).",
    };
}
