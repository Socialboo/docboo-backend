using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Tests.Support;
using Domain.Documents;

namespace Application.Tests;

public class DocumentJsonTests
{
    private static JsonObject GoldenNode() => JsonNode.Parse(Samples.GoldenJson)!.AsObject();

    private static JsonObject Block(JsonObject root, int section, int block) =>
        root["textual"]!["sections"]![section]!["blocks"]![block]!.AsObject();

    [Fact]
    public void Golden_sample_round_trips_without_changes()
    {
        var first = DocumentJson.Serialize(Samples.Golden());
        var second = DocumentJson.Serialize(DocumentJson.Deserialize(first));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Golden_sample_maps_polymorphic_blocks_and_inlines()
    {
        var doc = Samples.Golden();

        var intro = doc.Textual.Sections[0];
        var paragraph = Assert.IsType<ParagraphBlock>(intro.Blocks[0]);
        Assert.IsType<CitationInline>(paragraph.Content[1]);
        var footnote = Assert.IsType<FootnoteInline>(paragraph.Content[3]);
        Assert.Equal("fn-1", footnote.Id);
        Assert.IsType<ListBlock>(intro.Children[0].Blocks[0]);
        Assert.IsType<LongQuoteBlock>(doc.Textual.Sections[1].Children[0].Blocks[0]);
        Assert.IsType<TableBlock>(doc.Textual.Sections[2].Blocks[0]);
        Assert.IsType<FigureBlock>(doc.Textual.Sections[2].Blocks[1]);
        Assert.IsType<HardBreakInline>(((ParagraphBlock)doc.Textual.Sections[3].Blocks[0]).Content[1]);
        Assert.Equal("Rio de Janeiro", doc.References[1].PublisherPlace);
    }

    [Fact]
    public void Serializes_discriminators_and_camel_case_enum_values()
    {
        var json = DocumentJson.Serialize(Samples.Golden());

        Assert.Contains("\"type\":\"longQuote\"", json);
        Assert.Contains("\"marks\":[\"italic\"]", json);
        Assert.Contains("\"mode\":\"narrative\"", json);
        Assert.Contains("\"publisher-place\":\"Rio de Janeiro\"", json);
    }

    [Fact]
    public void Accepts_discriminator_after_other_properties()
    {
        var root = GoldenNode();
        var block = Block(root, 1, 0);
        var type = block["type"]!.GetValue<string>();
        block.Remove("type");
        block.Add("type", type); // now the last property

        var doc = DocumentJson.Deserialize(root.ToJsonString());

        Assert.IsType<ParagraphBlock>(doc.Textual.Sections[1].Blocks[0]);
    }

    [Fact]
    public void Rejects_unknown_properties()
    {
        var root = GoldenNode();
        root.Add("script", "alert(1)");

        Assert.Throws<JsonException>(() => DocumentJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void Rejects_unknown_block_type()
    {
        var root = GoldenNode();
        Block(root, 0, 0)["type"] = "html";

        Assert.Throws<JsonException>(() => DocumentJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void Rejects_missing_required_property()
    {
        var root = GoldenNode();
        Block(root, 2, 1).Remove("widthPercent");

        Assert.Throws<JsonException>(() => DocumentJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void Rejects_null_for_non_nullable_property()
    {
        var root = GoldenNode();
        root["metadata"]!["title"] = null;

        Assert.Throws<JsonException>(() => DocumentJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void Keeps_unknown_csl_fields_as_extension_data()
    {
        var root = GoldenNode();
        root["references"]![1]!["collection-title"] = "Série Exemplo";

        var doc = DocumentJson.Deserialize(root.ToJsonString());

        Assert.Equal("Série Exemplo", doc.References[1].Extra!["collection-title"].GetString());
        Assert.Contains("\"collection-title\":\"Série Exemplo\"", DocumentJson.Serialize(doc));
    }
}
