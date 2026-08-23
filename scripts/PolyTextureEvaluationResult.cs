using System.Collections.Generic;

public sealed class PolyTextureEvaluationResult
{
    public HashSet<string> HiddenSourceIds { get; } = new(System.StringComparer.Ordinal);
    public List<SweepGeneratorElement> LiveSweeps { get; } = new();
    public List<MirrorGeneratorElement> LiveMirrors { get; } = new();
    public Dictionary<string, List<List<Godot.Vector2>>> GeometryByElementId { get; } = new(System.StringComparer.Ordinal);

    public List<List<Godot.Vector2>> GetGeometry(string elementId)
    {
        return GeometryByElementId.TryGetValue(elementId, out List<List<Godot.Vector2>> geometry)
            ? geometry
            : new List<List<Godot.Vector2>>();
    }
}
