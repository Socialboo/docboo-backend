using Domain.Documents;

namespace Domain.Works;

/// <summary>An academic work owned by a user. Its content is the versioned <see cref="DocumentModel"/>.</summary>
public sealed class Work
{
    public const int MaxTitleLength = 500;
    public const string UntitledTitle = "Trabalho sem título";

    private Work() { } // EF Core

    public Guid Id { get; private set; }

    /// <summary>Owner. No authentication in the MVP: a single local user (see ICurrentUser).</summary>
    public Guid UserId { get; private set; }

    /// <summary>Denormalized from <c>Document.Metadata.Title</c> for listing.</summary>
    public string Title { get; private set; } = "";

    public string ProfileId { get; private set; } = "";

    public DocumentModel Document { get; private set; } = null!;

    /// <summary>Optimistic concurrency version; incremented on every document save.</summary>
    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Work Create(Guid userId, string profileId, string? title, DateTimeOffset now)
    {
        var effectiveTitle = NormalizeTitle(title);
        return new Work
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            ProfileId = profileId,
            Title = effectiveTitle,
            Document = DocumentModel.CreateEmpty(profileId, effectiveTitle),
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Replaces the document. The caller must have validated it against this work.</summary>
    public void ReplaceDocument(DocumentModel document, DateTimeOffset now)
    {
        if (document.ProfileId != ProfileId)
            throw new InvalidOperationException("The document profile does not match the work profile.");

        Document = document;
        Title = NormalizeTitle(document.Metadata.Title);
        Version++;
        UpdatedAt = now;
    }

    private static string NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return UntitledTitle;
        return trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..MaxTitleLength];
    }
}
