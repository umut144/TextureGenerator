using Godot;

public static class TextureGeneratorSurfaceService
{
    public static bool SetDomainSize(TextureGeneratorItem texture, float widthCm, float heightCm)
    {
        if (texture == null)
        {
            return false;
        }

        float nextWidth = Mathf.Max(0.01f, widthCm);
        float nextHeight = Mathf.Max(0.01f, heightCm);
        if (Mathf.IsEqualApprox(texture.DomainWidthCm, nextWidth)
            && Mathf.IsEqualApprox(texture.DomainHeightCm, nextHeight))
        {
            return false;
        }

        texture.DomainWidthCm = nextWidth;
        texture.DomainHeightCm = nextHeight;
        return true;
    }

    public static bool SetPreviewResolution(TextureGeneratorItem texture, int widthPx, int heightPx)
    {
        if (texture == null)
        {
            return false;
        }

        int nextWidth = Mathf.Max(1, widthPx);
        int nextHeight = Mathf.Max(1, heightPx);
        if (texture.PreviewWidthPx == nextWidth && texture.PreviewHeightPx == nextHeight)
        {
            return false;
        }

        texture.PreviewWidthPx = nextWidth;
        texture.PreviewHeightPx = nextHeight;
        return true;
    }
}
