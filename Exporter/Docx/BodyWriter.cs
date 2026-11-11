using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Domain.Documents;
using Exporter.Profiles;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Exporter.Docx;

/// <summary>
/// Writes document.xml body: one Word section per part of the work, so page numbering can be
/// configured per part (cover not counted, number hidden on pre-textual pages, visible from the
/// introduction on, as defined by the profile).
/// </summary>
internal sealed class BodyWriter
{
    private enum Part { Cover, TitlePage, PreTextual, Textual }

    private sealed class Section(Part part)
    {
        public Part Part { get; } = part;
        public List<OpenXmlElement> Content { get; } = [];

        /// <summary>Cover/title page: a full-page layout table that must be closed by a tiny paragraph.</summary>
        public bool FullPage { get; init; }
    }

    private readonly DocumentModel _doc;
    private readonly NormProfile _profile;
    private readonly NumberingBuilder _numbering;
    private readonly ExportWarnings _warnings;

    public BodyWriter(DocumentModel doc, NormProfile profile, NumberingBuilder numbering, ExportWarnings warnings)
    {
        _doc = doc;
        _profile = profile;
        _numbering = numbering;
        _warnings = warnings;
    }

    /// <param name="pageNumberReference">Header or footer reference that prints the page number.</param>
    public Body Write(Func<OpenXmlElement> pageNumberReference)
    {
        var sections = new List<Section>();
        var pre = _doc.PreTextual;

        if (pre.Cover.Enabled)
            sections.Add(Cover());
        if (pre.TitlePage.Enabled)
            sections.Add(TitlePage());
        if (pre.AbstractPt.Enabled || pre.AbstractEn.Enabled)
            sections.Add(Abstracts());
        sections.Add(Textual());

        _warnings.Add("toc");
        if (pre.Lists.Figures || pre.Lists.Tables) _warnings.Add("lists");
        if (_doc.References.Count > 0) _warnings.Add("references");
        foreach (var _ in _doc.PostTextual.Appendices) _warnings.Add("appendices");
        foreach (var _ in _doc.PostTextual.Annexes) _warnings.Add("annexes");

        var countStart = CountStartIndex(sections);
        var body = new Body();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var showNumber = _profile.PageNumbering.VisibleFrom switch
            {
                PageNumberVisibleFrom.Textual => section.Part == Part.Textual,
                _ => i >= countStart,
            };
            var sectPr = SectionProperties(restartNumbering: i == countStart, showNumber ? pageNumberReference() : null);
            var isLast = i == sections.Count - 1;

            var content = section.Content;
            if (section.FullPage || content.Count == 0 || content[^1] is not Paragraph)
                content.Add(Para(StyleIds.SectionEnd));

            foreach (var element in content) body.Append(element);

            if (isLast)
            {
                body.Append(sectPr);
            }
            else
            {
                var last = (Paragraph)content[^1];
                (last.ParagraphProperties ??= new ParagraphProperties()).Append(sectPr);
            }
        }
        return body;
    }

    private int CountStartIndex(List<Section> sections)
    {
        if (_profile.PageNumbering.CountFrom == PageCountStart.Cover && sections[0].Part == Part.Cover)
            return 0;
        return sections.FindIndex(s => s.Part != Part.Cover);
    }

    // ---------- pre-textual ----------

    private Section Cover()
    {
        var m = _doc.Metadata;
        var top = new List<OpenXmlElement>();
        AddLine(top, m.Institution, bold: true, caps: true);
        AddLine(top, m.Faculty, caps: true);
        AddLine(top, m.Course, caps: true);
        if (m.Authors.Count > 0) top.Add(Para(StyleIds.CoverText));
        foreach (var author in m.Authors) AddLine(top, author, caps: true);

        var section = new Section(Part.Cover) { FullPage = true };
        section.Content.Add(FullPageTable(
            (0.35, TableVerticalAlignmentValues.Top, top),
            (0.35, TableVerticalAlignmentValues.Center, [TitleParagraph()]),
            (0.30, TableVerticalAlignmentValues.Bottom, CityAndYear())));
        return section;
    }

    private Section TitlePage()
    {
        var m = _doc.Metadata;
        var p = _profile.PreTextual;
        var authors = new List<OpenXmlElement>();
        foreach (var author in m.Authors) AddLine(authors, author, caps: true);

        var nature = new List<OpenXmlElement>();
        if (!string.IsNullOrWhiteSpace(m.Nature)) nature.Add(Para(StyleIds.NatureOfWork, Text(m.Nature)));
        if (!string.IsNullOrWhiteSpace(m.Advisor)) nature.Add(Para(StyleIds.NatureOfWork, Text($"{p.AdvisorLabel} {m.Advisor}")));
        if (!string.IsNullOrWhiteSpace(m.CoAdvisor)) nature.Add(Para(StyleIds.NatureOfWork, Text($"{p.CoAdvisorLabel} {m.CoAdvisor}")));

        var section = new Section(Part.TitlePage) { FullPage = true };
        section.Content.Add(FullPageTable(
            (0.15, TableVerticalAlignmentValues.Top, authors),
            (0.25, TableVerticalAlignmentValues.Center, [TitleParagraph()]),
            (0.35, TableVerticalAlignmentValues.Center, nature),
            (0.25, TableVerticalAlignmentValues.Bottom, CityAndYear())));
        return section;
    }

    private Section Abstracts()
    {
        var section = new Section(Part.PreTextual);
        var rules = _profile.PreTextual;
        var first = true;
        foreach (var (element, title, label) in new[]
                 {
                     (_doc.PreTextual.AbstractPt, rules.AbstractTitlePt, rules.KeywordsLabelPt),
                     (_doc.PreTextual.AbstractEn, rules.AbstractTitleForeign, rules.KeywordsLabelForeign),
                 })
        {
            if (!element.Enabled) continue;

            var titleParagraph = Para(StyleIds.PreTextualTitle, Text(title));
            if (!first) titleParagraph.ParagraphProperties!.InsertAfter(new PageBreakBefore(), titleParagraph.ParagraphProperties.ParagraphStyleId);
            section.Content.Add(titleParagraph);
            first = false;

            foreach (var line in element.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                section.Content.Add(Para(StyleIds.AbstractText, Text(line)));

            if (element.Keywords.Count > 0)
            {
                var keywords = Para(StyleIds.AbstractText,
                    Text(label, bold: true),
                    Text(" " + string.Join("; ", element.Keywords) + "."));
                keywords.ParagraphProperties!.Append(new SpacingBetweenLines
                {
                    Before = Units.PtToTwips(_profile.Body.FontSizePt * _profile.Body.LineSpacing).ToString(),
                });
                section.Content.Add(keywords);
            }
        }
        return section;
    }

    private Paragraph TitleParagraph()
    {
        var m = _doc.Metadata;
        var runs = new List<OpenXmlElement> { Text(m.Title, bold: true, caps: true) };
        if (!string.IsNullOrWhiteSpace(m.Subtitle)) runs.Add(Text($": {m.Subtitle}"));
        return Para(StyleIds.CoverText, [.. runs]);
    }

    private List<OpenXmlElement> CityAndYear()
    {
        var lines = new List<OpenXmlElement>();
        AddLine(lines, _doc.Metadata.City);
        AddLine(lines, _doc.Metadata.Year?.ToString());
        return lines;
    }

    private static void AddLine(List<OpenXmlElement> target, string? text, bool bold = false, bool caps = false)
    {
        if (!string.IsNullOrWhiteSpace(text)) target.Add(Para(StyleIds.CoverText, Text(text, bold: bold, caps: caps)));
    }

    /// <summary>
    /// Borderless one-column table filling the text area, used to place blocks at the top, middle and
    /// bottom of the cover/title page identically in Word and LibreOffice.
    /// </summary>
    private Table FullPageTable(params (double Fraction, TableVerticalAlignmentValues Align, List<OpenXmlElement> Content)[] rows)
    {
        var page = _profile.Page;
        var width = Units.CmToTwips(page.WidthCm - page.MarginLeftCm - page.MarginRightCm);
        // Leave room for the closing paragraph so the table never pushes it onto a blank page.
        var height = Units.CmToTwips(page.HeightCm - page.MarginTopCm - page.MarginBottomCm - 1.5);

        var table = new Table(
            new TableProperties(
                new TableWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableCellMarginDefault(
                    new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 0, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 0, Type = TableWidthValues.Dxa }),
                new TableLook { Val = "0000" }),
            new TableGrid(new GridColumn { Width = width.ToString() }));

        foreach (var (fraction, align, content) in rows)
        {
            var cell = new W.TableCell(new TableCellProperties(
                new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = align }));
            if (content.Count == 0) content.Add(Para(StyleIds.CoverText));
            foreach (var element in content) cell.Append(element);

            table.Append(new W.TableRow(
                new TableRowProperties(new TableRowHeight { Val = (uint)(height * fraction), HeightType = HeightRuleValues.AtLeast }),
                cell));
        }
        return table;
    }

    // ---------- textual ----------

    private Section Textual()
    {
        var section = new Section(Part.Textual);
        foreach (var s in _doc.Textual.Sections) WriteSection(section.Content, s, depth: 1);
        return section;
    }

    private void WriteSection(List<OpenXmlElement> target, Domain.Documents.Section section, int depth)
    {
        target.Add(Para(StyleIds.Heading(depth), Text(section.Title)));
        WriteBlocks(target, section.Blocks);
        foreach (var child in section.Children) WriteSection(target, child, depth + 1);
    }

    private void WriteBlocks(List<OpenXmlElement> target, IReadOnlyList<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock p:
                    target.Add(Para(null, Inlines(p.Content)));
                    break;
                case LongQuoteBlock q:
                    target.Add(Para(StyleIds.Quote, Inlines(q.Content)));
                    break;
                case ListBlock list:
                    var numId = list.Ordered ? _numbering.NewOrderedList() : _numbering.BulletListId;
                    foreach (var item in list.Items)
                    {
                        var paragraph = Para(StyleIds.ListParagraph, Inlines(item.Content));
                        paragraph.ParagraphProperties!.InsertAfter(
                            new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = numId }),
                            paragraph.ParagraphProperties.ParagraphStyleId);
                        target.Add(paragraph);
                    }
                    break;
                case FigureBlock:
                    _warnings.Add("figure");
                    break;
                case TableBlock:
                    _warnings.Add("table");
                    break;
            }
        }
    }

    private OpenXmlElement[] Inlines(IReadOnlyList<Inline> inlines)
    {
        var runs = new List<OpenXmlElement>();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline t:
                    runs.Add(Text(t.Text, t.Marks));
                    break;
                case HardBreakInline:
                    runs.Add(new Run(new Break()));
                    break;
                case CitationInline c:
                    _warnings.Add("citation");
                    var locator = string.IsNullOrEmpty(c.Locator) ? "" : $", {c.Locator}";
                    runs.Add(Text($"({string.Join("; ", c.RefIds)}{locator})"));
                    break;
                case FootnoteInline:
                    _warnings.Add("footnote");
                    break;
            }
        }
        return [.. runs];
    }

    // ---------- primitives ----------

    private SectionProperties SectionProperties(bool restartNumbering, OpenXmlElement? pageNumberReference)
    {
        var page = _profile.Page;
        var sectPr = new SectionProperties();
        if (pageNumberReference is not null) sectPr.Append(pageNumberReference);
        sectPr.Append(new SectionType { Val = SectionMarkValues.NextPage });
        sectPr.Append(new PageSize
        {
            Width = (uint)Units.CmToTwips(page.WidthCm),
            Height = (uint)Units.CmToTwips(page.HeightCm),
        });
        sectPr.Append(new PageMargin
        {
            Top = Units.CmToTwips(page.MarginTopCm),
            Right = (uint)Units.CmToTwips(page.MarginRightCm),
            Bottom = Units.CmToTwips(page.MarginBottomCm),
            Left = (uint)Units.CmToTwips(page.MarginLeftCm),
            Header = (uint)Units.CmToTwips(page.HeaderDistanceCm),
            Footer = (uint)Units.CmToTwips(page.FooterDistanceCm),
            Gutter = 0,
        });
        if (restartNumbering) sectPr.Append(new PageNumberType { Start = 1 });
        return sectPr;
    }

    private static Paragraph Para(string? styleId, params OpenXmlElement[] runs)
    {
        var paragraph = new Paragraph();
        if (styleId is not null) paragraph.Append(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        else paragraph.Append(new ParagraphProperties());
        foreach (var run in runs) paragraph.Append(run);
        return paragraph;
    }

    private static Run Text(string text, IReadOnlyList<TextMark> marks)
    {
        var rPr = new RunProperties();
        if (marks.Contains(TextMark.Bold)) rPr.Append(new Bold(), new BoldComplexScript());
        if (marks.Contains(TextMark.Italic)) rPr.Append(new Italic(), new ItalicComplexScript());
        if (marks.Contains(TextMark.Underline)) rPr.Append(new Underline { Val = UnderlineValues.Single });
        if (marks.Contains(TextMark.Superscript)) rPr.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript });
        else if (marks.Contains(TextMark.Subscript)) rPr.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Subscript });

        var run = new Run();
        if (rPr.HasChildren) run.Append(rPr);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static Run Text(string text, bool bold = false, bool caps = false)
    {
        var run = new Run();
        if (bold || caps)
        {
            var rPr = new RunProperties();
            if (bold) rPr.Append(new Bold(), new BoldComplexScript());
            if (caps) rPr.Append(new Caps());
            run.Append(rPr);
        }
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }
}
