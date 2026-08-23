public sealed class SweepGeneratorElement : PolyTextureElement
{
    public const string ElementType = "sweep";

    public string SourceElementId { get; set; } = string.Empty;
    public string TargetElementId { get; set; } = string.Empty;
    public int Count { get; set; } = 8;
    public float StartT { get; set; }
    public float EndT { get; set; } = 1.0f;
    public PolyTextureSweepAlignment Alignment { get; set; } = PolyTextureSweepAlignment.Normal;
    public PolyTextureSweepSideMode SideMode { get; set; } = PolyTextureSweepSideMode.Right;
    public float RotationOffsetDegrees { get; set; }
    public float LengthScaleStart { get; set; } = 1.0f;
    public float LengthScaleEnd { get; set; } = 0.1f;
    public float WidthScaleStart { get; set; } = 1.0f;
    public float WidthScaleEnd { get; set; } = 0.1f;
    public bool RenderSource { get; set; }

    public override string Type => ElementType;

    public override PolyTextureElement Clone()
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
