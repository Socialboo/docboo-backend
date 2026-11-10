namespace Domain.Profiles;

/// <summary>Summary of a formatting norm profile (ABNT, institution variants, APA, ...).</summary>
/// <param name="Id">Stable identifier stored in each work (e.g. <c>abnt-tcc</c>).</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Short description for the profile picker.</param>
/// <param name="MaxSectionDepth">Deepest heading level the profile supports.</param>
public sealed record NormProfileInfo(string Id, string Name, string Description, int MaxSectionDepth);

/// <summary>Available norm profiles. The formatting rules themselves live with the Exporter.</summary>
public interface INormProfileCatalog
{
    IReadOnlyList<NormProfileInfo> List();

    NormProfileInfo? Find(string id);
}
