using Godot;

public sealed class CenterStrokePoint : TextureGeneratorPoint
{
    public float LeftWidth { get; set; }
    public float RightWidth { get; set; }
    public TextureGeneratorHandleMode HandleMode { get; set; } = TextureGeneratorHandleMode.Linear;
    public Vector2 InHandle { get; set; }
    public Vector2 OutHandle { get; set; }

    public override CenterStrokePoint Clone()
    {
        return new CenterStrokePoint
        {
            X = X,
            Y = Y,
            LeftWidth = LeftWidth,
            RightWidth = RightWidth,
            HandleMode = HandleMode,
            InHandle = InHandle,
            OutHandle = OutHandle
        };
    }
}
