using Domain.Documents;

namespace Application.Documents;

/// <summary>Enumerates blocks of a document with their JSON paths (same paths as validation errors).</summary>
public static class DocumentWalker
{
    public static IEnumerable<(string Path, Block Block)> Blocks(DocumentModel doc)
    {
        for (var i = 0; i < doc.Textual.Sections.Count; i++)
            foreach (var item in SectionBlocks($"textual.sections[{i}]", doc.Textual.Sections[i]))
                yield return item;

        foreach (var (name, list) in new[] { ("appendices", doc.PostTextual.Appendices), ("annexes", doc.PostTextual.Annexes) })
            for (var i = 0; i < list.Count; i++)
                for (var j = 0; j < list[i].Blocks.Count; j++)
                    yield return ($"postTextual.{name}[{i}].blocks[{j}]", list[i].Blocks[j]);
    }

    public static IEnumerable<(string Path, FigureBlock Figure)> Figures(DocumentModel doc) =>
        Blocks(doc).Where(b => b.Block is FigureBlock).Select(b => (b.Path, (FigureBlock)b.Block));

    private static IEnumerable<(string, Block)> SectionBlocks(string path, Section section)
    {
        for (var j = 0; j < section.Blocks.Count; j++)
            yield return ($"{path}.blocks[{j}]", section.Blocks[j]);
        for (var c = 0; c < section.Children.Count; c++)
            foreach (var item in SectionBlocks($"{path}.children[{c}]", section.Children[c]))
                yield return item;
    }
}
