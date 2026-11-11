using DocumentFormat.OpenXml.Wordprocessing;
using Exporter.Profiles;

namespace Exporter.Docx;

/// <summary>
/// numbering.xml: progressive heading numbering (1, 1.1, 1.1.1 ...) linked to the Heading styles,
/// bullet lists and ordered lists (a), b), c) ...). Each ordered list restarts its count.
/// </summary>
internal sealed class NumberingBuilder
{
    private const int HeadingsAbstractId = 1;
    private const int BulletAbstractId = 2;
    private const int OrderedAbstractId = 3;
    private const int BulletNumId = 2;

    private readonly NormProfile _profile;
    private readonly List<NumberingInstance> _orderedInstances = [];
    private int _nextNumId = 3;

    public NumberingBuilder(NormProfile profile) => _profile = profile;

    public int BulletListId => BulletNumId;

    /// <summary>New numbering instance for an ordered list, restarting at 1.</summary>
    public int NewOrderedList()
    {
        var id = _nextNumId++;
        _orderedInstances.Add(new NumberingInstance(
            new AbstractNumId { Val = OrderedAbstractId },
            new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 })
        { NumberID = id });
        return id;
    }

    public Numbering Build()
    {
        var numbering = new Numbering();
        numbering.Append(HeadingsAbstract());
        numbering.Append(ListAbstract(BulletAbstractId, NumberFormatValues.Bullet, "•"));
        numbering.Append(ListAbstract(OrderedAbstractId, NumberFormatValues.LowerLetter, "%1)"));

        numbering.Append(new NumberingInstance(new AbstractNumId { Val = HeadingsAbstractId }) { NumberID = StyleSheetBuilder.HeadingNumberingId });
        numbering.Append(new NumberingInstance(new AbstractNumId { Val = BulletAbstractId }) { NumberID = BulletNumId });
        foreach (var instance in _orderedInstances) numbering.Append(instance);
        return numbering;
    }

    private AbstractNum HeadingsAbstract()
    {
        var abstractNum = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.Multilevel }) { AbstractNumberId = HeadingsAbstractId };
        for (var i = 0; i < _profile.MaxSectionDepth; i++)
        {
            var text = string.Join(".", Enumerable.Range(1, i + 1).Select(n => $"%{n}"));
            abstractNum.Append(new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = NumberFormatValues.Decimal },
                new ParagraphStyleIdInLevel { Val = StyleIds.Heading(i + 1) },
                new LevelSuffix { Val = LevelSuffixValues.Space },
                new LevelText { Val = text },
                new LevelJustification { Val = LevelJustificationValues.Left },
                new PreviousParagraphProperties(new Indentation { Left = "0", FirstLine = "0" }))
            { LevelIndex = i });
        }
        return abstractNum;
    }

    private AbstractNum ListAbstract(int id, NumberFormatValues format, string text)
    {
        var left = Units.CmToTwips(_profile.Body.FirstLineIndentCm + 0.63);
        var hanging = Units.CmToTwips(0.63);
        return new AbstractNum(
            new MultiLevelType { Val = MultiLevelValues.SingleLevel },
            new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = format },
                new LevelText { Val = text },
                new LevelJustification { Val = LevelJustificationValues.Left },
                new PreviousParagraphProperties(new Indentation { Left = left.ToString(), Hanging = hanging.ToString() }))
            { LevelIndex = 0 })
        { AbstractNumberId = id };
    }
}
