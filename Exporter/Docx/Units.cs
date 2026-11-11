namespace Exporter.Docx;

/// <summary>Conversions from the units used in norms (cm, pt) to WordprocessingML units.</summary>
public static class Units
{
    /// <summary>Twentieths of a point (twips): page size, margins, indents, spacing.</summary>
    public static int CmToTwips(double cm) => (int)Math.Round(cm * 1440 / 2.54, MidpointRounding.AwayFromZero);

    public static int PtToTwips(double pt) => (int)Math.Round(pt * 20, MidpointRounding.AwayFromZero);

    /// <summary>Font sizes are written in half-points (w:sz).</summary>
    public static int PtToHalfPoints(double pt) => (int)Math.Round(pt * 2, MidpointRounding.AwayFromZero);

    /// <summary>Line spacing with lineRule="auto" is in 240ths of a line (240 = single, 360 = 1.5).</summary>
    public static int LineSpacingToAutoLine(double multiple) => (int)Math.Round(multiple * 240, MidpointRounding.AwayFromZero);

    /// <summary>English Metric Units (images): 360000 per cm.</summary>
    public static long CmToEmu(double cm) => (long)Math.Round(cm * 360000, MidpointRounding.AwayFromZero);
}
