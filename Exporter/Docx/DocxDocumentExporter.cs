using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Domain.Assets;
using Domain.Documents;
using Exporter.Profiles;

namespace Exporter.Docx;

/// <param name="Content">The .docx file.</param>
/// <param name="Warnings">Content that could not be rendered yet (user-facing, pt-BR).</param>
public sealed record DocxExportResult(byte[] Content, IReadOnlyList<string> Warnings);

/// <summary>
/// Generates a .docx from the document model with the Open XML SDK. This file is the single source of
/// truth for both outputs: the PDF is produced by converting it (see <c>LibreOfficeConverter</c>).
/// </summary>
public sealed class DocxDocumentExporter(INormProfileStore profiles)
{
    public DocxExportResult Export(DocumentModel document, IReadOnlyDictionary<string, ImageData>? images = null)
    {
        images ??= new Dictionary<string, ImageData>();
        var profile = profiles.GetRequired(document.ProfileId);
        var fontFamily = profile.ResolveFontFamily(document.ProfileOverrides);
        var warnings = new ExportWarnings();

        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = StyleSheetBuilder.Build(profile, fontFamily);
            main.AddNewPart<DocumentSettingsPart>().Settings = Settings(profile);

            var pageNumberReference = PageNumberPart(main, profile);
            var numbering = new NumberingBuilder(profile);
            var writer = new BodyWriter(main, document, profile, numbering, warnings, images);
            main.Document = new Document(writer.Write(pageNumberReference));
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = numbering.Build();
            main.AddNewPart<FootnotesPart>().Footnotes = Footnotes(writer.Footnotes);

            package.PackageProperties.Title = document.Metadata.Title;
            package.PackageProperties.Creator = string.Join("; ", document.Metadata.Authors);
            package.PackageProperties.Language = profile.Language;
            package.PackageProperties.Created = DateTime.UtcNow;
        }

        return new DocxExportResult(stream.ToArray(), warnings.ToMessages());
    }

    /// <summary>Header (or footer) holding only the PAGE field, positioned by the profile.</summary>
    private static Func<OpenXmlElement> PageNumberPart(MainDocumentPart main, NormProfile profile)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId
            {
                Val = profile.PageNumbering.Position == PageNumberPosition.TopRight ? StyleIds.Header : StyleIds.Footer,
            }),
            new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " });

        if (profile.PageNumbering.Position == PageNumberPosition.TopRight)
        {
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new Header(paragraph);
            var id = main.GetIdOfPart(header);
            return () => new HeaderReference { Type = HeaderFooterValues.Default, Id = id };
        }

        var footer = main.AddNewPart<FooterPart>();
        footer.Footer = new Footer(paragraph);
        var footerId = main.GetIdOfPart(footer);
        return () => new FooterReference { Type = HeaderFooterValues.Default, Id = footerId };
    }

    /// <summary>footnotes.xml: the two required separator footnotes (ids -1 and 0) followed by the content.</summary>
    private static Footnotes Footnotes(IReadOnlyList<Footnote> footnotes)
    {
        var part = new Footnotes(
            new Footnote(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }),
                new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
            new Footnote(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }),
                new Run(new ContinuationSeparatorMark()))) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 });
        foreach (var footnote in footnotes) part.Append(footnote);
        return part;
    }

    private static Settings Settings(NormProfile profile) => new(
        new Zoom { Percent = "100" },
        new DefaultTabStop { Val = (short)Units.CmToTwips(1.25) },
        new CharacterSpacingControl { Val = CharacterSpacingValues.DoNotCompress },
        // Word refreshes the table of contents and lists (page numbers) when the file is opened.
        new UpdateFieldsOnOpen { Val = true },
        new FootnoteDocumentWideProperties(new FootnoteSpecialReference { Id = -1 }, new FootnoteSpecialReference { Id = 0 }),
        // Word 2013+ layout; avoids "compatibility mode" when the student opens the file.
        new Compatibility(
            // Justified lines ending in a manual line break are not stretched word by word.
            new DoNotExpandShiftReturn(),
            new CompatibilitySetting
        {
            Name = CompatSettingNameValues.CompatibilityMode,
            Uri = "http://schemas.microsoft.com/office/word",
            Val = "15",
        }),
        new ThemeFontLanguages { Val = profile.Language });
}
