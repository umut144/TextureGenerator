public sealed class InvertFilterElement : PolyTextureElement
{
    public const string ElementType = "invert";

    public string SourceElementId { get; set; } = string.Empty;

    public override string Type => ElementType;

    public override PolyTextureElement Clone()
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
