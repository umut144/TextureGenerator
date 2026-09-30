using System.Collections.Generic;
using Godot;

public class CenterStrokeElement : TextureGeneratorElement
{
    public const string ElementType = "center_stroke";

    public bool Symmetry { get; set; } = true;
    public float Falloff { get; set; }
    public TextureGeneratorElementTransform Transform { get; set; } = new();
    public List<CenterStrokePoint> Points { get; } = new();

    public virtual bool SupportsBezierHandles => false;
    public override string Type => ElementType;

    public override TextureGeneratorElement Clone()
    {
        CenterStrokeElement clone = new()
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

    public void MakePointsLocal()
    {
        if (Points.Count == 0)
        {
            return;
        }

        Vector2 anchor = Points[0].Position;
        Transform.Position = anchor;
        foreach (CenterStrokePoint point in Points)
        {
            point.Position -= anchor;
        }
        NormalizeLocalAxes();
    }

    public void NormalizeAnchor()
    {
        if (Points.Count == 0 || Points[0].Position.LengthSquared() < 0.0001f)
        {
            return;
        }

        Vector2 localAnchor = Points[0].Position;
        Vector2 documentAnchor = Transform.TransformPoint(localAnchor);
        foreach (CenterStrokePoint point in Points)
        {
            point.Position -= localAnchor;
        }
        Transform.Position = documentAnchor;
    }

    public void NormalizeLocalAxes()
    {
        NormalizeAnchor();
        if (Points.Count < 2)
        {
            return;
        }

        Vector2 primaryDirection = Vector2.Zero;
        for (int pointIndex = 1; pointIndex < Points.Count; pointIndex++)
        {
            primaryDirection = Points[pointIndex].Position;
            if (primaryDirection.LengthSquared() >= 0.0001f)
            {
                break;
            }
        }

        if (primaryDirection.LengthSquared() < 0.0001f)
        {
            return;
        }

        float angleRadians = primaryDirection.Angle();
        if (Mathf.Abs(angleRadians) < 0.0001f)
        {
            return;
        }

        foreach (CenterStrokePoint point in Points)
        {
            point.Position = point.Position.Rotated(-angleRadians);
            point.InHandle = point.InHandle.Rotated(-angleRadians);
            point.OutHandle = point.OutHandle.Rotated(-angleRadians);
        }
        Transform.RotationDegrees += Mathf.RadToDeg(angleRadians);
    }
}
