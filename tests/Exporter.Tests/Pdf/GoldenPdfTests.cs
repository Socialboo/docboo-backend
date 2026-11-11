using System.Text.RegularExpressions;
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

public sealed partial class GoldenPdfTests(GoldenPdfFixture fixture) : IClassFixture<GoldenPdfFixture>
{
    private const double Cm = GoldenPdfFixture.CmToPt;

    [GeneratedRegex(@"^(?<title>.+?)\s*\.{3,}\s*(?<page>\d+)$")]
    private static partial Regex IndexLine();

    private PdfDocument Open()
    {
        Skip.If(fixture.Pdf is null, "LibreOffice (soffice + Python/UNO) não está instalado nesta máquina.");
        return PdfDocument.Open(fixture.Pdf);
    }

    /// <summary>Words inside the top margin (3 cm), where the page number is printed.</summary>
    private static List<Word> HeaderWords(Page page) =>
        page.GetWords().Where(w => w.BoundingBox.Bottom > page.Height - 3 * Cm).ToList();

    private static List<Word> BodyWords(Page page) =>
        page.GetWords().Where(w => w.BoundingBox.Bottom <= page.Height - 3 * Cm).ToList();

    /// <summary>Text lines of the body, top to bottom (words grouped by baseline).</summary>
    private static List<string> Lines(Page page) =>
        BodyWords(page)
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
            .ToList();

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static int PageWithTitle(PdfDocument pdf, string title) =>
        pdf.GetPages().First(p => Lines(p).FirstOrDefault() == title).Number;

    /// <summary>"Title ..... 7" lines of an index page.</summary>
    private static List<(string Title, int Page)> IndexEntries(Page page) =>
        Lines(page).Skip(1)
            .Select(l => IndexLine().Match(l))
            .Where(m => m.Success)
            .Select(m => (Normalize(m.Groups["title"].Value), int.Parse(m.Groups["page"].Value)))
            .ToList();

    [SkippableFact]
    public void Has_expected_pages_all_A4()
    {
        using var pdf = Open();

        // cover, title page, resumo, abstract, lists of figures and tables, sumário, 4 chapters,
        // references, appendix, annex
        Assert.Equal(14, pdf.NumberOfPages);
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
        var introPage = PageWithTitle(pdf, "1 INTRODUÇÃO");

        for (var n = 1; n < introPage; n++)
            Assert.DoesNotContain(HeaderWords(pdf.GetPage(n)), w => w.Text.All(char.IsDigit));
    }

    [SkippableFact]
    public void Textual_and_post_textual_pages_show_the_count_from_the_title_page_at_top_right()
    {
        using var pdf = Open();
        var introPage = PageWithTitle(pdf, "1 INTRODUÇÃO");
        Assert.Equal(8, introPage);

        for (var n = introPage; n <= pdf.NumberOfPages; n++)
        {
            var page = pdf.GetPage(n);
            var number = Assert.Single(HeaderWords(page));
            Assert.Equal((n - 1).ToString(), number.Text); // the cover is not counted
            Assert.InRange(number.BoundingBox.Right, page.Width - 2 * Cm - 2, page.Width - 2 * Cm + 2);
            Assert.InRange(number.BoundingBox.Top, page.Height - 2 * Cm - 14, page.Height - 2 * Cm + 2);
        }
    }

    [SkippableFact]
    public void Table_of_contents_lists_every_heading_with_its_real_page_number()
    {
        using var pdf = Open();
        var tocPage = pdf.GetPage(PageWithTitle(pdf, "SUMÁRIO"));

        var entries = IndexEntries(tocPage);

        Assert.Equal(
            ["1 INTRODUÇÃO", "1.1 OBJETIVOS", "2 FUNDAMENTAÇÃO TEÓRICA", "2.1 DOCUMENTOS ESTRUTURADOS", "2.1.1 Estilos de parágrafo",
             "3 RESULTADOS", "4 CONCLUSÃO", "REFERÊNCIAS", "APÊNDICE A – QUESTIONÁRIO APLICADO AOS PARTICIPANTES",
             "ANEXO A – MODELO DE FOLHA DE APROVAÇÃO DA INSTITUIÇÃO"],
            entries.Select(e => e.Title));

        foreach (var (title, number) in entries)
        {
            // Printed number N is on PDF page N + 1 (the cover is not counted).
            var page = pdf.GetPage(number + 1);
            Assert.Contains(title, Lines(page).Select(Normalize));
        }
    }

    [SkippableFact]
    public void Lists_of_figures_and_tables_point_to_the_right_pages()
    {
        using var pdf = Open();

        foreach (var (listTitle, label) in new[] { ("LISTA DE FIGURAS", "Figura 1"), ("LISTA DE TABELAS", "Tabela 1") })
        {
            var entry = Assert.Single(IndexEntries(pdf.GetPage(PageWithTitle(pdf, listTitle))));
            Assert.StartsWith(label + " – ", entry.Title);
            Assert.Contains(Lines(pdf.GetPage(entry.Page + 1)), l => Normalize(l) == entry.Title);
        }
    }

