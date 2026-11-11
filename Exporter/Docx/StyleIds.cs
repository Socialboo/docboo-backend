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

    /// <summary>"Figura 1 – ..." / "Tabela 1 – ..." above figures and tables (built-in "caption").</summary>
    public const string Caption = "Caption";

    /// <summary>"Fonte: ..." below figures and tables.</summary>
    public const string Source = "DocbooSource";

    /// <summary>Paragraph holding a figure image.</summary>
    public const string Figure = "DocbooFigure";

    public const string TableText = "DocbooTableText";
    public const string TableHeader = "DocbooTableHeader";

    public const string FootnoteText = "FootnoteText";

    /// <summary>Character style of the footnote mark (superscript).</summary>
    public const string FootnoteReference = "FootnoteReference";

    /// <summary>Entries of the lists of figures/tables (built-in "table of figures").</summary>
    public const string TableOfFigures = "TableofFigures";

    /// <summary>Unnumbered post-textual titles (REFERÊNCIAS, APÊNDICE A – ...). Included in the table of contents.</summary>
    public const string PostTextualTitle = "DocbooPostTextualTitle";

    public const string Bibliography = "Bibliography";

    public static string Heading(int level) => $"Heading{level}";

    /// <summary>Table of contents entry of a level (built-in "toc N").</summary>
    public static string Toc(int level) => $"TOC{level}";
}
