using Godot;

public abstract class PolyTexturePoint
{
    public float X { get; set; }
    public float Y { get; set; }

    public Vector2 Position
    {
        get => new(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public abstract PolyTexturePoint Clone();
}
