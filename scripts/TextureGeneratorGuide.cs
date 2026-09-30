using Godot;

public sealed class TextureGeneratorGuide
{
    public string Id { get; set; } = "guide";
    public string Name { get; set; } = "Guide";
    public TextureGeneratorGuideType Type { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 AxisEnd { get; set; }

    public TextureGeneratorGuide Clone()
    {
        return new TextureGeneratorGuide
        {
            Id = Id,
            Name = Name,
            Type = Type,
            Position = Position,
            AxisEnd = AxisEnd
        };
    }
}
