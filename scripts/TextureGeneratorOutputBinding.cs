using System.Collections.Generic;

public sealed class TextureGeneratorOutputBinding
{
    public string Id { get; set; } = "output";
    public string Name { get; set; } = "Output";
    public TextureGeneratorOutputKind Kind { get; set; } = TextureGeneratorOutputKind.Mask;
    public List<string> SourceElementIds { get; } = new();
    public bool Enabled { get; set; } = true;
    public float Value { get; set; } = 1.0f;
    public float HeightAmplitudeCm { get; set; } = 1.0f;

    public TextureGeneratorOutputBinding Clone()
    {
        TextureGeneratorOutputBinding clone = new()
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
