public sealed class PolyTextureOutputBinding
{
    public string Id { get; set; } = "output";
    public string Name { get; set; } = "Output";
    public PolyTextureOutputKind Kind { get; set; } = PolyTextureOutputKind.Mask;
    public string SourceElementId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public float Value { get; set; } = 1.0f;
    public float HeightAmplitudeCm { get; set; } = 1.0f;

    public PolyTextureOutputBinding Clone()
    {
        return new PolyTextureOutputBinding
        {
            Id = Id,
            Name = Name,
            Kind = Kind,
            SourceElementId = SourceElementId,
            Enabled = Enabled,
            Value = Value,
            HeightAmplitudeCm = HeightAmplitudeCm
        };
    }
}
