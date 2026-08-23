using System.Collections.Generic;
using Godot;

public static class PolyTextureBakeService
{
    public static Image BakeScalar(PolyTextureItem texture, PolyTextureOutputBinding output, int widthPx, int heightPx)
    {
        int width = Mathf.Max(1, widthPx);
        int height = Mathf.Max(1, heightPx);
        Image image = Image.CreateEmpty(width, height, false, Image.Format.L8);
        image.Fill(Colors.Black);
        if (texture == null || output == null || !output.Enabled
            || texture.DomainWidthCm <= 0.0f || texture.DomainHeightCm <= 0.0f)
        {
            return image;
        }

        PolyTextureEvaluationResult evaluation = PolyTextureEvaluator.Evaluate(texture);
        float value = Mathf.Clamp(output.Value, 0.0f, 1.0f);
        foreach (string sourceId in output.SourceElementIds)
        {
            foreach (List<Vector2> polygon in evaluation.GetGeometry(sourceId))
            {
                RasterizePolygon(image, polygon, texture.DomainWidthCm, texture.DomainHeightCm, value);
            }
        }
        return image;
    }

    public static Image DeriveNormalMap(Image heightImage, PolyTextureItem texture, float heightAmplitudeCm)
    {
        int width = heightImage?.GetWidth() ?? 1;
        int height = heightImage?.GetHeight() ?? 1;
        Image normalImage = Image.CreateEmpty(width, height, false, Image.Format.Rgb8);
        if (heightImage == null || texture == null)
        {
            normalImage.Fill(new Color(0.5f, 0.5f, 1.0f));
            return normalImage;
        }

        float spacingX = texture.DomainWidthCm / Mathf.Max(1, width - 1);
        float spacingY = texture.DomainHeightCm / Mathf.Max(1, height - 1);
        float amplitude = Mathf.Max(0.0001f, heightAmplitudeCm);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float left = heightImage.GetPixel(Mathf.Max(0, x - 1), y).R * amplitude;
                float right = heightImage.GetPixel(Mathf.Min(width - 1, x + 1), y).R * amplitude;
                float up = heightImage.GetPixel(x, Mathf.Max(0, y - 1)).R * amplitude;
                float down = heightImage.GetPixel(x, Mathf.Min(height - 1, y + 1)).R * amplitude;
                float slopeX = (right - left) / (2.0f * spacingX);
                float slopeY = (up - down) / (2.0f * spacingY);
                Vector3 normal = new Vector3(-slopeX, -slopeY, 1.0f).Normalized();
                normalImage.SetPixel(x, y, new Color(normal.X * 0.5f + 0.5f, normal.Y * 0.5f + 0.5f, normal.Z * 0.5f + 0.5f));
            }
        }
        return normalImage;
    }

    public static Error SavePng(string path, Image image)
    {
        if (image == null)
        {
            return Error.InvalidParameter;
        }
        string absolutePath = path.StartsWith("res://", System.StringComparison.Ordinal)
            || path.StartsWith("user://", System.StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : path;
        string directory = absolutePath.GetBaseDir();
        if (!string.IsNullOrEmpty(directory))
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
        }
        return image.SavePng(absolutePath);
    }

    private static void RasterizePolygon(Image image, List<Vector2> polygon, float domainWidthCm, float domainHeightCm, float value)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        List<Vector2> pixels = new(polygon.Count);
        foreach (Vector2 point in polygon)
        {
            pixels.Add(new Vector2(
                point.X / domainWidthCm * image.GetWidth(),
                (domainHeightCm - point.Y) / domainHeightCm * image.GetHeight()));
        }

        int minY = image.GetHeight() - 1;
        int maxY = 0;
        foreach (Vector2 point in pixels)
        {
            minY = Mathf.Min(minY, Mathf.FloorToInt(point.Y));
            maxY = Mathf.Max(maxY, Mathf.CeilToInt(point.Y));
        }
        minY = Mathf.Clamp(minY, 0, image.GetHeight() - 1);
        maxY = Mathf.Clamp(maxY, 0, image.GetHeight() - 1);

        List<float> intersections = new();
        for (int y = minY; y <= maxY; y++)
        {
            float scanY = y + 0.5f;
            intersections.Clear();
            for (int index = 0; index < pixels.Count; index++)
            {
                Vector2 a = pixels[index];
                Vector2 b = pixels[(index + 1) % pixels.Count];
                if ((a.Y <= scanY && b.Y > scanY) || (b.Y <= scanY && a.Y > scanY))
                {
                    intersections.Add(a.X + (scanY - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
            }
            intersections.Sort();
            for (int pairIndex = 0; pairIndex + 1 < intersections.Count; pairIndex += 2)
            {
                int startX = Mathf.Clamp(Mathf.CeilToInt(intersections[pairIndex] - 0.5f), 0, image.GetWidth() - 1);
                int endX = Mathf.Clamp(Mathf.FloorToInt(intersections[pairIndex + 1] - 0.5f), 0, image.GetWidth() - 1);
                for (int x = startX; x <= endX; x++)
                {
                    float current = image.GetPixel(x, y).R;
                    float next = Mathf.Max(current, value);
                    image.SetPixel(x, y, new Color(next, next, next));
                }
            }
        }
    }
}
