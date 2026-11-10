using Application.Documents;
using Application.Tests.Support;
using Domain.Documents;

namespace Application.Tests;

public class DocumentValidatorTests
{
    private const string Profile = "abnt-tcc";

    private static IReadOnlyList<Application.Common.ValidationError> Validate(DocumentModel doc, DocumentLimits? limits = null) =>
        Samples.Validator(limits).Validate(doc, Profile);

    [Fact]
    public void Golden_sample_is_valid()
    {
        Assert.Empty(Validate(Samples.Golden()));
    }

    [Fact]
    public void New_empty_document_is_valid()
    {
        Assert.Empty(Validate(DocumentModel.CreateEmpty(Profile, "Novo trabalho")));
    }

    [Fact]
    public void Rejects_unsupported_schema_version()
    {
        var errors = Validate(Samples.Golden() with { SchemaVersion = 2 });

        Assert.Contains(errors, e => e is { Path: "schemaVersion", Code: "schema.unsupported" });
    }

    [Fact]
    public void Rejects_profile_change_and_unknown_profile()
    {
        var doc = Samples.Golden() with { ProfileId = "apa" };

        Assert.Contains(Validate(doc), e => e.Code == "profile.mismatch");
        Assert.Contains(Samples.Validator().Validate(doc, "apa"), e => e.Code == "profile.unknown");
    }

    [Fact]
    public void Allows_five_levels_and_rejects_the_sixth()
    {
        Section Nest(int depth) => depth == 1
            ? Samples.Section($"s{depth}")
            : Samples.Section($"s{depth}", children: [Nest(depth - 1)]);

        Assert.Empty(Validate(Samples.Golden().WithSections(Nest(5)) with { References = [] }));

        var errors = Validate(Samples.Golden().WithSections(Nest(6)) with { References = [] });
        var error = Assert.Single(errors);
        Assert.Equal("section.tooDeep", error.Code);
        Assert.Equal("textual.sections[0].children[0].children[0].children[0].children[0].children[0]", error.Path);
    }

    [Fact]
    public void Rejects_duplicate_and_invalid_ids()
    {
        var doc = Samples.Golden().WithSections(
            Samples.Section("dup", [Samples.Paragraph("dup"), Samples.Paragraph("bad id!")]));

        var errors = Validate(doc);

        Assert.Contains(errors, e => e is { Code: "id.duplicate", Path: "textual.sections[0].blocks[0].id" });
        Assert.Contains(errors, e => e is { Code: "id.invalid", Path: "textual.sections[0].blocks[1].id" });
    }

    [Fact]
    public void Rejects_citation_to_unknown_reference()
    {
        var citation = new CitationInline { RefIds = ["nao-existe"], Locator = null, Mode = CitationMode.Parenthetical };
        var doc = Samples.Golden().WithSections(Samples.Section("s", [Samples.Paragraph("p", citation)]));

        var error = Assert.Single(Validate(doc));

        Assert.Equal("citation.unknownReference", error.Code);
        Assert.Equal("textual.sections[0].blocks[0].content[0].refIds[0]", error.Path);
    }

    [Fact]
    public void Rejects_nested_footnotes()
    {
        var inner = new FootnoteInline { Id = "fn-inner", Content = [Samples.Text("x")] };
        var outer = new FootnoteInline { Id = "fn-outer", Content = [inner] };
        var doc = Samples.Golden().WithSections(Samples.Section("s", [Samples.Paragraph("p", outer)]));

        Assert.Contains(Validate(doc), e => e.Code == "footnote.nested");
    }

    [Fact]
    public void Rejects_invalid_figure_and_table()
    {
        var figure = new FigureBlock { Id = "f", AssetId = "img", Caption = [], Source = [], WidthPercent = 150 };
        var table = new TableBlock
        {
            Id = "t",
            Title = [],
            Source = [],
            Rows = [new TableRow { Header = false, Cells = [new TableCell { Content = [], ColSpan = 0, RowSpan = 2 }] }],
        };
        var doc = Samples.Golden().WithSections(Samples.Section("s", [figure, table]));

        var errors = Validate(doc);

        Assert.Contains(errors, e => e.Code == "figure.width");
        Assert.Contains(errors, e => e is { Code: "table.span", Path: "textual.sections[0].blocks[1].rows[0].cells[0].colSpan" });
        Assert.Contains(errors, e => e is { Code: "table.span", Path: "textual.sections[0].blocks[1].rows[0].cells[0].rowSpan" });
    }

    [Fact]
    public void Rejects_duplicate_reference_ids_and_non_http_urls()
    {
        var doc = Samples.Golden();
        doc = doc with
        {
            References = [.. doc.References, doc.References[0], doc.References[2] with { Id = "js", Url = "javascript:alert(1)" }],
        };

        var errors = Validate(doc);

        Assert.Contains(errors, e => e is { Code: "id.duplicate", Path: "references[3].id" });
        Assert.Contains(errors, e => e is { Code: "url.invalid", Path: "references[4].URL" });
    }

    [Fact]
    public void Enforces_text_and_document_size_limits()
    {
        var limits = new DocumentLimits { MaxTextInlineLength = 10 };
        var doc = Samples.Golden().WithSections(Samples.Section("s", [Samples.Paragraph("p", Samples.Text(new string('a', 11)))]));

        Assert.Contains(Validate(doc, limits), e => e.Code == "text.tooLong");
        Assert.Contains(Validate(Samples.Golden(), new DocumentLimits { MaxDocumentBytes = 1024 }), e => e.Code == "document.tooLarge");
    }

    [Fact]
    public void Requires_at_least_one_section()
    {
        Assert.Contains(Validate(Samples.Golden().WithSections() with { References = [] }), e => e.Code == "sections.empty");
    }
}
