public sealed class BranchGeneratorElement : TextureGeneratorElement
{
    public const string ElementType = "branch";

    public string SourceElementId { get; set; } = string.Empty;
    public int Seed { get; set; } = 1;
    public int Count { get; set; } = 10;
    public int Segments { get; set; } = 3;
    public int Depth { get; set; } = 1;
    public int ChildrenPerBranch { get; set; } = 2;
    public float StartT { get; set; } = 0.08f;
    public float EndT { get; set; } = 0.92f;
    public float LengthMinCm { get; set; } = 18.0f;
    public float LengthMaxCm { get; set; } = 55.0f;
    public float AngleMinDegrees { get; set; } = 25.0f;
    public float AngleMaxDegrees { get; set; } = 65.0f;
    public float WidthScale { get; set; } = 0.45f;
    public float Irregularity { get; set; } = 0.25f;
    public float DepthLengthScale { get; set; } = 0.55f;
    public bool RenderSource { get; set; } = true;

    public override string Type => ElementType;

    public override TextureGeneratorElement Clone()
    {
        return new BranchGeneratorElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            SourceElementId = SourceElementId,
            Seed = Seed,
            Count = Count,
            Segments = Segments,
            Depth = Depth,
            ChildrenPerBranch = ChildrenPerBranch,
            StartT = StartT,
            EndT = EndT,
            LengthMinCm = LengthMinCm,
            LengthMaxCm = LengthMaxCm,
            AngleMinDegrees = AngleMinDegrees,
            AngleMaxDegrees = AngleMaxDegrees,
            WidthScale = WidthScale,
            Irregularity = Irregularity,
            DepthLengthScale = DepthLengthScale,
            RenderSource = RenderSource
        };
    }
}
