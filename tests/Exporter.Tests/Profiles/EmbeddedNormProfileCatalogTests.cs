using Exporter.Profiles;

namespace Exporter.Tests.Profiles;

public class EmbeddedNormProfileCatalogTests
{
    [Fact]
    public void Loads_abnt_profile_from_embedded_resources()
    {
        var catalog = new EmbeddedNormProfileCatalog();

        var abnt = catalog.Find("abnt-tcc");

        Assert.NotNull(abnt);
        Assert.Equal(5, abnt.MaxSectionDepth);
        Assert.Contains(catalog.List(), p => p.Id == "abnt-tcc");
    }

    [Fact]
    public void Unknown_profile_is_null()
    {
        Assert.Null(new EmbeddedNormProfileCatalog().Find("nao-existe"));
    }
}
