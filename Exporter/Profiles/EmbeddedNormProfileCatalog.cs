using System.Reflection;
using Domain.Profiles;

namespace Exporter.Profiles;

/// <summary>Full norm profiles, including formatting rules (used by the generators).</summary>
public interface INormProfileStore
{
    NormProfile? Find(string id);

    NormProfile GetRequired(string id);
}

/// <summary>Loads norm profiles shipped as embedded JSON resources (Exporter/Profiles/*.json).</summary>
public sealed class EmbeddedNormProfileCatalog : INormProfileCatalog, INormProfileStore
{
    private readonly IReadOnlyList<NormProfile> _profiles;

    public EmbeddedNormProfileCatalog()
    {
        var assembly = typeof(EmbeddedNormProfileCatalog).Assembly;
        _profiles = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Exporter.Profiles.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => NormProfileLoader.Parse(ReadResource(assembly, name)))
            .ToList();

        var duplicate = _profiles.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidNormProfileException($"Duplicate norm profile id '{duplicate.Key}'.");
    }

    public IReadOnlyList<NormProfileInfo> List() => _profiles.Select(p => p.ToInfo()).ToList();

    NormProfileInfo? INormProfileCatalog.Find(string id) => Find(id)?.ToInfo();

    public NormProfile? Find(string id) => _profiles.FirstOrDefault(p => p.Id == id);

    public NormProfile GetRequired(string id) =>
        Find(id) ?? throw new KeyNotFoundException($"Norm profile '{id}' not found.");

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing resource {resourceName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
