using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Exporter.Profiles;
using TextAlignment = Exporter.Profiles.TextAlignment;

namespace Exporter.Docx;

/// <summary>Builds styles.xml from a norm profile. All formatting lives in styles, not in direct formatting.</summary>
internal static class StyleSheetBuilder
{
    public const int HeadingNumberingId = 1;

    public static Styles Build(NormProfile profile, string fontFamily)
    {
        var styles = new Styles();
        styles.Append(DocDefaults(profile, fontFamily));

        styles.Append(ParagraphStyle(StyleIds.Normal, "Normal", basedOn: null, isDefault: true,
            Paragraph(profile.Body.Alignment, profile.Body.LineSpacing, firstLineCm: profile.Body.FirstLineIndentCm),
            Run(profile.Body.FontSizePt)));

        foreach (var level in profile.Headings.Levels)
            styles.Append(HeadingStyle(profile, level));

        styles.Append(ParagraphStyle(StyleIds.Quote, "Quote", StyleIds.Normal, isDefault: false,
            Paragraph(TextAlignment.Justify, profile.LongQuote.LineSpacing, firstLineCm: 0, leftCm: profile.LongQuote.LeftIndentCm,
                spaceBeforePt: profile.Body.FontSizePt * profile.Body.LineSpacing,
                spaceAfterPt: profile.Body.FontSizePt * profile.Body.LineSpacing),
            Run(profile.LongQuote.FontSizePt)));

        styles.Append(ParagraphStyle(StyleIds.ListParagraph, "List Paragraph", StyleIds.Normal, isDefault: false,
            Paragraph(profile.Body.Alignment, profile.Body.LineSpacing, firstLineCm: 0), null));

        styles.Append(ParagraphStyle(StyleIds.PreTextualTitle, "Docboo Pre-textual Title", StyleIds.Normal, isDefault: false,
            Paragraph(TextAlignment.Center, profile.Body.LineSpacing, firstLineCm: 0,
                spaceAfterPt: profile.Headings.SpaceAfterPt, keepNext: true),
            Run(null, bold: true, allCaps: true)));

        styles.Append(ParagraphStyle(StyleIds.CoverText, "Docboo Cover Text", StyleIds.Normal, isDefault: false,
            Paragraph(TextAlignment.Center, profile.Body.LineSpacing, firstLineCm: 0), null));

        styles.Append(ParagraphStyle(StyleIds.NatureOfWork, "Docboo Nature of Work", StyleIds.Normal, isDefault: false,
            Paragraph(TextAlignment.Justify, profile.SmallText.LineSpacing, firstLineCm: 0, leftCm: profile.PreTextual.NatureIndentCm,
                spaceAfterPt: profile.SmallText.FontSizePt),
            Run(profile.SmallText.FontSizePt)));

        styles.Append(ParagraphStyle(StyleIds.AbstractText, "Docboo Abstract Text", StyleIds.Normal, isDefault: false,
            Paragraph(TextAlignment.Justify, profile.Body.LineSpacing, firstLineCm: 0), null));

        styles.Append(ParagraphStyle(StyleIds.SectionEnd, "Docboo Section End", StyleIds.Normal, isDefault: false,
            Paragraph(TextAlignment.Left, 1.0, firstLineCm: 0), Run(1)));

        var pageNumberAlignment = profile.PageNumbering.Position == PageNumberPosition.TopRight ? TextAlignment.Right : TextAlignment.Center;
        styles.Append(ParagraphStyle(StyleIds.Header, "header", StyleIds.Normal, isDefault: false,
            Paragraph(pageNumberAlignment, 1.0, firstLineCm: 0), Run(profile.PageNumbering.FontSizePt)));
        styles.Append(ParagraphStyle(StyleIds.Footer, "footer", StyleIds.Normal, isDefault: false,
            Paragraph(pageNumberAlignment, 1.0, firstLineCm: 0), Run(profile.PageNumbering.FontSizePt)));

        return styles;
    }

