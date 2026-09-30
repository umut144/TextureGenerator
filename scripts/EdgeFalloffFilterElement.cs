public sealed class EdgeFalloffFilterElement : TextureGeneratorElement
{
    public const string ElementType = "edge_falloff";

    public string SourceElementId { get; set; } = string.Empty;
    public float RadiusCm { get; set; } = 3.0f;
    public float Exponent { get; set; } = 1.0f;

    public override string Type => ElementType;

    public override TextureGeneratorElement Clone()
    {
        return new EdgeFalloffFilterElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            SourceElementId = SourceElementId,
            RadiusCm = RadiusCm,
            Exponent = Exponent
        };
    }
}
