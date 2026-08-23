public sealed class CenterPathElement : CenterStrokeElement
{
    public new const string ElementType = "center_path";

    public override bool SupportsBezierHandles => true;
    public override string Type => ElementType;

    public override PolyTextureElement Clone()
    {
        CenterPathElement clone = new()
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
