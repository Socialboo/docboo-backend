using System.Text.Json.Nodes;
using Domain.Documents;
using Exporter.Docx;
using Exporter.Profiles;

namespace Exporter.Tests.Profiles;

public class NormProfileTests
{
    private static NormProfile Abnt() => new EmbeddedNormProfileCatalog().GetRequired("abnt-tcc");

    private static JsonObject AbntJson()
    {
        using var stream = typeof(NormProfile).Assembly.GetManifestResourceStream("Exporter.Profiles.abnt-tcc.json")!;
        return JsonNode.Parse(stream)!.AsObject();
    }

    [Fact]
    public void Abnt_profile_has_the_mvp_rules()
    {
        var p = Abnt();

        Assert.Equal((21.0, 29.7), (p.Page.WidthCm, p.Page.HeightCm));
        Assert.Equal((3.0, 3.0, 2.0, 2.0), (p.Page.MarginTopCm, p.Page.MarginLeftCm, p.Page.MarginBottomCm, p.Page.MarginRightCm));
        Assert.Equal(12, p.Body.FontSizePt);
        Assert.Equal(1.5, p.Body.LineSpacing);
        Assert.Equal(TextAlignment.Justify, p.Body.Alignment);
        Assert.Equal((4.0, 10.0, 1.0), (p.LongQuote.LeftIndentCm, p.LongQuote.FontSizePt, p.LongQuote.LineSpacing));
        Assert.Equal((10.0, 1.0), (p.SmallText.FontSizePt, p.SmallText.LineSpacing));
        Assert.Equal(PageCountStart.TitlePage, p.PageNumbering.CountFrom);
        Assert.Equal(PageNumberVisibleFrom.Textual, p.PageNumbering.VisibleFrom);
        Assert.Equal(PageNumberPosition.TopRight, p.PageNumbering.Position);
        Assert.True(p.Headings.Numbered);
        Assert.Equal([1, 2, 3, 4, 5], p.Headings.Levels.Select(l => l.Level));
    }

    [Fact]
    public void Heading_levels_are_visually_distinct()
    {
        var p = Abnt();

        var looks = p.Headings.Levels.Select(l => (l.Bold, l.Italic, l.AllCaps)).ToList();

        Assert.Equal(looks.Count, looks.Distinct().Count());
        Assert.True(p.Heading(1).PageBreakBefore);
        Assert.False(p.Heading(2).PageBreakBefore);
    }

    [Theory]
    [InlineData(null, "Times New Roman")]
    [InlineData(FontFamilyOption.TimesNewRoman, "Times New Roman")]
    [InlineData(FontFamilyOption.Arial, "Arial")]
    public void Resolves_font_family_from_work_override(FontFamilyOption? option, string expected)
    {
        Assert.Equal(expected, Abnt().ResolveFontFamily(new ProfileOverrides { FontFamily = option }));
    }

    [Theory]
    [InlineData(3.0, 1701)]
    [InlineData(2.0, 1134)]
    [InlineData(21.0, 11906)]
    [InlineData(29.7, 16838)]
    [InlineData(1.25, 709)]
    [InlineData(4.0, 2268)]
    public void Converts_centimeters_to_twips(double cm, int twips)
    {
        Assert.Equal(twips, Units.CmToTwips(cm));
    }

    [Fact]
    public void Converts_font_sizes_and_line_spacing()
    {
        Assert.Equal(24, Units.PtToHalfPoints(12));
        Assert.Equal(20, Units.PtToHalfPoints(10));
        Assert.Equal(360, Units.LineSpacingToAutoLine(1.5));
        Assert.Equal(240, Units.LineSpacingToAutoLine(1.0));
        Assert.Equal(360, Units.PtToTwips(18));
    }

    [Fact]
    public void Embedded_profile_round_trips_through_the_loader()
    {
        var parsed = NormProfileLoader.Parse(AbntJson().ToJsonString());

        Assert.Equal("abnt-tcc", parsed.Id);
    }

    [Theory]
    [InlineData("page.marginLeftCm", -1)]
    [InlineData("page.headerDistanceCm", 5)]
    [InlineData("body.fontSizePt", 40)]
    [InlineData("body.lineSpacing", 0.5)]
    [InlineData("maxSectionDepth", 6)]
    public void Rejects_invalid_values(string path, double value)
    {
        var json = AbntJson();
        var parts = path.Split('.');
        JsonNode target = json;
        foreach (var part in parts[..^1]) target = target[part]!;
        target[parts[^1]] = value;

        var ex = Assert.Throws<InvalidNormProfileException>(() => NormProfileLoader.Parse(json.ToJsonString()));
        Assert.Contains("abnt-tcc", ex.Message);
    }

    [Fact]
    public void Rejects_unknown_properties_and_bad_font_default()
    {
        var unknown = AbntJson();
        unknown["marginEverywhere"] = 1;
        Assert.Throws<InvalidNormProfileException>(() => NormProfileLoader.Parse(unknown.ToJsonString()));

        var badDefault = AbntJson();
        badDefault["fonts"]!["default"] = "comicSans";
        Assert.Throws<InvalidNormProfileException>(() => NormProfileLoader.Parse(badDefault.ToJsonString()));
    }
}
