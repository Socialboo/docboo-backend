using Application.Documents;
using Application.Tests.Support;
using Domain.Documents;

namespace Application.Tests;

public class DocumentSanitizerTests
{
    [Fact]
    public void Golden_sample_is_already_clean()
    {
        var doc = Samples.Golden();

        Assert.Equal(DocumentJson.Serialize(doc), DocumentJson.Serialize(DocumentSanitizer.Sanitize(doc)));
    }

    [Fact]
    public void Removes_control_characters_and_normalizes_text_runs()
    {
        var doc = Samples.Golden().WithSections(Samples.Section("s1",
        [
            Samples.Paragraph("p1",
                Samples.Text("a\u0000b\u0007c\td\ne", TextMark.Italic, TextMark.Bold, TextMark.Italic),
                Samples.Text(""),
                Samples.Text("e\u0301")), // e + combining acute -> é (NFC)
        ]));

        var content = ((ParagraphBlock)DocumentSanitizer.Sanitize(doc).Textual.Sections[0].Blocks[0]).Content;

        Assert.Equal(2, content.Count); // empty run dropped
        var first = Assert.IsType<TextInline>(content[0]);
        Assert.Equal("abc d e", first.Text);
        Assert.Equal([TextMark.Bold, TextMark.Italic], first.Marks);
        Assert.Equal("\u00e9", Assert.IsType<TextInline>(content[1]).Text);
    }

    [Fact]
    public void Removes_lone_surrogates_but_keeps_valid_pairs()
    {
        Assert.Equal("ab", DocumentSanitizer.Clean("a\ud800b", allowNewlines: false));
        Assert.Equal("a\ud83d\ude00b", DocumentSanitizer.Clean("a\ud83d\ude00b", allowNewlines: false));
    }

    [Fact]
    public void Trims_single_line_fields_and_cleans_lists()
    {
        var doc = Samples.Golden();
        doc = doc with
        {
            Metadata = doc.Metadata with
            {
                Title = "  Título\r\ncom quebra  ",
                Subtitle = "   ",
                Authors = [" Ana ", "", "Ana", "Bruno"],
            },
        };

        var metadata = DocumentSanitizer.Sanitize(doc).Metadata;

        Assert.Equal("Título  com quebra", metadata.Title);
        Assert.Null(metadata.Subtitle);
        Assert.Equal(["Ana", "Bruno"], metadata.Authors);
    }

    [Fact]
    public void Abstract_keeps_paragraph_breaks()
    {
        var doc = Samples.Golden();
        doc = doc with
        {
            PreTextual = doc.PreTextual with
            {
                AbstractPt = doc.PreTextual.AbstractPt with { Text = " Primeiro.\r\nSegundo.\rTerceiro. " },
            },
        };

        Assert.Equal("Primeiro.\nSegundo.\nTerceiro.", DocumentSanitizer.Sanitize(doc).PreTextual.AbstractPt.Text);
    }
}
