public sealed class SweepGeneratorElement : TextureGeneratorElement
{
    public const string ElementType = "sweep";

    public string SourceElementId { get; set; } = string.Empty;
    public string TargetElementId { get; set; } = string.Empty;
    public int Count { get; set; } = 8;
    public float StartT { get; set; }
    public float EndT { get; set; } = 1.0f;
    public TextureGeneratorSweepAlignment Alignment { get; set; } = TextureGeneratorSweepAlignment.Normal;
    public TextureGeneratorSweepSideMode SideMode { get; set; } = TextureGeneratorSweepSideMode.Right;
    public float RotationOffsetDegrees { get; set; }
    public float LengthScaleStart { get; set; } = 1.0f;
    public float LengthScaleEnd { get; set; } = 0.1f;
    public float WidthScaleStart { get; set; } = 1.0f;
    public float WidthScaleEnd { get; set; } = 0.1f;
    public bool RenderSource { get; set; }

    public override string Type => ElementType;

    public override TextureGeneratorElement Clone()
    {
        return new SweepGeneratorElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            SourceElementId = SourceElementId,
            TargetElementId = TargetElementId,
            Count = Count,
            StartT = StartT,
            EndT = EndT,
            Alignment = Alignment,
            SideMode = SideMode,
            RotationOffsetDegrees = RotationOffsetDegrees,
            LengthScaleStart = LengthScaleStart,
            LengthScaleEnd = LengthScaleEnd,
            WidthScaleStart = WidthScaleStart,
            WidthScaleEnd = WidthScaleEnd,
            RenderSource = RenderSource
        };
    }
}
