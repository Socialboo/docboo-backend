using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Domain.Assets;
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

    /// <summary>The golden figure image (assetId "asset-fluxo").</summary>
    internal static IReadOnlyDictionary<string, ImageData> GoldenImages() => new Dictionary<string, ImageData>
    {
        ["asset-fluxo"] = new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "fluxo-exportacao.png")), "image/png", 600, 240),
    };

    internal static DocxExportResult Export(DocumentModel doc) => new DocxDocumentExporter(Profiles).Export(doc, GoldenImages());

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
        Assert.Contains("(LIMA, 2019, p. 52)", ParagraphsWithStyle(StyleIds.Quote).Single().InnerText);
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
        // The first two tables are the layout tables of the cover and the title page.
        var sections = Body.Elements<Table>().Take(2).ToList();

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
        Assert.Equal(["RESUMO", "ABSTRACT", "LISTA DE FIGURAS", "LISTA DE TABELAS", "SUMÁRIO"],
            ParagraphsWithStyle(StyleIds.PreTextualTitle).Select(p => p.InnerText));
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
        Assert.NotNull(_docx.MainDocumentPart!.DocumentSettingsPart!.Settings!.Descendants<DoNotExpandShiftReturn>().SingleOrDefault());
    }

    [Fact]
    public void Renders_everything_in_the_golden_sample_without_warnings()
    {
        Assert.Empty(_result.Warnings);
    }

    [Fact]
    public void Formats_citations_author_date()
    {
        var text = Body.InnerText;

        Assert.Contains("(SOUZA; PEREIRA, 2021)", text);
        Assert.Contains("Segundo Lima (2019, p. 45)", text);
        Assert.Contains("(LIMA, 2019, p. 52)", text);
    }

    [Fact]
    public void Writes_footnotes_with_separators_and_reference_marks()
    {
        var footnotes = _docx.MainDocumentPart!.FootnotesPart!.Footnotes!.Elements<Footnote>().ToList();

        Assert.Equal([-1L, 0L, 1L], footnotes.Select(f => f.Id!.Value));
        Assert.Equal(FootnoteEndnoteValues.Separator, footnotes[0].Type!.Value);
        Assert.Contains("Por estrutura entende-se", footnotes[2].InnerText);
        Assert.Equal(StyleIds.FootnoteText, footnotes[2].Descendants<ParagraphStyleId>().Single().Val!.Value);

        var reference = Body.Descendants<FootnoteReference>().Single();
        Assert.Equal(1, reference.Id!.Value);
        Assert.Equal(StyleIds.FootnoteReference, ((W.Run)reference.Parent!).RunProperties!.RunStyle!.Val!.Value);
        Assert.Equal("20", Style(StyleIds.FootnoteText).StyleRunProperties!.FontSize!.Val!.Value);
    }

    [Fact]
    public void Writes_figure_with_numbered_caption_image_and_source()
    {
        var caption = ParagraphsWithStyle(StyleIds.Caption).First(p => p.InnerText.StartsWith("Figura"));
        Assert.Equal("Figura 1 – Fluxo de exportação do documento", caption.InnerText);
        Assert.Equal(" SEQ Figura \\* ARABIC ", caption.Descendants<SimpleField>().Single().Instruction!.Value);

        var figure = ParagraphsWithStyle(StyleIds.Figure).Single();
        var extent = figure.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().Single();
        Assert.Equal(Units.CmToEmu(16 * 0.8), extent.Cx!.Value);           // 80% of the 16 cm text width
        Assert.Equal(extent.Cx.Value * 240 / 600, extent.Cy!.Value);       // keeps the aspect ratio
        var image = _docx.MainDocumentPart!.ImageParts.Single();
        Assert.Equal("image/png", image.ContentType);

        var source = ParagraphsWithStyle(StyleIds.Source);
        Assert.Contains(source, p => p.InnerText == "Fonte: Elaborado pela autora (2026).");
    }

    [Fact]
    public void Writes_table_with_caption_open_sides_and_header_rule()
    {
        var caption = ParagraphsWithStyle(StyleIds.Caption).Single(p => p.InnerText.StartsWith("Tabela"));
        Assert.Equal("Tabela 1 – Tempo médio de formatação por documento", caption.InnerText);

        var table = Body.Elements<Table>().Single(t => t.InnerText.Contains("Manual"));
        var borders = table.GetFirstChild<TableProperties>()!.TableBorders!;
        Assert.Equal(BorderValues.Single, borders.TopBorder!.Val!.Value);
        Assert.Equal(BorderValues.Single, borders.BottomBorder!.Val!.Value);
        Assert.Equal(BorderValues.Nil, borders.LeftBorder!.Val!.Value);
        Assert.Equal(BorderValues.Nil, borders.InsideVerticalBorder!.Val!.Value);

        var header = table.Elements<W.TableRow>().First();
        Assert.NotNull(header.TableRowProperties!.GetFirstChild<TableHeader>());
        Assert.All(header.Elements<W.TableCell>(), c => Assert.NotNull(c.TableCellProperties!.TableCellBorders!.BottomBorder));
        Assert.Equal(StyleIds.TableHeader, header.Descendants<ParagraphStyleId>().First().Val!.Value);
    }

    [Fact]
    public void Merges_table_cells_with_row_and_column_spans()
    {
        var doc = Golden().WithBlocks(new TableBlock
        {
            Id = "t-span",
            Title = [new TextInline { Text = "Spans", Marks = [] }],
            Source = [],
            Rows =
            [
                new Domain.Documents.TableRow { Header = true, Cells = [Cell("A", colSpan: 2), Cell("B")] },
                new Domain.Documents.TableRow { Header = false, Cells = [Cell("C", rowSpan: 2), Cell("D"), Cell("E")] },
                new Domain.Documents.TableRow { Header = false, Cells = [Cell("F"), Cell("G")] },
            ],
        });

        using var stream = new MemoryStream(Export(doc).Content);
        using var docx = WordprocessingDocument.Open(stream, false);
        var table = docx.MainDocumentPart!.Document!.Body!.Elements<Table>().Single(t => t.InnerText.Contains("ABCDEFG") || t.InnerText.StartsWith("AB"));
        var rows = table.Elements<W.TableRow>().ToList();

        Assert.Equal(3, table.GetFirstChild<TableGrid>()!.Elements<GridColumn>().Count());
        Assert.Equal(2, rows[0].Elements<W.TableCell>().First().TableCellProperties!.GridSpan!.Val!.Value);
        Assert.Equal(MergedCellValues.Restart, rows[1].Elements<W.TableCell>().First().TableCellProperties!.VerticalMerge!.Val!.Value);
        var continued = rows[2].Elements<W.TableCell>().First();
        Assert.NotNull(continued.TableCellProperties!.VerticalMerge);
        Assert.Null(continued.TableCellProperties.VerticalMerge!.Val);
        Assert.Equal(["", "F", "G"], rows[2].Elements<W.TableCell>().Select(c => c.InnerText));
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(docx));
    }

    private static Domain.Documents.TableCell Cell(string text, int colSpan = 1, int rowSpan = 1) =>
        new() { Content = [new TextInline { Text = text, Marks = [] }], ColSpan = colSpan, RowSpan = rowSpan };

    [Fact]
    public void Writes_table_of_contents_and_lists_as_prefilled_fields()
    {
        var codes = Body.Descendants<FieldCode>().Select(f => f.Text).ToList();
        Assert.Contains(" TOC \\o \"1-5\" \\h \\z \\u ", codes);
        Assert.Contains(" TOC \\h \\z \\c \"Figura\" ", codes);
        Assert.Contains(" TOC \\h \\z \\c \"Tabela\" ", codes);

        Assert.Equal(
            ["1 Introdução", "1.1 Objetivos", "2 Fundamentação teórica", "2.1 Documentos estruturados", "2.1.1 Estilos de parágrafo",
             "3 Resultados", "4 Conclusão", "REFERÊNCIAS", "APÊNDICE A – Questionário aplicado aos participantes",
             "ANEXO A – Modelo de folha de aprovação da instituição"],
            Body.Descendants<Paragraph>().Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.StartsWith("TOC") == true)
                .Select(p => p.InnerText.Replace(codes.First(c => c.Contains("\\o")), "")));

        Assert.Equal(["Figura 1 – Fluxo de exportação do documento", "Tabela 1 – Tempo médio de formatação por documento"],
            ParagraphsWithStyle(StyleIds.TableOfFigures).Select(p => string.Concat(p.Descendants<W.Text>().Select(t => t.Text))));

        Assert.True(_docx.MainDocumentPart!.DocumentSettingsPart!.Settings!.GetFirstChild<UpdateFieldsOnOpen>()!.Val!.Value);
        Assert.Equal("toc 1", Style(StyleIds.Toc(1)).StyleName!.Val!.Value);
        Assert.NotNull(Style(StyleIds.Toc(1)).StyleRunProperties!.Bold);
    }

    [Fact]
    public void Writes_references_sorted_with_highlighted_titles()
    {
        var entries = ParagraphsWithStyle(StyleIds.Bibliography);

        Assert.Equal(["BIBLIOTECA CENTRAL EXEMPLO", "LIMA, Roberto", "SOUZA, Ana; PEREIRA, Carlos"], entries.Select(e => e.InnerText.Split('.')[0]));
        Assert.Equal("Editoração eletrônica", entries[1].Descendants<W.Run>().Single(r => r.RunProperties?.Bold is not null).InnerText);
        Assert.Equal("240", Style(StyleIds.Bibliography).StyleParagraphProperties!.SpacingBetweenLines!.Line!.Value); // single
        Assert.Equal("240", Style(StyleIds.Bibliography).StyleParagraphProperties!.SpacingBetweenLines!.After!.Value); // 12 pt
    }

    [Fact]
    public void Writes_post_textual_titles_in_order()
    {
        Assert.Equal(
            ["REFERÊNCIAS", "APÊNDICE A – Questionário aplicado aos participantes", "ANEXO A – Modelo de folha de aprovação da instituição"],
            ParagraphsWithStyle(StyleIds.PostTextualTitle).Select(p => p.InnerText));
        var style = Style(StyleIds.PostTextualTitle).StyleParagraphProperties!;
        Assert.Equal(0, style.OutlineLevel!.Val!.Value);
        Assert.NotNull(style.PageBreakBefore);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    public void Letters_appendices(int index, string expected)
    {
        Assert.Equal(expected, BodyWriter.Letters(index));
    }

    [Fact]
    public void Warns_when_a_figure_image_is_missing()
    {
        var result = new DocxDocumentExporter(Profiles).Export(Golden(), new Dictionary<string, ImageData>());

        Assert.Contains(result.Warnings, w => w.Contains("1 figura(s) sem imagem"));
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
        Assert.Equal(3, sections.Count); // title page, pre-textual, textual
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

internal static class GoldenDocumentExtensions
{
    /// <summary>Golden document with extra blocks at the end of its last chapter.</summary>
    public static DocumentModel WithBlocks(this DocumentModel doc, params Block[] blocks)
    {
        var sections = doc.Textual.Sections.ToList();
        sections[^1] = sections[^1] with { Blocks = [.. sections[^1].Blocks, .. blocks] };
        return doc with { Textual = new Textual { Sections = sections } };
    }
}
