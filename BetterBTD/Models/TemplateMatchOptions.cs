using OpenCvSharp;

namespace BetterBTD.Models;

public readonly record struct TemplateMatchOptions(TemplateMatchModes Method, bool UseMask)
{
    public static TemplateMatchOptions CCoeffNormedNoMask { get; } = new(TemplateMatchModes.CCoeffNormed, false);

    public static TemplateMatchOptions CCorrNormedMasked { get; } = new(TemplateMatchModes.CCorrNormed, true);

    public static TemplateMatchOptions SqDiffNormedMasked { get; } = new(TemplateMatchModes.SqDiffNormed, true);
}
