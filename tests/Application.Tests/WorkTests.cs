using Application.Tests.Support;
using Domain.Works;

namespace Application.Tests;

public class WorkTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_at_version_one_with_empty_document()
    {
        var work = Work.Create(Guid.NewGuid(), "abnt-tcc", "  Meu TCC ", T0);

        Assert.Equal(1, work.Version);
        Assert.Equal("Meu TCC", work.Title);
        Assert.Equal("Meu TCC", work.Document.Metadata.Title);
        Assert.Equal("abnt-tcc", work.Document.ProfileId);
        Assert.Single(work.Document.Textual.Sections);
    }

    [Fact]
    public void Create_without_title_uses_placeholder()
    {
        Assert.Equal(Work.UntitledTitle, Work.Create(Guid.NewGuid(), "abnt-tcc", "   ", T0).Title);
    }

    [Fact]
    public void ReplaceDocument_bumps_version_and_syncs_title()
    {
        var work = Work.Create(Guid.NewGuid(), "abnt-tcc", null, T0);
        var doc = Samples.Golden();

        work.ReplaceDocument(doc, T0.AddMinutes(1));

        Assert.Equal(2, work.Version);
        Assert.Equal(doc.Metadata.Title, work.Title);
        Assert.Equal(T0.AddMinutes(1), work.UpdatedAt);
    }

    [Fact]
    public void ReplaceDocument_rejects_profile_change()
    {
        var work = Work.Create(Guid.NewGuid(), "abnt-tcc", null, T0);

        Assert.Throws<InvalidOperationException>(() => work.ReplaceDocument(Samples.Golden() with { ProfileId = "apa" }, T0));
    }
}
