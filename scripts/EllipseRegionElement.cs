using Godot;

public sealed class EllipseRegionElement : PolyTextureElement
{
    public const string ElementType = "ellipse_region";

    public Vector2 Position { get; set; }
    public float RotationDegrees { get; set; }
    public float WidthCm { get; set; } = 24.0f;
    public float HeightCm { get; set; } = 24.0f;

    public override string Type => ElementType;

    public override PolyTextureElement Clone()
    {
        return new EllipseRegionElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            Position = Position,
            RotationDegrees = RotationDegrees,
            WidthCm = WidthCm,
            HeightCm = HeightCm
        };
    }
}
