using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Domain.Assets;
using Domain.Documents;
using Exporter.Profiles;
using Exporter.References;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Exporter.Docx;

/// <summary>Entry recorded while writing, used to pre-fill the table of contents and the lists.</summary>
internal sealed record IndexEntry(int Level, string Text);

/// <summary>
/// Writes block and inline content: paragraphs, long quotes, lists, figures (with images), tables,
/// footnotes and citations. Shared by the textual part, appendices and annexes.
/// </summary>
internal sealed class ContentWriter
{
    private readonly MainDocumentPart _main;
    private readonly NormProfile _profile;
    private readonly NumberingBuilder _numbering;
    private readonly ExportWarnings _warnings;
    private readonly IReadOnlyDictionary<string, ImageData> _images;
    private readonly CitationFormatter _citations;
    private readonly List<Footnote> _footnotes = [];
    private int _figureCount;
    private int _tableCount;
    private uint _drawingId;

    public ContentWriter(MainDocumentPart main, NormProfile profile, NumberingBuilder numbering, ExportWarnings warnings,
        IReadOnlyDictionary<string, ImageData> images, ReferenceCatalog references)
    {
        _main = main;
        _profile = profile;
        _numbering = numbering;
        _warnings = warnings;
        _images = images;
        _citations = new CitationFormatter(references);
    }

    public List<IndexEntry> Figures { get; } = [];
    public List<IndexEntry> Tables { get; } = [];
    public IReadOnlyList<Footnote> Footnotes => _footnotes;

