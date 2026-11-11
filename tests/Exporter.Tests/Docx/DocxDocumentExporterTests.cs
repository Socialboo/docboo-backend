using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Domain.Documents;
using Exporter.Docx;
using Exporter.Profiles;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Exporter.Tests.Docx;

public sealed class DocxDocumentExporterTests : IDisposable
{
    private static readonly EmbeddedNormProfileCatalog Profiles = new();

    private readonly MemoryStream _stream;
    private readonly WordprocessingDocument _docx;
    private readonly DocxExportResult _result;

    public DocxDocumentExporterTests()
    {
        _result = Export(Golden());
        _stream = new MemoryStream(_result.Content);
        _docx = WordprocessingDocument.Open(_stream, false);
    }

    public void Dispose()
    {
        _docx.Dispose();
        _stream.Dispose();
    }

    internal static DocumentModel Golden() =>
        DocumentJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "golden-tcc.json")));

    internal static DocxExportResult Export(DocumentModel doc) => new DocxDocumentExporter(Profiles).Export(doc);

    private Body Body => _docx.MainDocumentPart!.Document!.Body!;

    private List<SectionProperties> Sections => Body.Descendants<SectionProperties>().ToList();

    private Style Style(string id) =>
        _docx.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<Style>().Single(s => s.StyleId == id);

    private List<Paragraph> ParagraphsWithStyle(string styleId) =>
        Body.Descendants<Paragraph>().Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val == styleId).ToList();

    [Fact]
    public void Produces_a_schema_valid_docx()
    {
        var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(_docx).ToList();

        Assert.True(errors.Count == 0,
            string.Join("\n", errors.Take(10).Select(e => $"{e.Path?.XPath}: {e.Description}")));
    }

    [Fact]
    public void Has_one_section_per_part_with_profile_page_setup()
    {
        // cover, title page, abstracts, textual
        Assert.Equal(4, Sections.Count);
        foreach (var sectPr in Sections)
        {
            var size = sectPr.GetFirstChild<PageSize>()!;
            var margin = sectPr.GetFirstChild<PageMargin>()!;
            Assert.Equal((11906u, 16838u), (size.Width!.Value, size.Height!.Value));
            Assert.Equal((1701, 1701u, 1134, 1134u), (margin.Top!.Value, margin.Left!.Value, margin.Bottom!.Value, margin.Right!.Value));
        }
    }

    [Fact]
    public void Counts_pages_from_the_title_page_and_shows_numbers_from_the_textual_part()
    {
        var sections = Sections;

        // Count restarts at 1 on the title page (cover not counted) and is never restarted again.
        Assert.Null(sections[0].GetFirstChild<PageNumberType>());
        Assert.Equal(1, sections[1].GetFirstChild<PageNumberType>()?.Start?.Value);
        Assert.Null(sections[2].GetFirstChild<PageNumberType>()?.Start);
        Assert.Null(sections[3].GetFirstChild<PageNumberType>()?.Start);

        // Page number printed only from the textual section on.
        Assert.Equal([false, false, false, true], sections.Select(s => s.Elements<HeaderReference>().Any()));

        var header = _docx.MainDocumentPart!.HeaderParts.Single().Header!;
        Assert.Equal(" PAGE ", header.Descendants<SimpleField>().Single().Instruction!.Value);
        Assert.Equal(StyleIds.Header, header.Descendants<ParagraphStyleId>().Single().Val!.Value);
        Assert.Equal(JustificationValues.Right, Style(StyleIds.Header).StyleParagraphProperties!.Justification!.Val!.Value);
    }

    [Fact]
    public void Normal_style_follows_the_body_rules()
    {
        var normal = Style(StyleIds.Normal);
        var pPr = normal.StyleParagraphProperties!;
        var rPr = normal.StyleRunProperties!;

        Assert.True(normal.Default!.Value);
        Assert.Equal("360", pPr.SpacingBetweenLines!.Line!.Value); // 1.5
        Assert.Equal(LineSpacingRuleValues.Auto, pPr.SpacingBetweenLines.LineRule!.Value);
        Assert.Equal("709", pPr.Indentation!.FirstLine!.Value);    // 1.25 cm
        Assert.Equal(JustificationValues.Both, pPr.Justification!.Val!.Value);
        Assert.Equal("24", rPr.FontSize!.Val!.Value);               // 12 pt

        var defaults = _docx.MainDocumentPart!.StyleDefinitionsPart!.Styles!.DocDefaults!.RunPropertiesDefault!.RunPropertiesBaseStyle!;
        Assert.Equal("Times New Roman", defaults.RunFonts!.Ascii!.Value);
        Assert.Equal("pt-BR", defaults.Languages!.Val!.Value);
    }

    [Fact]
    public void Long_quote_style_follows_the_profile()
    {
        var quote = Style(StyleIds.Quote);

        Assert.Equal("quote", quote.StyleName!.Val!.Value, ignoreCase: true);
        Assert.Equal("2268", quote.StyleParagraphProperties!.Indentation!.Left!.Value); // 4 cm
        Assert.Equal("240", quote.StyleParagraphProperties.SpacingBetweenLines!.Line!.Value); // single
        Assert.Equal("20", quote.StyleRunProperties!.FontSize!.Val!.Value); // 10 pt
        Assert.Single(ParagraphsWithStyle(StyleIds.Quote));
    }

    [Fact]
    public void Headings_use_builtin_styles_numbering_and_outline_levels()
    {
        for (var level = 1; level <= 5; level++)
        {
            var style = Style(StyleIds.Heading(level));
            Assert.Equal($"heading {level}", style.StyleName!.Val!.Value);
            Assert.Equal(level - 1, style.StyleParagraphProperties!.OutlineLevel!.Val!.Value);
            Assert.Equal(level - 1, style.StyleParagraphProperties.NumberingProperties!.NumberingLevelReference!.Val!.Value);
        }

        Assert.NotNull(Style(StyleIds.Heading(1)).StyleParagraphProperties!.PageBreakBefore);
        Assert.NotNull(Style(StyleIds.Heading(1)).StyleRunProperties!.Bold);
        Assert.NotNull(Style(StyleIds.Heading(1)).StyleRunProperties!.Caps);
        Assert.Null(Style(StyleIds.Heading(2)).StyleRunProperties!.Bold);
        Assert.NotNull(Style(StyleIds.Heading(5)).StyleRunProperties!.Italic);

        var levelTexts = _docx.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!
            .Elements<AbstractNum>().First().Elements<Level>().Select(l => l.LevelText!.Val!.Value).ToList();
        Assert.Equal(["%1", "%1.%2", "%1.%2.%3", "%1.%2.%3.%4", "%1.%2.%3.%4.%5"], levelTexts);

        Assert.Equal(["Introdução", "Fundamentação teórica", "Resultados", "Conclusão"], ParagraphsWithStyle("Heading1").Select(p => p.InnerText));
        Assert.Equal(["Objetivos", "Documentos estruturados"], ParagraphsWithStyle("Heading2").Select(p => p.InnerText));
        Assert.Equal(["Estilos de parágrafo"], ParagraphsWithStyle("Heading3").Select(p => p.InnerText));
    }

    [Fact]
    public void Cover_and_title_page_contain_the_metadata()
    {
        var sections = Body.Elements<Table>().ToList();
        Assert.Equal(2, sections.Count); // layout tables of cover and title page

        var cover = sections[0].InnerText;
        Assert.Contains("Universidade Federal Exemplo", cover);
        Assert.Contains("Maria Exemplo da Silva", cover);
        Assert.Contains("Formatação automática de trabalhos acadêmicos: um estudo de caso com documentos estruturados", cover);
        Assert.Contains("São Paulo2026", cover);

        var titlePage = sections[1].InnerText;
        Assert.Contains("Trabalho de Conclusão de Curso apresentado", titlePage);
        Assert.Contains("Orientador: Prof. Dr. João Orientador", titlePage);

        var nature = Style(StyleIds.NatureOfWork).StyleParagraphProperties!;
        Assert.Equal("4535", nature.Indentation!.Left!.Value); // 8 cm
    }

    [Fact]
    public void Abstracts_have_titles_text_and_keywords()
    {
        Assert.Equal(["RESUMO", "ABSTRACT"], ParagraphsWithStyle(StyleIds.PreTextualTitle).Select(p => p.InnerText));
        var abstractText = ParagraphsWithStyle(StyleIds.AbstractText).Select(p => p.InnerText).ToList();
        Assert.Contains("Palavras-chave: formatação; documentos estruturados; normalização.", abstractText);
        Assert.Contains("Keywords: formatting; structured documents; standards.", abstractText);
    }

    [Fact]
    public void Body_keeps_inline_formatting_and_lists()
    {
        var italic = Body.Descendants<W.Run>().Single(r => r.InnerText == "fundamental");
        Assert.NotNull(italic.RunProperties!.Italic);

        var listItems = ParagraphsWithStyle(StyleIds.ListParagraph);
        Assert.Equal(3, listItems.Count);
        Assert.All(listItems, p => Assert.NotNull(p.ParagraphProperties!.NumberingProperties));
        Assert.Single(Body.Descendants<Break>()); // hardBreak in the conclusion
    }

    [Fact]
    public void Reports_content_not_rendered_yet()
    {
        var warnings = string.Join("\n", _result.Warnings);

        Assert.Contains("1 figura(s)", warnings);
        Assert.Contains("1 tabela(s)", warnings);
        Assert.Contains("1 nota(s) de rodapé", warnings);
        Assert.Contains("3 citação(ões)", warnings);
        Assert.Contains("referências", warnings);
        Assert.Contains("1 apêndice(s)", warnings);
        Assert.Contains("1 anexo(s)", warnings);
        Assert.Contains("sumário", warnings);
    }

    [Fact]
    public void Applies_font_override_and_optional_parts()
    {
        var doc = Golden();
        doc = doc with
        {
            ProfileOverrides = new ProfileOverrides { FontFamily = FontFamilyOption.Arial },
            PreTextual = doc.PreTextual with
            {
                Cover = new ToggleElement { Enabled = false },
                AbstractEn = doc.PreTextual.AbstractEn with { Enabled = false },
            },
        };

        using var stream = new MemoryStream(Export(doc).Content);
        using var docx = WordprocessingDocument.Open(stream, false);

        var fonts = docx.MainDocumentPart!.StyleDefinitionsPart!.Styles!.DocDefaults!.RunPropertiesDefault!.RunPropertiesBaseStyle!.RunFonts!;
        Assert.Equal("Arial", fonts.Ascii!.Value);

        var sections = docx.MainDocumentPart.Document!.Body!.Descendants<SectionProperties>().ToList();
        Assert.Equal(3, sections.Count); // title page, abstract, textual
        Assert.Equal(1, sections[0].GetFirstChild<PageNumberType>()?.Start?.Value); // title page is still page 1
        Assert.DoesNotContain("ABSTRACT", docx.MainDocumentPart.Document.Body.InnerText);
    }

    [Fact]
    public void Writes_core_properties()
    {
        Assert.Equal("Formatação automática de trabalhos acadêmicos", _docx.PackageProperties.Title);
        Assert.Equal("Maria Exemplo da Silva", _docx.PackageProperties.Creator);
    }
}
