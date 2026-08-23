using System.Collections.Generic;

public sealed class PolyTextureEvaluationResult
{
    public HashSet<string> HiddenSourceIds { get; } = new(System.StringComparer.Ordinal);
    public List<SweepGeneratorElement> LiveSweeps { get; } = new();
    public List<MirrorGeneratorElement> LiveMirrors { get; } = new();
}
