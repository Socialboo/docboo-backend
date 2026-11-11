using Exporter.Pdf;
using Exporter.Tests.Docx;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Exporter.Tests.Pdf;

/// <summary>Converts the golden sample once (LibreOffice is slow to start) and shares the PDF.</summary>
public sealed class GoldenPdfFixture
{
    public const double CmToPt = 72 / 2.54;

    public GoldenPdfFixture()
    {
        Converter = new LibreOfficeConverter(Options.Create(new LibreOfficeOptions()));
        if (!Converter.IsAvailable) return;

        var docx = DocxDocumentExporterTests.Export(DocxDocumentExporterTests.Golden()).Content;
        Pdf = Converter.ConvertDocxToPdfAsync(docx, CancellationToken.None).GetAwaiter().GetResult();

        // Keep the artifacts next to the test binaries for manual inspection.
        var outDir = Path.Combine(AppContext.BaseDirectory, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "golden-tcc.docx"), docx);
        File.WriteAllBytes(Path.Combine(outDir, "golden-tcc.pdf"), Pdf);
    }

    public LibreOfficeConverter Converter { get; }
    public byte[]? Pdf { get; }
}

public sealed class GoldenPdfTests(GoldenPdfFixture fixture) : IClassFixture<GoldenPdfFixture>
{
    private const double Cm = GoldenPdfFixture.CmToPt;

    private PdfDocument Open()
    {
        Skip.If(fixture.Pdf is null, "LibreOffice (soffice) não está instalado nesta máquina.");
        return PdfDocument.Open(fixture.Pdf);
    }

    /// <summary>Words inside the top margin (3 cm), where the page number is printed.</summary>
    private static List<Word> HeaderWords(Page page) =>
        page.GetWords().Where(w => w.BoundingBox.Bottom > page.Height - 3 * Cm).ToList();

    private static List<Word> BodyWords(Page page) =>
        page.GetWords().Where(w => w.BoundingBox.Bottom <= page.Height - 3 * Cm).ToList();

    private static int PageOf(PdfDocument pdf, string text) =>
        pdf.GetPages().First(p => p.Text.Contains(text, StringComparison.Ordinal)).Number;

    [SkippableFact]
    public void Has_expected_pages_all_A4()
    {
        using var pdf = Open();

        // cover, title page, resumo, abstract, 4 chapters (each primary section starts a new page)
        Assert.Equal(8, pdf.NumberOfPages);
        Assert.All(pdf.GetPages(), p =>
        {
            Assert.InRange(p.Width, 21 * Cm - 1, 21 * Cm + 1);
            Assert.InRange(p.Height, 29.7 * Cm - 1, 29.7 * Cm + 1);
        });
    }

    [SkippableFact]
    public void Pre_textual_pages_show_no_page_number()
    {
        using var pdf = Open();
        var introPage = PageOf(pdf, "INTRODUÇÃO");

        for (var n = 1; n < introPage; n++)
            Assert.DoesNotContain(HeaderWords(pdf.GetPage(n)), w => w.Text.All(char.IsDigit));
    }

    [SkippableFact]
    public void Textual_pages_show_the_count_from_the_title_page_at_top_right()
    {
        using var pdf = Open();
        var introPage = PageOf(pdf, "INTRODUÇÃO");
        Assert.Equal(5, introPage); // cover, title page, resumo, abstract

        for (var n = introPage; n <= pdf.NumberOfPages; n++)
        {
            var page = pdf.GetPage(n);
            var number = Assert.Single(HeaderWords(page));
            Assert.Equal((n - 1).ToString(), number.Text); // the cover is not counted
            Assert.InRange(number.BoundingBox.Right, page.Width - 2 * Cm - 2, page.Width - 2 * Cm + 2); // right margin
            Assert.InRange(number.BoundingBox.Top, page.Height - 2 * Cm - 14, page.Height - 2 * Cm + 2);   // 2 cm from top
        }
    }

    [SkippableFact]
    public void Chapters_start_at_the_same_height_on_every_page()
    {
        using var pdf = Open();
        // Measure the chapter number: accents and cedillas change the bounding box of the title words.
        var tops = new[] { ("1", "INTRODUÇÃO"), ("2", "FUNDAMENTAÇÃO"), ("3", "RESULTADOS"), ("4", "CONCLUSÃO") }
            .Select(c => BodyWords(pdf.GetPage(PageOf(pdf, c.Item2))).First(w => w.Text == c.Item1).BoundingBox.Top)
            .ToList();

        Assert.All(tops, top => Assert.InRange(top, tops[0] - 0.5, tops[0] + 0.5));
    }

    [SkippableFact]
    public void Body_text_respects_left_and_right_margins()
    {
        using var pdf = Open();
        var page = pdf.GetPage(PageOf(pdf, "INTRODUÇÃO"));
        var words = BodyWords(page);

        Assert.InRange(words.Min(w => w.BoundingBox.Left), 3 * Cm - 1.5, 3 * Cm + 1.5);
        Assert.InRange(words.Max(w => w.BoundingBox.Right), page.Width - 2 * Cm - 4, page.Width - 2 * Cm + 1);
        // Text starts below the 3 cm top margin.
        Assert.True(words.Max(w => w.BoundingBox.Top) <= page.Height - 3 * Cm + 1);
    }

    [SkippableFact]
    public void Renders_numbered_headings_and_pre_textual_content()
    {
        using var pdf = Open();
        var text = string.Join("\n", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));

        Assert.Contains("UNIVERSIDADE FEDERAL EXEMPLO", text);
        Assert.Contains("FORMATAÇÃO AUTOMÁTICA DE TRABALHOS ACADÊMICOS: um estudo de caso", text);
        Assert.Contains("Orientador: Prof. Dr. João Orientador", text);
        Assert.Contains("RESUMO", text);
        Assert.Contains("Palavras-chave: formatação; documentos estruturados; normalização.", text);
        Assert.Contains("1 INTRODUÇÃO", text);
        Assert.Contains("1.1 OBJETIVOS", text);
        Assert.Contains("2 FUNDAMENTAÇÃO TEÓRICA", text);
        Assert.Contains("2.1.1 Estilos de parágrafo", text);
        Assert.Contains("4 CONCLUSÃO", text);
    }

    [SkippableFact]
    public void Uses_the_profile_font_family()
    {
        using var pdf = Open();
        var fonts = pdf.GetPage(PageOf(pdf, "INTRODUÇÃO")).Letters.Select(l => l.FontName).Distinct().ToList();

        // Times New Roman on Windows; its metric-compatible Liberation Serif in the Linux container.
        Assert.All(fonts, f => Assert.Matches("(?i)times|liberationserif", f));
    }
}
