namespace Domain.Documents;

/// <summary>
/// Source of truth for an academic work (schema v1). Edited by the frontend, stored as JSONB,
/// and consumed by the Exporter. Breaking changes require a new <see cref="SchemaVersion"/>
/// and a migration step (see docs/document-model.md).
/// </summary>
public sealed record DocumentModel
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Version of this JSON schema. Currently always 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Norm profile used to format the work (e.g. <c>abnt-tcc</c>).</summary>
    public required string ProfileId { get; init; }

    /// <summary>Per-work overrides of profile options that the profile allows to change.</summary>
    public required ProfileOverrides ProfileOverrides { get; init; }

    public required DocumentMetadata Metadata { get; init; }
    public required PreTextual PreTextual { get; init; }
    public required Textual Textual { get; init; }
    public required PostTextual PostTextual { get; init; }

    /// <summary>Bibliographic references (CSL-JSON compatible). Citations point to <see cref="CslItem.Id"/>.</summary>
    public required IReadOnlyList<CslItem> References { get; init; }

    /// <summary>Empty document for a new work.</summary>
    public static DocumentModel CreateEmpty(string profileId, string title) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        ProfileId = profileId,
        ProfileOverrides = new ProfileOverrides { FontFamily = null },
        Metadata = new DocumentMetadata
        {
            Institution = "",
            Faculty = null,
            Course = "",
            Authors = [],
            Title = title,
            Subtitle = null,
            Advisor = "",
            CoAdvisor = null,
            City = "",
            Year = null,
            Nature = "",
        },
        PreTextual = new PreTextual
        {
            Cover = new ToggleElement { Enabled = true },
            TitlePage = new ToggleElement { Enabled = true },
            AbstractPt = new AbstractElement { Enabled = true, Text = "", Keywords = [] },
            AbstractEn = new AbstractElement { Enabled = false, Text = "", Keywords = [] },
            Lists = new GeneratedLists { Figures = true, Tables = true },
        },
        Textual = new Textual
        {
            Sections =
            [
                new Section { Id = "sec-introducao", Title = "Introdução", Blocks = [], Children = [] },
            ],
        },
        PostTextual = new PostTextual { Appendices = [], Annexes = [] },
        References = [],
    };
}

/// <summary>Profile options a work may override. Unset values use the profile default.</summary>
public sealed record ProfileOverrides
{
    public required FontFamilyOption? FontFamily { get; init; }
}

public sealed record DocumentMetadata
{
    public required string Institution { get; init; }
    public required string? Faculty { get; init; }
    public required string Course { get; init; }
    public required IReadOnlyList<string> Authors { get; init; }
    public required string Title { get; init; }
    public required string? Subtitle { get; init; }
    public required string Advisor { get; init; }
    public required string? CoAdvisor { get; init; }

    /// <summary>City of the institution (local).</summary>
    public required string City { get; init; }

    /// <summary>Year of deposit/presentation.</summary>
    public required int? Year { get; init; }

    /// <summary>Nature of the work ("Trabalho de Conclusão de Curso apresentado ...").</summary>
    public required string Nature { get; init; }
}

public sealed record PreTextual
{
    public required ToggleElement Cover { get; init; }
    public required ToggleElement TitlePage { get; init; }

    /// <summary>Abstract in the document language (resumo).</summary>
    public required AbstractElement AbstractPt { get; init; }

    /// <summary>Abstract in a foreign language (abstract). Optional.</summary>
    public required AbstractElement AbstractEn { get; init; }

    /// <summary>Which generated lists to include. The table of contents is always generated.</summary>
    public required GeneratedLists Lists { get; init; }
}

public sealed record ToggleElement
{
    public required bool Enabled { get; init; }
}

public sealed record AbstractElement
{
    public required bool Enabled { get; init; }

    /// <summary>Plain text. Paragraph breaks are <c>\n</c>.</summary>
    public required string Text { get; init; }

    public required IReadOnlyList<string> Keywords { get; init; }
}

public sealed record GeneratedLists
{
    public required bool Figures { get; init; }
    public required bool Tables { get; init; }
}

public sealed record Textual
{
    public required IReadOnlyList<Section> Sections { get; init; }
}

/// <summary>Chapter or section. The heading level is the nesting depth (1..5); numbering is generated.</summary>
public sealed record Section
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<Block> Blocks { get; init; }
    public required IReadOnlyList<Section> Children { get; init; }
}

public sealed record PostTextual
{
    public required IReadOnlyList<Attachment> Appendices { get; init; }
    public required IReadOnlyList<Attachment> Annexes { get; init; }
}

/// <summary>Appendix (written by the author) or annex (third-party material).</summary>
public sealed record Attachment
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<Block> Blocks { get; init; }
}
