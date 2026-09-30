public sealed class InvertFilterElement : TextureGeneratorElement
{
    public const string ElementType = "invert";

    public string SourceElementId { get; set; } = string.Empty;

    public override string Type => ElementType;

    public override TextureGeneratorElement Clone()
    {
        return new InvertFilterElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            SourceElementId = SourceElementId
        };
    }
}
