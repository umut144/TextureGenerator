public sealed class MirrorGeneratorElement : PolyTextureElement
{
    public const string ElementType = "mirror";

    public string SourceElementId { get; set; } = string.Empty;
    public string AxisGuideId { get; set; } = string.Empty;
    public bool RenderSource { get; set; }

    public override string Type => ElementType;

    public override PolyTextureElement Clone()
    {
        return new MirrorGeneratorElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            SourceElementId = SourceElementId,
            AxisGuideId = AxisGuideId,
            RenderSource = RenderSource
        };
    }
}
