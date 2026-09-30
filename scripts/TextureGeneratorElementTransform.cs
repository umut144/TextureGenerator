using Godot;

public sealed class TextureGeneratorElementTransform
{
    public Vector2 Position { get; set; }
    public float RotationDegrees { get; set; }
    public float LengthScale { get; set; } = 1.0f;
    public float WidthScale { get; set; } = 1.0f;

    public Vector2 TransformPoint(Vector2 localPoint)
    {
        return Position + (localPoint * LengthScale).Rotated(Mathf.DegToRad(RotationDegrees));
    }

    public Vector2 InverseTransformPoint(Vector2 documentPoint)
    {
        float safeScale = Mathf.Max(LengthScale, 0.0001f);
        return (documentPoint - Position).Rotated(-Mathf.DegToRad(RotationDegrees)) / safeScale;
    }

    public Vector2 TransformVector(Vector2 localVector)
    {
        return (localVector * LengthScale).Rotated(Mathf.DegToRad(RotationDegrees));
    }

    public TextureGeneratorElementTransform Clone()
    {
        return new TextureGeneratorElementTransform
        {
            Position = Position,
            RotationDegrees = RotationDegrees,
            LengthScale = LengthScale,
            WidthScale = WidthScale
        };
    }
}
