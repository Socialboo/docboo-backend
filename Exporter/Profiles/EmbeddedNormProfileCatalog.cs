using System.Reflection;
using System.Text.Json;
using Domain.Profiles;

namespace Exporter.Profiles;

/// <summary>Loads norm profiles shipped as embedded JSON resources (Exporter/Profiles/*.json).</summary>
public sealed class EmbeddedNormProfileCatalog : INormProfileCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly IReadOnlyList<NormProfileInfo> _profiles;

    public EmbeddedNormProfileCatalog()
    {
        var assembly = typeof(EmbeddedNormProfileCatalog).Assembly;
        _profiles = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Exporter.Profiles.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => Load(assembly, name))
            .ToList();

        var duplicate = _profiles.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate norm profile id '{duplicate.Key}'.");
    }

    public IReadOnlyList<NormProfileInfo> List() => _profiles;

    public NormProfileInfo? Find(string id) => _profiles.FirstOrDefault(p => p.Id == id);

    private static NormProfileInfo Load(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing resource {resourceName}.");
        var profile = JsonSerializer.Deserialize<NormProfileInfo>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Empty norm profile {resourceName}.");
        if (string.IsNullOrWhiteSpace(profile.Id) || profile.MaxSectionDepth is < 1 or > 9)
            throw new InvalidOperationException($"Invalid norm profile {resourceName}.");
        return profile;
    }
}
