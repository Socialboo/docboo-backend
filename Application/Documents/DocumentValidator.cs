using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common;
using Domain.Documents;
using Domain.Profiles;
using Microsoft.Extensions.Options;

namespace Application.Documents;

/// <summary>
/// Structural validation of a (sanitized) document before it is stored. Drafts are allowed:
/// empty metadata fields are fine here. Completeness for export is checked separately by the exporter.
/// </summary>
public sealed partial class DocumentValidator(IOptions<DocumentLimits> limits, INormProfileCatalog profiles)
{
    private readonly DocumentLimits _limits = limits.Value;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]*$")]
    private static partial Regex IdPattern();

    /// <param name="document">Sanitized document.</param>
    /// <param name="expectedProfileId">Profile of the work being saved (profiles cannot change after creation).</param>
    public IReadOnlyList<ValidationError> Validate(DocumentModel document, string expectedProfileId)
    {
        var ctx = new Context(_limits);

        if (document.SchemaVersion != DocumentModel.CurrentSchemaVersion)
            ctx.Error("schemaVersion", "schema.unsupported", $"Versão de schema não suportada. Esperada: {DocumentModel.CurrentSchemaVersion}.");

        var profile = profiles.Find(document.ProfileId);
        if (document.ProfileId != expectedProfileId)
            ctx.Error("profileId", "profile.mismatch", "O perfil do documento não corresponde ao perfil do trabalho.");
        else if (profile is null)
            ctx.Error("profileId", "profile.unknown", "Perfil de norma desconhecido.");
        ctx.MaxDepth = profile?.MaxSectionDepth ?? 5;

        ValidateMetadata(ctx, document.Metadata);
        ValidateAbstract(ctx, "preTextual.abstractPt", document.PreTextual.AbstractPt);
        ValidateAbstract(ctx, "preTextual.abstractEn", document.PreTextual.AbstractEn);

        // References first so citations can be checked against them.
        if (document.References.Count > _limits.MaxReferences)
            ctx.Error("references", "references.tooMany", $"Máximo de {_limits.MaxReferences} referências.");
        for (var i = 0; i < document.References.Count; i++)
            ValidateReference(ctx, $"references[{i}]", document.References[i]);

        if (document.Textual.Sections.Count == 0)
            ctx.Error("textual.sections", "sections.empty", "O trabalho precisa de ao menos uma seção.");
        for (var i = 0; i < document.Textual.Sections.Count; i++)
            ValidateSection(ctx, $"textual.sections[{i}]", document.Textual.Sections[i], depth: 1);

        ValidateAttachments(ctx, "postTextual.appendices", document.PostTextual.Appendices);
        ValidateAttachments(ctx, "postTextual.annexes", document.PostTextual.Annexes);

        if (ctx.BlockCount > _limits.MaxBlocks)
            ctx.Error("textual", "blocks.tooMany", $"Máximo de {_limits.MaxBlocks} blocos.");

        foreach (var (path, refId) in ctx.Citations)
        {
            if (!ctx.ReferenceIds.Contains(refId))
                ctx.Error(path, "citation.unknownReference", $"A citação aponta para a referência inexistente '{refId}'.");
        }

        if (ctx.Errors.Count == 0)
        {
            var bytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(document, DocumentJson.Options));
            if (bytes > _limits.MaxDocumentBytes)
                ctx.Error("", "document.tooLarge", $"Documento excede o tamanho máximo ({_limits.MaxDocumentBytes / 1024} KiB).");
        }

        return ctx.Errors;
    }

    private void ValidateMetadata(Context ctx, DocumentMetadata m)
    {
        ctx.ShortText("metadata.institution", m.Institution);
        ctx.ShortText("metadata.faculty", m.Faculty);
        ctx.ShortText("metadata.course", m.Course);
        ctx.ShortText("metadata.title", m.Title);
        ctx.ShortText("metadata.subtitle", m.Subtitle);
        ctx.ShortText("metadata.advisor", m.Advisor);
        ctx.ShortText("metadata.coAdvisor", m.CoAdvisor);
        ctx.ShortText("metadata.city", m.City);
        ctx.Text("metadata.nature", m.Nature, _limits.MaxAbstractLength);

        if (m.Authors.Count > _limits.MaxAuthors)
            ctx.Error("metadata.authors", "authors.tooMany", $"Máximo de {_limits.MaxAuthors} autores.");
        for (var i = 0; i < m.Authors.Count; i++)
            ctx.ShortText($"metadata.authors[{i}]", m.Authors[i]);

        if (m.Year is < 1900 or > 2100)
            ctx.Error("metadata.year", "year.outOfRange", "Ano deve estar entre 1900 e 2100.");
    }

    private void ValidateAbstract(Context ctx, string path, AbstractElement a)
    {
        ctx.Text($"{path}.text", a.Text, _limits.MaxAbstractLength);
        if (a.Keywords.Count > _limits.MaxKeywords)
            ctx.Error($"{path}.keywords", "keywords.tooMany", $"Máximo de {_limits.MaxKeywords} palavras-chave.");
        for (var i = 0; i < a.Keywords.Count; i++)
            ctx.Text($"{path}.keywords[{i}]", a.Keywords[i], _limits.MaxKeywordLength);
    }

    private void ValidateReference(Context ctx, string path, CslItem r)
    {
        if (ctx.CheckId($"{path}.id", r.Id) && !ctx.ReferenceIds.Add(r.Id))
            ctx.Error($"{path}.id", "id.duplicate", $"Id de referência duplicado '{r.Id}'.");
        if (r.Type.Length == 0)
            ctx.Error($"{path}.type", "required", "Tipo da referência é obrigatório.");
        ctx.ShortText($"{path}.type", r.Type);
        ctx.Text($"{path}.title", r.Title, _limits.MaxShortTextLength * 2);
        ctx.Text($"{path}.containerTitle", r.ContainerTitle, _limits.MaxShortTextLength * 2);
        if (r.Url is not null && !(Uri.TryCreate(r.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            ctx.Error($"{path}.URL", "url.invalid", "URL deve ser http(s) absoluta.");
        if (r.Extra is { Count: > 50 })
            ctx.Error(path, "reference.tooManyFields", "Campos CSL extras demais.");
    }

    private void ValidateSection(Context ctx, string path, Section section, int depth)
    {
        if (depth > ctx.MaxDepth)
        {
            ctx.Error(path, "section.tooDeep", $"Seções suportam no máximo {ctx.MaxDepth} níveis.");
            return;
        }

        ctx.UniqueId($"{path}.id", section.Id);
        ctx.ShortText($"{path}.title", section.Title);
        ValidateBlocks(ctx, $"{path}.blocks", section.Blocks);
        for (var i = 0; i < section.Children.Count; i++)
            ValidateSection(ctx, $"{path}.children[{i}]", section.Children[i], depth + 1);
    }

    private void ValidateAttachments(Context ctx, string path, IReadOnlyList<Attachment> attachments)
    {
        for (var i = 0; i < attachments.Count; i++)
        {
            var p = $"{path}[{i}]";
            ctx.UniqueId($"{p}.id", attachments[i].Id);
            ctx.ShortText($"{p}.title", attachments[i].Title);
            ValidateBlocks(ctx, $"{p}.blocks", attachments[i].Blocks);
        }
    }

    private void ValidateBlocks(Context ctx, string path, IReadOnlyList<Block> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            var p = $"{path}[{i}]";
            var block = blocks[i];
            ctx.BlockCount++;
            ctx.UniqueId($"{p}.id", block.Id);

            switch (block)
            {
                case ParagraphBlock para:
                    ValidateInlines(ctx, $"{p}.content", para.Content, inFootnote: false);
                    break;
                case LongQuoteBlock quote:
                    ValidateInlines(ctx, $"{p}.content", quote.Content, inFootnote: false);
                    break;
                case ListBlock list:
                    if (list.Items.Count == 0)
                        ctx.Error($"{p}.items", "list.empty", "Lista sem itens.");
                    for (var j = 0; j < list.Items.Count; j++)
                        ValidateInlines(ctx, $"{p}.items[{j}].content", list.Items[j].Content, inFootnote: false);
                    break;
                case FigureBlock figure:
                    ctx.CheckId($"{p}.assetId", figure.AssetId);
                    if (figure.WidthPercent is < 10 or > 100)
                        ctx.Error($"{p}.widthPercent", "figure.width", "Largura deve estar entre 10% e 100%.");
                    ValidateInlines(ctx, $"{p}.caption", figure.Caption, inFootnote: false);
                    ValidateInlines(ctx, $"{p}.source", figure.Source, inFootnote: false);
                    break;
                case TableBlock table:
                    ValidateTable(ctx, p, table);
                    break;
            }
        }
    }

    private void ValidateTable(Context ctx, string path, TableBlock table)
    {
        ValidateInlines(ctx, $"{path}.title", table.Title, inFootnote: false);
        ValidateInlines(ctx, $"{path}.source", table.Source, inFootnote: false);

        if (table.Rows.Count == 0)
            ctx.Error($"{path}.rows", "table.empty", "Tabela sem linhas.");
        if (table.Rows.Count > _limits.MaxTableRows)
            ctx.Error($"{path}.rows", "table.tooManyRows", $"Máximo de {_limits.MaxTableRows} linhas.");

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var rowPath = $"{path}.rows[{r}]";
            if (row.Cells.Count == 0)
                ctx.Error($"{rowPath}.cells", "table.emptyRow", "Linha sem células.");

            var width = 0;
            for (var c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                var cellPath = $"{rowPath}.cells[{c}]";
                if (cell.ColSpan < 1 || cell.ColSpan > _limits.MaxTableColumns)
                    ctx.Error($"{cellPath}.colSpan", "table.span", "colSpan inválido.");
                if (cell.RowSpan < 1 || cell.RowSpan > table.Rows.Count - r)
                    ctx.Error($"{cellPath}.rowSpan", "table.span", "rowSpan inválido.");
                width += Math.Max(cell.ColSpan, 1);
                ValidateInlines(ctx, $"{cellPath}.content", cell.Content, inFootnote: false);
            }
            if (width > _limits.MaxTableColumns)
                ctx.Error(rowPath, "table.tooManyColumns", $"Máximo de {_limits.MaxTableColumns} colunas.");
        }
    }

    private void ValidateInlines(Context ctx, string path, IReadOnlyList<Inline> inlines, bool inFootnote)
    {
        for (var i = 0; i < inlines.Count; i++)
        {
            var p = $"{path}[{i}]";
            switch (inlines[i])
            {
                case TextInline text:
                    ctx.Text($"{p}.text", text.Text, _limits.MaxTextInlineLength);
                    break;
                case CitationInline citation:
                    if (citation.RefIds.Count == 0)
                        ctx.Error($"{p}.refIds", "citation.empty", "Citação sem referência.");
                    for (var j = 0; j < citation.RefIds.Count; j++)
                        ctx.Citations.Add(($"{p}.refIds[{j}]", citation.RefIds[j]));
                    ctx.ShortText($"{p}.locator", citation.Locator);
                    break;
                case FootnoteInline footnote:
                    if (inFootnote)
                        ctx.Error(p, "footnote.nested", "Notas de rodapé não podem conter outras notas.");
                    ctx.UniqueId($"{p}.id", footnote.Id);
                    ValidateInlines(ctx, $"{p}.content", footnote.Content, inFootnote: true);
                    break;
            }
        }
    }

    private sealed class Context(DocumentLimits limits)
    {
        public List<ValidationError> Errors { get; } = [];
        public HashSet<string> Ids { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ReferenceIds { get; } = new(StringComparer.Ordinal);
        public List<(string Path, string RefId)> Citations { get; } = [];
        public int BlockCount { get; set; }
        public int MaxDepth { get; set; }

        public void Error(string path, string code, string message) => Errors.Add(new ValidationError(path, code, message));

        public bool CheckId(string path, string id)
        {
            if (id.Length == 0 || id.Length > limits.MaxIdLength || !IdPattern().IsMatch(id))
            {
                Error(path, "id.invalid", $"Id inválido: use letras, números, '-' ou '_' (até {limits.MaxIdLength}).");
                return false;
            }
            return true;
        }

        /// <summary>Section, block, attachment and footnote ids share one namespace.</summary>
        public void UniqueId(string path, string id)
        {
            if (CheckId(path, id) && !Ids.Add(id))
                Error(path, "id.duplicate", $"Id duplicado '{id}'.");
        }

        public void ShortText(string path, string? value) => Text(path, value, limits.MaxShortTextLength);

        public void Text(string path, string? value, int max)
        {
            if (value is not null && value.Length > max)
                Error(path, "text.tooLong", $"Texto excede {max} caracteres.");
        }
    }
}
