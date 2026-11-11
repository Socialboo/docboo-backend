using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Exporter.Docx;

/// <summary>Word field helpers (TOC, SEQ, PAGE).</summary>
internal static class Fields
{
    /// <summary>Table of contents from heading styles 1..n and outline levels (post-textual titles).</summary>
    public static string TableOfContents(int maxLevel) => $" TOC \\o \"1-{maxLevel}\" \\h \\z \\u ";

    /// <summary>List built from captions numbered with SEQ fields of the given label.</summary>
    public static string ListOf(string seqLabel) => $" TOC \\h \\z \\c \"{seqLabel}\" ";

    public static SimpleField Sequence(string label, int value) =>
        new(new Run(new Text(value.ToString()))) { Instruction = $" SEQ {label} \\* ARABIC " };

    /// <summary>
    /// Wraps pre-filled entry paragraphs in a complex field spanning them, so Word/LibreOffice treat
    /// them as one generated index that can be updated (and replace the placeholder entries).
    /// </summary>
    public static void WrapInField(IReadOnlyList<Paragraph> paragraphs, string instruction)
    {
        if (paragraphs.Count == 0) return;

        var first = paragraphs[0];
        var insertAfter = (OpenXmlElement?)first.ParagraphProperties;
        foreach (var run in new[]
                 {
                     new Run(new FieldChar { FieldCharType = FieldCharValues.Begin, Dirty = true }),
                     new Run(new FieldCode(instruction) { Space = SpaceProcessingModeValues.Preserve }),
                     new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
                 })
        {
            insertAfter = insertAfter is null ? first.PrependChild(run) : first.InsertAfter(run, insertAfter);
        }

        paragraphs[^1].Append(new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
    }
}
