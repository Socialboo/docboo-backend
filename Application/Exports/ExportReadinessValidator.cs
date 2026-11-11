using Application.Common;
using Domain.Documents;

namespace Application.Exports;

/// <summary>
/// Drafts may be incomplete, but an export needs the data the pre-textual pages print
/// (cover, title page, abstracts). Errors use the same paths as document validation.
/// </summary>
public static class ExportReadinessValidator
{
    public static IReadOnlyList<ValidationError> Validate(DocumentModel doc)
    {
        var errors = new List<ValidationError>();
        var m = doc.Metadata;

        void Require(bool ok, string path, string message)
        {
            if (!ok) errors.Add(new ValidationError(path, "export.required", message));
        }

        Require(!string.IsNullOrWhiteSpace(m.Title), "metadata.title", "Informe o título do trabalho.");
        Require(m.Authors.Count > 0, "metadata.authors", "Informe ao menos um autor.");
        Require(!string.IsNullOrWhiteSpace(m.City), "metadata.city", "Informe a cidade (local).");
        Require(m.Year is not null, "metadata.year", "Informe o ano.");

        if (doc.PreTextual.Cover.Enabled)
            Require(!string.IsNullOrWhiteSpace(m.Institution), "metadata.institution", "Informe a instituição (usada na capa).");

        if (doc.PreTextual.TitlePage.Enabled)
        {
            Require(!string.IsNullOrWhiteSpace(m.Nature), "metadata.nature", "Informe a natureza do trabalho (folha de rosto).");
            Require(!string.IsNullOrWhiteSpace(m.Advisor), "metadata.advisor", "Informe o orientador (folha de rosto).");
        }

        foreach (var (path, element, name) in new[]
                 {
                     ("preTextual.abstractPt", doc.PreTextual.AbstractPt, "resumo"),
                     ("preTextual.abstractEn", doc.PreTextual.AbstractEn, "abstract"),
                 })
        {
            if (!element.Enabled) continue;
            Require(!string.IsNullOrWhiteSpace(element.Text), $"{path}.text", $"Escreva o texto do {name} ou desative-o.");
            Require(element.Keywords.Count > 0, $"{path}.keywords", $"Informe as palavras-chave do {name}.");
        }

        return errors;
    }
}
