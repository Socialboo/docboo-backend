using Domain.Documents;
using Exporter.References;

namespace Exporter.Tests.References;

public class ReferenceFormattingTests
{
    private static CslName N(string family, string given) => new() { Family = family, Given = given };
    private static CslDate Y(int year) => new() { DateParts = [[year]] };

    private static readonly CslItem Book = new()
    {
        Id = "lima2019", Type = "book", Title = "Editoração eletrônica", Author = [N("Lima", "Roberto")],
        Issued = Y(2019), Publisher = "Editora Exemplo", PublisherPlace = "Rio de Janeiro", Edition = "2",
    };

    private static readonly CslItem Article = new()
    {
        Id = "souza2021", Type = "article-journal", Title = "Tempo dedicado à formatação de trabalhos",
        Author = [N("Souza", "Ana"), N("Pereira", "Carlos")], Issued = Y(2021),
        ContainerTitle = "Revista Exemplo de Educação", Volume = "12", Issue = "3", Page = "10-25",
    };

    private static readonly CslItem Webpage = new()
    {
        Id = "guia", Type = "webpage", Title = "Guia de normalização de trabalhos", Author = [new CslName { Literal = "Biblioteca Central Exemplo" }],
        Issued = Y(2020), Url = "https://example.org/guia", Accessed = new CslDate { DateParts = [[2026, 3, 15]] },
    };

    private static string Ref(CslItem item, params CslItem[] all) =>
        new ReferenceFormatter(new ReferenceCatalog(all.Length > 0 ? all : [item])).Format(item).ToPlainText();

    private static string Cite(string mode, string? locator, params string[] ids) =>
        new CitationFormatter(new ReferenceCatalog([Book, Article, Webpage])).Format(new CitationInline
        {
            RefIds = ids, Locator = locator, Mode = mode == "p" ? CitationMode.Parenthetical : CitationMode.Narrative,
        });

    // ---------- reference list ----------

    [Fact]
    public void Formats_a_book()
    {
        Assert.Equal("LIMA, Roberto. Editoração eletrônica. 2. ed. Rio de Janeiro: Editora Exemplo, 2019.", Ref(Book));
    }

    [Fact]
    public void Highlights_the_book_title_in_bold_but_not_the_subtitle()
    {
        var item = Book with { Title = "Editoração eletrônica: uma introdução" };

        var segments = new ReferenceFormatter(new ReferenceCatalog([item])).Format(item);

        Assert.Equal("Editoração eletrônica", Assert.Single(segments, s => s.Bold).Text);
        Assert.Contains("Editoração eletrônica: uma introdução. 2. ed.", segments.ToPlainText());
    }

    [Fact]
    public void Formats_a_journal_article_with_the_journal_in_bold()
    {
        var segments = new ReferenceFormatter(new ReferenceCatalog([Article])).Format(Article);

        Assert.Equal(
            "SOUZA, Ana; PEREIRA, Carlos. Tempo dedicado à formatação de trabalhos. Revista Exemplo de Educação, v. 12, n. 3, p. 10-25, 2021.",
            segments.ToPlainText());
        Assert.Equal("Revista Exemplo de Educação", Assert.Single(segments, s => s.Bold).Text);
    }

    [Fact]
    public void Formats_a_webpage_with_access_date()
    {
        Assert.Equal(
            "BIBLIOTECA CENTRAL EXEMPLO. Guia de normalização de trabalhos. 2020. Disponível em: https://example.org/guia. Acesso em: 15 mar. 2026.",
            Ref(Webpage));
    }

    [Fact]
    public void Formats_a_chapter_with_editors_and_pages()
    {
        var chapter = new CslItem
        {
            Id = "cap", Type = "chapter", Title = "Normas técnicas", Author = [N("Costa", "Bia")],
            Editor = [N("Ramos", "Davi")], ContainerTitle = "Manual de escrita", Issued = Y(2018),
            Publisher = "Editora X", PublisherPlace = "Curitiba", Page = "33-50",
        };

        Assert.Equal("COSTA, Bia. Normas técnicas. In: RAMOS, Davi (ed.). Manual de escrita. Curitiba: Editora X, 2018. p. 33-50.", Ref(chapter));
    }

