using System.Collections.Generic;

public sealed class PolyTextureOutputBinding
{
    public string Id { get; set; } = "output";
    public string Name { get; set; } = "Output";
    public PolyTextureOutputKind Kind { get; set; } = PolyTextureOutputKind.Mask;
    public List<string> SourceElementIds { get; } = new();
    public bool Enabled { get; set; } = true;
    public float Value { get; set; } = 1.0f;
    public float HeightAmplitudeCm { get; set; } = 1.0f;

    public PolyTextureOutputBinding Clone()
    {
        PolyTextureOutputBinding clone = new()
        {
            Id = Id,
            Name = Name,
            Kind = Kind,
            Enabled = Enabled,
            Value = Value,
            HeightAmplitudeCm = HeightAmplitudeCm
        };
        clone.SourceElementIds.AddRange(SourceElementIds);
        return clone;
    }
}