    private static DocDefaults DocDefaults(NormProfile profile, string fontFamily) =>
        new(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = fontFamily, HighAnsi = fontFamily, ComplexScript = fontFamily, EastAsia = fontFamily },
                new FontSize { Val = Units.PtToHalfPoints(profile.Body.FontSizePt).ToString() },
                new FontSizeComplexScript { Val = Units.PtToHalfPoints(profile.Body.FontSizePt).ToString() },
                new Languages { Val = profile.Language, EastAsia = profile.Language, Bidi = profile.Language })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto })));

    private static Style HeadingStyle(NormProfile profile, HeadingLevelRules level)
    {
        // A heading that always opens a page needs no space before it; engines disagree on whether that space
        // is kept at the top of a page (LibreOffice keeps it after a section break, drops it after a page break).
        var pPr = Paragraph(TextAlignment.Left, profile.Body.LineSpacing, firstLineCm: 0,
            spaceBeforePt: level.PageBreakBefore ? 0 : profile.Headings.SpaceBeforePt, spaceAfterPt: profile.Headings.SpaceAfterPt,
            keepNext: true, pageBreakBefore: level.PageBreakBefore);
        pPr.Append(new OutlineLevel { Val = level.Level - 1 });
        if (profile.Headings.Numbered)
        {
            // Order matters in CT_PPrBase: numPr comes right after pageBreakBefore/keep*.
            pPr.InsertAfter(new NumberingProperties(
                new NumberingLevelReference { Val = level.Level - 1 },
                new NumberingId { Val = HeadingNumberingId }), pPr.Elements<OpenXmlElement>()
                .LastOrDefault(e => e is KeepNext or KeepLines or PageBreakBefore));
        }

        return ParagraphStyle(StyleIds.Heading(level.Level), $"heading {level.Level}", StyleIds.Normal, isDefault: false,
            pPr, Run(null, bold: level.Bold, italic: level.Italic, allCaps: level.AllCaps));
    }

    private static Style ParagraphStyle(string id, string name, string? basedOn, bool isDefault,
        StyleParagraphProperties? pPr, StyleRunProperties? rPr)
    {
        var style = new Style { Type = StyleValues.Paragraph, StyleId = id, Default = isDefault ? true : null };
        style.Append(new StyleName { Val = name });
        if (basedOn is not null) style.Append(new BasedOn { Val = basedOn });
        if (id.StartsWith("Heading", StringComparison.Ordinal) || id == StyleIds.PreTextualTitle)
            style.Append(new NextParagraphStyle { Val = StyleIds.Normal });
        style.Append(new PrimaryStyle());
        if (pPr is not null) style.Append(pPr);
        if (rPr is not null) style.Append(rPr);
        return style;
    }

    private static StyleParagraphProperties Paragraph(TextAlignment alignment, double lineSpacing, double firstLineCm,
        double leftCm = 0, double spaceBeforePt = 0, double spaceAfterPt = 0, bool keepNext = false, bool pageBreakBefore = false)
    {
        var pPr = new StyleParagraphProperties();
        if (keepNext)
        {
            pPr.Append(new KeepNext());
            pPr.Append(new KeepLines());
        }
        if (pageBreakBefore) pPr.Append(new PageBreakBefore());
        pPr.Append(new SpacingBetweenLines
        {
            Before = Units.PtToTwips(spaceBeforePt).ToString(),
            After = Units.PtToTwips(spaceAfterPt).ToString(),
            Line = Units.LineSpacingToAutoLine(lineSpacing).ToString(),
            LineRule = LineSpacingRuleValues.Auto,
        });
        pPr.Append(new Indentation
        {
            Left = Units.CmToTwips(leftCm).ToString(),
            FirstLine = Units.CmToTwips(firstLineCm).ToString(),
        });
        pPr.Append(new Justification { Val = Justify(alignment) });
        return pPr;
    }

    private static StyleRunProperties Run(double? sizePt, bool bold = false, bool italic = false, bool allCaps = false)
    {
        var rPr = new StyleRunProperties();
        if (bold) rPr.Append(new Bold(), new BoldComplexScript());
        if (italic) rPr.Append(new Italic(), new ItalicComplexScript());
        if (allCaps) rPr.Append(new Caps());
        if (sizePt is { } pt)
        {
            rPr.Append(new FontSize { Val = Units.PtToHalfPoints(pt).ToString() });
            rPr.Append(new FontSizeComplexScript { Val = Units.PtToHalfPoints(pt).ToString() });
        }
        return rPr;
    }

    public static JustificationValues Justify(TextAlignment alignment) => alignment switch
    {
        TextAlignment.Left => JustificationValues.Left,
        TextAlignment.Center => JustificationValues.Center,
        TextAlignment.Right => JustificationValues.Right,
        _ => JustificationValues.Both,
    };
}
