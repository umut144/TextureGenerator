public sealed class ScatterGeneratorElement : PolyTextureElement
{
    public const string ElementType = "scatter";

    public string SourceElementId { get; set; } = string.Empty;
    public string BoundsElementId { get; set; } = string.Empty;
    public int Seed { get; set; } = 1;
    public int Count { get; set; } = 64;
    public int ClusterCount { get; set; } = 6;
    public float ClusterStrength { get; set; } = 0.8f;
    public float ClusterRadiusCm { get; set; } = 48.0f;
    public float ScaleMin { get; set; } = 0.35f;
    public float ScaleMax { get; set; } = 1.4f;
    public float RotationMinDegrees { get; set; } = -180.0f;
    public float RotationMaxDegrees { get; set; } = 180.0f;
    public bool RenderSource { get; set; }

    public override string Type => ElementType;

    public override PolyTextureElement Clone()
    {
        return new ScatterGeneratorElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            SourceElementId = SourceElementId,
            BoundsElementId = BoundsElementId,
            Seed = Seed,
            Count = Count,
            ClusterCount = ClusterCount,
            ClusterStrength = ClusterStrength,
            ClusterRadiusCm = ClusterRadiusCm,
            ScaleMin = ScaleMin,
            ScaleMax = ScaleMax,
            RotationMinDegrees = RotationMinDegrees,
            RotationMaxDegrees = RotationMaxDegrees,
            RenderSource = RenderSource
        };
    }
}