    [Fact]
    public void Formats_a_thesis()
    {
        var thesis = new CslItem
        {
            Id = "tese", Type = "thesis", Title = "Sistemas de documentos", Author = [N("Alves", "Rui")], Issued = Y(2022),
            Genre = "Dissertação (Mestrado em Computação)", Publisher = "Universidade Exemplo", PublisherPlace = "Recife",
        };

        Assert.Equal("ALVES, Rui. Sistemas de documentos. 2022. Dissertação (Mestrado em Computação) – Universidade Exemplo, Recife.", Ref(thesis));
    }

    [Fact]
    public void Uses_placeholders_for_missing_imprint_data()
    {
        var bare = new CslItem { Id = "x", Type = "book", Title = "Obra sem dados", Author = [N("Melo", "Ana")] };

        Assert.Equal("MELO, Ana. Obra sem dados. [S. l.]: [s. n.], [s. d.].", Ref(bare));
    }

    [Fact]
    public void Appends_doi()
    {
        Assert.EndsWith("2021. DOI: 10.1000/xyz.", Ref(Article with { Doi = "10.1000/xyz" }));
    }

    [Fact]
    public void Orders_the_list_alphabetically_ignoring_accents()
    {
        var alvares = Book with { Id = "a", Author = [N("Álvares", "Zé")] };
        var catalog = new ReferenceCatalog([Article, Book, Webpage, alvares]);

        Assert.Equal(["a", "guia", "lima2019", "souza2021"], catalog.Ordered.Select(r => r.Id));
    }

    // ---------- citations ----------

    [Theory]
    [InlineData("p", null, new[] { "souza2021" }, "(SOUZA; PEREIRA, 2021)")]
    [InlineData("p", "p. 12", new[] { "lima2019" }, "(LIMA, 2019, p. 12)")]
    [InlineData("n", "p. 45", new[] { "lima2019" }, "Lima (2019, p. 45)")]
    [InlineData("n", null, new[] { "souza2021" }, "Souza e Pereira (2021)")]
    [InlineData("p", null, new[] { "guia" }, "(BIBLIOTECA CENTRAL EXEMPLO, 2020)")]
    [InlineData("p", "p. 3", new[] { "lima2019", "souza2021" }, "(LIMA, 2019; SOUZA; PEREIRA, 2021, p. 3)")]
    [InlineData("n", null, new[] { "lima2019", "guia" }, "Lima (2019) e Biblioteca Central Exemplo (2020)")]
    public void Formats_author_date_citations(string mode, string? locator, string[] ids, string expected)
    {
        Assert.Equal(expected, Cite(mode, locator, ids));
    }

    [Fact]
    public void Four_or_more_authors_become_et_al()
    {
        var many = Article with { Id = "m", Author = [N("A", "1"), N("B", "2"), N("C", "3"), N("D", "4")] };
        var three = Article with { Id = "t", Author = [N("Ana", "1"), N("Bia", "2"), N("Caio", "3")] };
        var f = new CitationFormatter(new ReferenceCatalog([many, three]));

        Assert.Equal("(A et al., 2021)", f.Format(new CitationInline { RefIds = ["m"], Locator = null, Mode = CitationMode.Parenthetical }));
        Assert.Equal("Ana, Bia e Caio (2021)", f.Format(new CitationInline { RefIds = ["t"], Locator = null, Mode = CitationMode.Narrative }));
    }

    [Fact]
    public void Same_author_and_year_get_letter_suffixes_in_citations_and_list()
    {
        var first = Book with { Id = "l1", Title = "A primeira obra" };
        var second = Book with { Id = "l2", Title = "B segunda obra" };
        var catalog = new ReferenceCatalog([second, first]);
        var cite = new CitationFormatter(catalog);

        Assert.Equal("(LIMA, 2019a)", cite.Format(new CitationInline { RefIds = ["l1"], Locator = null, Mode = CitationMode.Parenthetical }));
        Assert.Equal("(LIMA, 2019b)", cite.Format(new CitationInline { RefIds = ["l2"], Locator = null, Mode = CitationMode.Parenthetical }));
        Assert.EndsWith("2019b.", new ReferenceFormatter(catalog).Format(second).ToPlainText());
    }

    [Fact]
    public void Without_author_uses_the_first_word_of_the_title()
    {
        var anonymous = Webpage with { Id = "anon", Author = null };
        var f = new CitationFormatter(new ReferenceCatalog([anonymous]));

        Assert.Equal("(GUIA..., 2020)", f.Format(new CitationInline { RefIds = ["anon"], Locator = null, Mode = CitationMode.Parenthetical }));
    }
}
