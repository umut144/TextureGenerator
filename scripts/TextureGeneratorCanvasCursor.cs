using Godot;

public sealed class TextureGeneratorCanvasCursor
{
    public bool IsInsideTexture { get; set; }
    public bool SnapEnabled { get; set; }
    public Vector2 DocumentPosition { get; set; }
    public Vector2 DisplayPosition { get; set; }
    public Vector2 SnappedDocumentPosition { get; set; }
    public Vector2 SnappedDisplayPosition { get; set; }
}
