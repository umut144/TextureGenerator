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
        Image combined = Image.CreateEmpty(width, height, false, Image.Format.L8);
        combined.Fill(Colors.Black);
        foreach (string sourceId in output.SourceElementIds)
        {
            Image sourceField = EvaluateElementField(texture, evaluation, sourceId, width, height, new HashSet<string>());
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = Mathf.Max(combined.GetPixel(x, y).R, sourceField.GetPixel(x, y).R);
                    combined.SetPixel(x, y, new Color(value, value, value));
                }
            }
        }
        float outputValue = Mathf.Clamp(output.Value, 0.0f, 1.0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = combined.GetPixel(x, y).R * outputValue;
                image.SetPixel(x, y, new Color(value, value, value));
            }
        }
        return image;
    }

    public static Image BakeElementScalar(PolyTextureItem texture, string elementId, int widthPx, int heightPx)
    {
        int width = Mathf.Max(1, widthPx);
        int height = Mathf.Max(1, heightPx);
        Image empty = Image.CreateEmpty(width, height, false, Image.Format.L8);
        empty.Fill(Colors.Black);
        if (texture == null || string.IsNullOrWhiteSpace(elementId)
            || texture.DomainWidthCm <= 0.0f || texture.DomainHeightCm <= 0.0f)
        {
            return empty;
        }
        PolyTextureEvaluationResult evaluation = PolyTextureEvaluator.Evaluate(texture);
        return EvaluateElementField(texture, evaluation, elementId, width, height, new HashSet<string>());
    }

    public static Image CreateScalarDebugPreview(Image scalar)
    {
        int width = scalar?.GetWidth() ?? 1;
        int height = scalar?.GetHeight() ?? 1;
        Image preview = Image.CreateEmpty(width, height, false, Image.Format.Rgb8);
        Color background = new(0.055f, 0.065f, 0.085f);
        preview.Fill(background);
        if (scalar == null)
        {
            return preview;
        }

        float maximum = 0.0f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                maximum = Mathf.Max(maximum, scalar.GetPixel(x, y).R);
            }
        }
        if (maximum <= 0.000001f)
        {
            return preview;
        }

        Color low = new(0.34f, 0.08f, 0.52f);
        Color middle = new(0.04f, 0.78f, 0.92f);
        Color high = new(1.0f, 0.92f, 0.22f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalized = Mathf.Clamp(scalar.GetPixel(x, y).R / maximum, 0.0f, 1.0f);
                if (normalized <= 0.000001f)
                {
                    continue;
                }
                Color color = normalized < 0.5f
                    ? low.Lerp(middle, normalized * 2.0f)
                    : middle.Lerp(high, (normalized - 0.5f) * 2.0f);
                preview.SetPixel(x, y, color);
            }
        }
        return preview;
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

    private static Image EvaluateElementField(PolyTextureItem texture, PolyTextureEvaluationResult evaluation, string elementId, int width, int height, HashSet<string> activeIds)
    {
        Image image = Image.CreateEmpty(width, height, false, Image.Format.L8);
        image.Fill(Colors.Black);
        if (!activeIds.Add(elementId))
        {
            return image;
        }

        PolyTextureElement element = texture.GetElement(elementId);
        if (element is InvertFilterElement invert && invert.Enabled)
        {
            Image source = EvaluateElementField(texture, evaluation, invert.SourceElementId, width, height, activeIds);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = 1.0f - source.GetPixel(x, y).R;
                    image.SetPixel(x, y, new Color(value, value, value));
                }
            }
        }
        else if (element is EdgeFalloffFilterElement falloff && falloff.Enabled)
        {
            Image source = EvaluateElementField(texture, evaluation, falloff.SourceElementId, width, height, activeIds);
            image = ApplyEdgeFalloff(source, texture, falloff.RadiusCm, falloff.Exponent);
        }
        else if (element?.Enabled == true)
        {
            foreach (List<Vector2> polygon in evaluation.GetGeometry(elementId))
            {
                RasterizePolygon(image, polygon, texture.DomainWidthCm, texture.DomainHeightCm, 1.0f);
            }
        }
        activeIds.Remove(elementId);
        return image;
    }

    private static Image ApplyEdgeFalloff(Image source, PolyTextureItem texture, float radiusCm, float exponent)
    {
        int width = source.GetWidth();
        int height = source.GetHeight();
        Image result = Image.CreateEmpty(width, height, false, Image.Format.L8);
        result.Fill(Colors.Black);
        float radius = Mathf.Max(0.0f, radiusCm);
        if (radius <= 0.0001f)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result.SetPixel(x, y, source.GetPixel(x, y));
                }
            }
            return result;
        }

        float spacingX = texture.DomainWidthCm / Mathf.Max(1, width);
        float spacingY = texture.DomainHeightCm / Mathf.Max(1, height);
        float diagonal = Mathf.Sqrt(spacingX * spacingX + spacingY * spacingY);
        float[] distances = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (source.GetPixel(x, y).R <= 0.0001f)
                {
                    distances[index] = 0.0f;
                    continue;
                }
                float boundaryDistance = Mathf.Min(
                    Mathf.Min((x + 0.5f) * spacingX, (width - x - 0.5f) * spacingX),
                    Mathf.Min((y + 0.5f) * spacingY, (height - y - 0.5f) * spacingY));
                distances[index] = Mathf.Min(boundaryDistance, radius + diagonal);
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (x > 0) distances[index] = Mathf.Min(distances[index], distances[index - 1] + spacingX);
                if (y > 0) distances[index] = Mathf.Min(distances[index], distances[index - width] + spacingY);
                if (x > 0 && y > 0) distances[index] = Mathf.Min(distances[index], distances[index - width - 1] + diagonal);
                if (x + 1 < width && y > 0) distances[index] = Mathf.Min(distances[index], distances[index - width + 1] + diagonal);
            }
        }
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                int index = y * width + x;
                if (x + 1 < width) distances[index] = Mathf.Min(distances[index], distances[index + 1] + spacingX);
                if (y + 1 < height) distances[index] = Mathf.Min(distances[index], distances[index + width] + spacingY);
                if (x + 1 < width && y + 1 < height) distances[index] = Mathf.Min(distances[index], distances[index + width + 1] + diagonal);
                if (x > 0 && y + 1 < height) distances[index] = Mathf.Min(distances[index], distances[index + width - 1] + diagonal);
            }
        }

        float curve = Mathf.Max(0.01f, exponent);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sourceValue = source.GetPixel(x, y).R;
                float value = sourceValue * Mathf.Pow(Mathf.Clamp(distances[y * width + x] / radius, 0.0f, 1.0f), curve);
                result.SetPixel(x, y, new Color(value, value, value));
            }
        }
        return result;
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
