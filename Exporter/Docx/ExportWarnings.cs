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
        "figure" => $"{count} figura(s) ainda não exportada(s): suporte a figuras chega na próxima versão.",
        "table" => $"{count} tabela(s) ainda não exportada(s): suporte a tabelas chega na próxima versão.",
        "footnote" => $"{count} nota(s) de rodapé ainda não exportada(s): suporte chega na próxima versão.",
        "citation" => $"{count} citação(ões) exportada(s) apenas com o identificador da referência: a formatação autor-data chega na próxima versão.",
        "references" => "A lista de referências ainda não é exportada: chega na próxima versão.",
        "appendices" => $"{count} apêndice(s) ainda não exportado(s): chega na próxima versão.",
        "annexes" => $"{count} anexo(s) ainda não exportado(s): chega na próxima versão.",
        "lists" => "As listas de figuras e de tabelas ainda não são geradas: chegam na próxima versão.",
        "toc" => "O sumário ainda não é gerado: chega na próxima versão.",
        _ => $"{count} elemento(s) '{code}' não exportado(s).",
    };
}
