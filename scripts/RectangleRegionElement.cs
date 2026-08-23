using Godot;

public sealed class RectangleRegionElement : PolyTextureElement
{
    public const string ElementType = "rectangle_region";

    public Vector2 Position { get; set; }
    public float RotationDegrees { get; set; }
    public float WidthCm { get; set; } = 80.0f;
    public float HeightCm { get; set; } = 40.0f;
    public float CornerRadiusCm { get; set; }

    public override string Type => ElementType;

    public override PolyTextureElement Clone()
    {
        return new RectangleRegionElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            Position = Position,
            RotationDegrees = RotationDegrees,
            WidthCm = WidthCm,
            HeightCm = HeightCm,
            CornerRadiusCm = CornerRadiusCm
        };
    }
}