    public void WriteBlocks(List<OpenXmlElement> target, IReadOnlyList<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock p:
                    target.Add(BodyWriter.Para(null, Inlines(p.Content)));
                    break;
                case LongQuoteBlock q:
                    target.Add(BodyWriter.Para(StyleIds.Quote, Inlines(q.Content)));
                    break;
                case ListBlock list:
                    WriteList(target, list);
                    break;
                case FigureBlock figure:
                    WriteFigure(target, figure);
                    break;
                case TableBlock table:
                    WriteTable(target, table);
                    break;
            }
        }
    }

    public OpenXmlElement[] Inlines(IReadOnlyList<Inline> inlines)
    {
        var runs = new List<OpenXmlElement>();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline t:
                    runs.Add(BodyWriter.Text(t.Text, t.Marks));
                    break;
                case HardBreakInline:
                    runs.Add(new Run(new Break()));
                    break;
                case CitationInline c:
                    runs.Add(BodyWriter.Text(_citations.Format(c)));
                    break;
                case FootnoteInline f:
                    runs.Add(FootnoteReference(f));
                    break;
            }
        }
        return [.. runs];
    }

    /// <summary>Plain text (citations formatted, footnotes omitted) for index entries and alt text.</summary>
    public string PlainText(IReadOnlyList<Inline> inlines) => string.Concat(inlines.Select(i => i switch
    {
        TextInline t => t.Text,
        HardBreakInline => " ",
        CitationInline c => _citations.Format(c),
        _ => "",
    })).Trim();

    // ---------- lists ----------

    private void WriteList(List<OpenXmlElement> target, ListBlock list)
    {
        var numId = list.Ordered ? _numbering.NewOrderedList() : _numbering.BulletListId;
        foreach (var item in list.Items)
        {
            var paragraph = BodyWriter.Para(StyleIds.ListParagraph, Inlines(item.Content));
            paragraph.ParagraphProperties!.InsertAfter(
                new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = numId }),
                paragraph.ParagraphProperties.ParagraphStyleId);
            target.Add(paragraph);
        }
    }

    // ---------- captions ----------

    private Paragraph Caption(string label, int number, IReadOnlyList<Inline> title) =>
        BodyWriter.Para(StyleIds.Caption,
            [BodyWriter.Text(label + " "), Fields.Sequence(label, number), BodyWriter.Text(_profile.Captions.Separator), .. Inlines(title)]);

    private Paragraph? Source(IReadOnlyList<Inline> source) =>
        source.Count == 0
            ? null
            : BodyWriter.Para(StyleIds.Source, [BodyWriter.Text(_profile.Captions.SourceLabel + " "), .. Inlines(source)]);

    // ---------- figures ----------

    private void WriteFigure(List<OpenXmlElement> target, FigureBlock figure)
    {
        var number = ++_figureCount;
        var label = _profile.Captions.FigureLabel;
        var title = PlainText(figure.Caption);
        Figures.Add(new IndexEntry(1, $"{label} {number}{_profile.Captions.Separator}{title}"));

        target.Add(Caption(label, number, figure.Caption));
        if (_images.TryGetValue(figure.AssetId, out var image))
        {
            target.Add(BodyWriter.Para(StyleIds.Figure, new Run(Drawing(image, figure.WidthPercent, $"{label} {number}", title))));
        }
        else
        {
            _warnings.Add("imageMissing");
            target.Add(BodyWriter.Para(StyleIds.Figure, BodyWriter.Text("[imagem não encontrada]")));
        }
        if (Source(figure.Source) is { } source) target.Add(source);
    }

    private Drawing Drawing(ImageData image, int widthPercent, string name, string description)
    {
        var page = _profile.Page;
        var textWidth = Units.CmToEmu(page.WidthCm - page.MarginLeftCm - page.MarginRightCm);
        var maxHeight = Units.CmToEmu((page.HeightCm - page.MarginTopCm - page.MarginBottomCm) * 0.75);
        long cx = textWidth * Math.Clamp(widthPercent, 10, 100) / 100;
        long cy = cx * image.Height / image.Width;
        if (cy > maxHeight)
        {
            cx = cx * maxHeight / cy;
            cy = maxHeight;
        }

        var part = _main.AddImagePart(image.ContentType == "image/png" ? ImagePartType.Png : ImagePartType.Jpeg);
        using (var stream = new MemoryStream(image.Content)) part.FeedData(stream);
        var relId = _main.GetIdOfPart(part);
        var id = ++_drawingId;

        return new Drawing(new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = id, Name = name, Description = description },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0, Name = $"image{id}" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = relId }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
        });
    }

    // ---------- tables ----------

    private sealed record Placement(Domain.Documents.TableCell Cell, int Row, int Column);

    private void WriteTable(List<OpenXmlElement> target, TableBlock table)
    {
        var number = ++_tableCount;
        var label = _profile.Captions.TableLabel;
        Tables.Add(new IndexEntry(1, $"{label} {number}{_profile.Captions.Separator}{PlainText(table.Title)}"));
        target.Add(Caption(label, number, table.Title));

        // Lay cells on a grid, honoring row/column spans.
        var rows = table.Rows.Count;
        var occupied = new Dictionary<(int, int), Placement>();
        var columns = 0;
        for (var r = 0; r < rows; r++)
        {
            var c = 0;
            foreach (var cell in table.Rows[r].Cells)
            {
                while (occupied.ContainsKey((r, c))) c++;
                var placement = new Placement(cell, r, c);
                for (var dr = 0; dr < Math.Max(1, cell.RowSpan) && r + dr < rows; dr++)
                    for (var dc = 0; dc < Math.Max(1, cell.ColSpan); dc++)
                        occupied[(r + dr, c + dc)] = placement;
                c += Math.Max(1, cell.ColSpan);
            }
            columns = Math.Max(columns, c);
        }
        columns = Math.Max(columns, occupied.Keys.Select(k => k.Item2 + 1).DefaultIfEmpty(1).Max());

        var page = _profile.Page;
        var textWidth = Units.CmToTwips(page.WidthCm - page.MarginLeftCm - page.MarginRightCm);
        var colWidth = textWidth / columns;
        var lastHeaderRow = table.Rows.Select((row, i) => (row, i)).LastOrDefault(x => x.row.Header).i;
        var hasHeader = table.Rows.Any(r => r.Header);

        // Open sides, horizontal rules at the top, below the header and at the bottom.
        var grid = new TableGrid();
        for (var c = 0; c < columns; c++) grid.Append(new GridColumn { Width = colWidth.ToString() });
        var wTable = new Table(
            new TableProperties(
                new TableWidth { Width = (colWidth * columns).ToString(), Type = TableWidthUnitValues.Dxa },
                new TableJustification { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 8, Color = "000000" },
                    new LeftBorder { Val = BorderValues.Nil },
                    new BottomBorder { Val = BorderValues.Single, Size = 8, Color = "000000" },
                    new RightBorder { Val = BorderValues.Nil },
                    new InsideHorizontalBorder { Val = BorderValues.Nil },
                    new InsideVerticalBorder { Val = BorderValues.Nil }),
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableLook { Val = "0000" }),
            grid);

        for (var r = 0; r < rows; r++)
        {
            var rowModel = table.Rows[r];
            var rowProps = new TableRowProperties(new CantSplit());
            if (rowModel.Header) rowProps.Append(new TableHeader());
            var row = new W.TableRow(rowProps);

            for (var c = 0; c < columns;)
            {
                if (!occupied.TryGetValue((r, c), out var placement))
                {
                    row.Append(Cell(colWidth, 1, null, [], header: false, underline: false));
                    c++;
                    continue;
                }

                var span = Math.Max(1, placement.Cell.ColSpan);
                var startsHere = placement.Row == r;
                var merge = placement.Cell.RowSpan > 1
                    ? new VerticalMerge { Val = startsHere ? MergedCellValues.Restart : null }
                    : null;
                var underline = hasHeader && r + Math.Max(1, placement.Cell.RowSpan) - 1 == lastHeaderRow && placement.Row <= lastHeaderRow;
                row.Append(Cell(colWidth * span, span, merge, startsHere ? placement.Cell.Content : [],
                    header: table.Rows[placement.Row].Header, underline: underline));
                c += span;
            }
            wTable.Append(row);
        }

        target.Add(wTable);
        if (Source(table.Source) is { } source) target.Add(source);
        else target.Add(BodyWriter.Para(StyleIds.Source)); // a table must be followed by a paragraph
    }

    private W.TableCell Cell(int width, int span, VerticalMerge? merge, IReadOnlyList<Inline> content, bool header, bool underline)
    {
        var props = new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
        if (span > 1) props.Append(new GridSpan { Val = span });
        if (merge is not null) props.Append(merge);
        if (underline) props.Append(new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "000000" }));
        props.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        return new W.TableCell(props, BodyWriter.Para(header ? StyleIds.TableHeader : StyleIds.TableText, Inlines(content)));
    }

    // ---------- footnotes ----------

    private Run FootnoteReference(FootnoteInline footnote)
    {
        var id = _footnotes.Count + 1;
        var paragraph = BodyWriter.Para(StyleIds.FootnoteText,
            [
                new Run(new RunProperties(new RunStyle { Val = StyleIds.FootnoteReference }), new FootnoteReferenceMark()),
                BodyWriter.Text(" "),
                .. Inlines(footnote.Content),
            ]);
        _footnotes.Add(new Footnote(paragraph) { Id = id });
        return new Run(new RunProperties(new RunStyle { Val = StyleIds.FootnoteReference }), new W.FootnoteReference { Id = id });
    }
}
