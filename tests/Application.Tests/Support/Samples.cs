using Application.Documents;
using Domain.Documents;
using Exporter.Profiles;
using Microsoft.Extensions.Options;

namespace Application.Tests.Support;

internal static class Samples
{
    public static string GoldenJson => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "golden-tcc.json"));

    public static DocumentModel Golden() => DocumentJson.Deserialize(GoldenJson);

    public static DocumentValidator Validator(DocumentLimits? limits = null) =>
        new(Options.Create(limits ?? new DocumentLimits()), new EmbeddedNormProfileCatalog());

    public static TextInline Text(string text, params TextMark[] marks) => new() { Text = text, Marks = marks };

    public static ParagraphBlock Paragraph(string id, params Inline[] content) => new() { Id = id, Content = content };

    public static Section Section(string id, IReadOnlyList<Block>? blocks = null, IReadOnlyList<Section>? children = null) =>
        new() { Id = id, Title = id, Blocks = blocks ?? [], Children = children ?? [] };

    public static DocumentModel WithSections(this DocumentModel doc, params Section[] sections) =>
        doc with { Textual = new Textual { Sections = sections } };
}
