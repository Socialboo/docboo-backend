namespace Application.Documents;

/// <summary>Size limits for stored documents (configuration section <c>DocumentLimits</c>).</summary>
public sealed class DocumentLimits
{
    public const string SectionName = "DocumentLimits";

    /// <summary>Hard cap for the request body of a document save, enforced by the HTTP layer.</summary>
    public const int MaxRequestBytes = 4 * 1024 * 1024;

    /// <summary>Serialized (UTF-8 JSON) size of a document after sanitization.</summary>
    public int MaxDocumentBytes { get; set; } = 2 * 1024 * 1024;

    public int MaxIdLength { get; set; } = 64;
    public int MaxShortTextLength { get; set; } = 500;
    public int MaxTextInlineLength { get; set; } = 20_000;
    public int MaxAbstractLength { get; set; } = 5_000;
    public int MaxKeywords { get; set; } = 10;
    public int MaxKeywordLength { get; set; } = 100;
    public int MaxAuthors { get; set; } = 10;
    public int MaxBlocks { get; set; } = 20_000;
    public int MaxReferences { get; set; } = 2_000;
    public int MaxTableRows { get; set; } = 1_000;
    public int MaxTableColumns { get; set; } = 30;
}
