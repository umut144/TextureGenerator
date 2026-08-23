using Godot;

public sealed class CenterStrokePoint : PolyTexturePoint
{
    public float LeftWidth { get; set; }
    public float RightWidth { get; set; }
    public PolyTextureHandleMode HandleMode { get; set; } = PolyTextureHandleMode.Linear;
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
