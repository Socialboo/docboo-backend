namespace Exporter.Docx;

/// <summary>
/// Paragraph and character styles written to styles.xml. Built-in Word styles keep their built-in
/// names (e.g. "heading 1") so Word recognizes them and the student can restyle the document later.
/// </summary>
public static class StyleIds
{
    public const string Normal = "Normal";
    public const string Quote = "Quote";
    public const string Header = "Header";
    public const string Footer = "Footer";
    public const string ListParagraph = "ListParagraph";

    /// <summary>Unnumbered titles of pre-textual elements (RESUMO, ABSTRACT, ...).</summary>
    public const string PreTextualTitle = "DocbooPreTextualTitle";

    /// <summary>Centered text on the cover and title page.</summary>
    public const string CoverText = "DocbooCoverText";

    /// <summary>"Nature of the work" block on the title page.</summary>
    public const string NatureOfWork = "DocbooNatureOfWork";

    /// <summary>Abstract paragraphs (no first-line indent).</summary>
    public const string AbstractText = "DocbooAbstractText";

    /// <summary>Tiny paragraph that closes a full-page section (cover, title page) without spilling.</summary>
    public const string SectionEnd = "DocbooSectionEnd";

    public static string Heading(int level) => $"Heading{level}";
}
