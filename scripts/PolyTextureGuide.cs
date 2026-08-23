using Godot;

public sealed class PolyTextureGuide
{
    public string Id { get; set; } = "guide";
    public string Name { get; set; } = "Guide";
    public PolyTextureGuideType Type { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 AxisEnd { get; set; }

    public PolyTextureGuide Clone()
    {
        return new PolyTextureGuide
        {
            Id = Id,
            Name = Name,
            Type = Type,
            Position = Position,
            AxisEnd = AxisEnd
        };
    }
}