    [SkippableFact]
    public void Footnote_is_printed_small_at_the_bottom_of_its_page()
    {
        using var pdf = Open();
        var page = pdf.GetPage(PageWithTitle(pdf, "1 INTRODUÇÃO"));

        var noteWords = page.GetWords().Where(w => w.BoundingBox.Bottom < 4 * Cm).ToList();
        Assert.StartsWith("1 Por estrutura entende-se", string.Join(" ", noteWords.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
        var noteLetters = noteWords.SelectMany(w => w.Letters).Where(l => char.IsLetter(l.Value[0])).ToList();
        Assert.All(noteLetters, l => Assert.InRange(l.PointSize, 9.5, 10.5));
    }

    [SkippableFact]
    public void Embeds_the_figure_image_between_caption_and_source()
    {
        using var pdf = Open();
        var page = pdf.GetPage(PageWithTitle(pdf, "3 RESULTADOS"));

        var image = Assert.Single(page.GetImages());
        var caption = page.GetWords().First(w => w.Text == "Figura");
        var source = page.GetWords().Last(w => w.Text == "Fonte:");
        Assert.True(image.BoundingBox.Top < caption.BoundingBox.Bottom, "image below the caption");
        Assert.True(image.BoundingBox.Bottom > source.BoundingBox.Top, "image above the source");
        Assert.InRange(image.BoundingBox.Width, 16 * 0.8 * Cm - 2, 16 * 0.8 * Cm + 2); // 80% of the text width
    }

    [SkippableFact]
    public void Line_before_a_manual_break_is_not_stretched()
    {
        using var pdf = Open();
        var page = pdf.GetPage(PageWithTitle(pdf, "4 CONCLUSÃO"));
        var line = BodyWords(page)
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom))
            .Single(g => g.Any(w => w.Text == "abordagem"))
            .OrderBy(w => w.BoundingBox.Left)
            .ToList();

        var gaps = line.Zip(line.Skip(1), (a, b) => b.BoundingBox.Left - a.BoundingBox.Right);
        Assert.All(gaps, gap => Assert.InRange(gap, 0, 6));
    }

    [SkippableFact]
    public void References_are_alphabetical()
    {
        using var pdf = Open();
        var lines = Lines(pdf.GetPage(PageWithTitle(pdf, "REFERÊNCIAS")));

        var starts = lines.Where(l => l.StartsWith("BIBLIOTECA") || l.StartsWith("LIMA") || l.StartsWith("SOUZA")).ToList();
        Assert.Equal(["BIBLIOTECA", "LIMA", "SOUZA"], starts.Select(l => l.Split(' ', ',', '.')[0]));
    }

    [SkippableFact]
    public void Chapters_start_at_the_same_height_on_every_page()
    {
        using var pdf = Open();
        // Measure the chapter number: accents and cedillas change the bounding box of the title words.
        var tops = new[] { "1 INTRODUÇÃO", "2 FUNDAMENTAÇÃO TEÓRICA", "3 RESULTADOS", "4 CONCLUSÃO" }
            .Select(t => BodyWords(pdf.GetPage(PageWithTitle(pdf, t))).First(w => w.Text == t[..1]).BoundingBox.Top)
            .ToList();

        Assert.All(tops, top => Assert.InRange(top, tops[0] - 0.5, tops[0] + 0.5));
    }

    [SkippableFact]
    public void Body_text_respects_left_and_right_margins()
    {
        using var pdf = Open();
        var page = pdf.GetPage(PageWithTitle(pdf, "1 INTRODUÇÃO"));
        var words = BodyWords(page).Where(w => w.BoundingBox.Bottom > 4 * Cm).ToList(); // above the footnote

        Assert.InRange(words.Min(w => w.BoundingBox.Left), 3 * Cm - 1.5, 3 * Cm + 1.5);
        Assert.InRange(words.Max(w => w.BoundingBox.Right), page.Width - 2 * Cm - 4, page.Width - 2 * Cm + 1);
        Assert.True(words.Max(w => w.BoundingBox.Top) <= page.Height - 3 * Cm + 1);
    }

    [SkippableFact]
    public void Renders_pre_textual_content_and_citations()
    {
        using var pdf = Open();
        var text = string.Join("\n", pdf.GetPages().SelectMany(Lines));

        Assert.Contains("UNIVERSIDADE FEDERAL EXEMPLO", text);
        Assert.Contains("Orientador: Prof. Dr. João Orientador", text);
        Assert.Contains("Palavras-chave: formatação; documentos estruturados; normalização.", text);
        Assert.Contains("(SOUZA; PEREIRA, 2021)", text);
        Assert.Contains("Segundo Lima (2019, p. 45)", text);
    }

    [SkippableFact]
    public void Uses_the_profile_font_family()
    {
        using var pdf = Open();
        var fonts = BodyWords(pdf.GetPage(PageWithTitle(pdf, "1 INTRODUÇÃO")))
            .SelectMany(w => w.Letters).Select(l => l.FontName).Distinct().ToList();

        // Times New Roman on Windows; its metric-compatible Liberation Serif in the Linux container.
        Assert.All(fonts, f => Assert.Matches("(?i)times|liberationserif", f));
    }
}
