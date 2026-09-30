public sealed class CrackLineElement : CenterStrokeElement
{
    public new const string ElementType = "crack_line";

    public override bool SupportsBezierHandles => true;
    public override string Type => ElementType;

    public override TextureGeneratorElement Clone()
    {
        CrackLineElement clone = new()
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            Opacity = Opacity,
            Symmetry = Symmetry,
            Falloff = Falloff,
            Transform = Transform.Clone()
        };

        foreach (CenterStrokePoint point in Points)
        {
            clone.Points.Add(point.Clone());
        }

        return clone;
    }
}
