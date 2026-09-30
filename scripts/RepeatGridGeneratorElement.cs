public sealed class RepeatGridGeneratorElement : TextureGeneratorElement
{
    public const string ElementType = "repeat_grid";

    public string SourceElementId { get; set; } = string.Empty;
    public int Columns { get; set; } = 6;
    public int Rows { get; set; } = 10;
    public float StepXCm { get; set; } = 85.0f;
    public float StepYCm { get; set; } = 45.0f;
    public float AlternateRowOffsetXCm { get; set; } = -42.5f;

    public override string Type => ElementType;

    public override TextureGeneratorElement Clone()
    {
        return new RepeatGridGeneratorElement
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            SourceElementId = SourceElementId,
            Columns = Columns,
            Rows = Rows,
            StepXCm = StepXCm,
            StepYCm = StepYCm,
            AlternateRowOffsetXCm = AlternateRowOffsetXCm
        };
    }
}
