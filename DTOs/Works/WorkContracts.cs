using System.ComponentModel.DataAnnotations;
using Domain.Documents;

namespace DTOs.Works;

/// <summary>Work as shown in the list (without the document body).</summary>
public sealed record WorkSummaryResponse(
    Guid Id,
    string Title,
    string ProfileId,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Full work, including the document model.</summary>
/// <param name="Id">Work id.</param>
/// <param name="Title">Title derived from the document metadata.</param>
/// <param name="ProfileId">Norm profile of the work.</param>
/// <param name="Version">Send it back in <see cref="SaveDocumentRequest.Version"/> to save.</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
/// <param name="UpdatedAt">Last save time (UTC).</param>
/// <param name="Document">The document model (schema v1).</param>
public sealed record WorkResponse(
    Guid Id,
    string Title,
    string ProfileId,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DocumentModel Document);

/// <summary>Creates a work with an empty document for the chosen norm profile.</summary>
public sealed record CreateWorkRequest
{
    /// <summary>Norm profile id (see <c>GET /api/profiles</c>).</summary>
    [Required, StringLength(64, MinimumLength = 1)]
    public required string ProfileId { get; init; }

    /// <summary>Initial title. Defaults to "Trabalho sem título".</summary>
    [StringLength(500)]
    public string? Title { get; init; }
}

/// <summary>Replaces the document of a work (autosave).</summary>
public sealed record SaveDocumentRequest
{
    /// <summary>Version the client edited. A mismatch returns 409 with the current version.</summary>
    [Range(1, int.MaxValue)]
    public required int Version { get; init; }

    public required DocumentModel Document { get; init; }
}

/// <param name="Version">New version to use in the next save.</param>
/// <param name="UpdatedAt">Save time (UTC).</param>
/// <param name="Title">Title derived from the document metadata.</param>
public sealed record SaveDocumentResponse(int Version, DateTimeOffset UpdatedAt, string Title);

/// <summary>Norm profile available for new works.</summary>
public sealed record ProfileResponse(string Id, string Name, string Description, int MaxSectionDepth);
