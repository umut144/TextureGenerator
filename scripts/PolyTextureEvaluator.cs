using System.Collections.Generic;
using Godot;

public static class PolyTextureEvaluator
{
    public static PolyTextureEvaluationResult Evaluate(PolyTextureItem texture)
    {
        PolyTextureEvaluationResult result = new();
        if (texture == null)
        {
            return result;
        }

        foreach (PolyTextureElement operation in texture.Elements)
        {
            if (!operation.Enabled)
            {
                continue;
            }

            if (operation is RectangleRegionElement rectangleRegion)
            {
                List<Vector2> polygon = PolyTextureRenderer.BuildRectangleRegion(rectangleRegion);
                result.GeometryByElementId[operation.Id] = polygon.Count >= 3
                    ? new List<List<Vector2>> { polygon }
                    : new List<List<Vector2>>();
            }
            else if (operation is CenterStrokeElement centerStroke)
            {
                List<Vector2> polygon = PolyTextureRenderer.BuildFilledPolygon(centerStroke);
                result.GeometryByElementId[operation.Id] = polygon.Count >= 3
                    ? new List<List<Vector2>> { polygon }
                    : new List<List<Vector2>>();
            }
            else if (operation is SweepGeneratorElement sweep)
            {
                result.HiddenSourceIds.Add(sweep.SourceElementId);
                List<List<Vector2>> polygons = PolyTextureRenderer.BuildSweepPolygons(texture, sweep);
                if (sweep.RenderSource)
                {
                    polygons.InsertRange(0, result.GetGeometry(sweep.SourceElementId));
                }
                result.GeometryByElementId[sweep.Id] = polygons;
                result.LiveSweeps.Add(sweep);
            }
            else if (operation is RepeatGridGeneratorElement repeat)
            {
                result.HiddenSourceIds.Add(repeat.SourceElementId);
                List<List<Vector2>> polygons = PolyTextureRenderer.BuildRepeatGridPolygons(result.GetGeometry(repeat.SourceElementId), repeat);
                result.GeometryByElementId[repeat.Id] = polygons;
                result.LiveRepeatGrids.Add(repeat);
            }
            else if (operation is MirrorGeneratorElement mirror)
            {
                result.HiddenSourceIds.Add(mirror.SourceElementId);
                List<List<Vector2>> sourceGeometry = result.GetGeometry(mirror.SourceElementId);
                List<List<Vector2>> polygons = PolyTextureRenderer.BuildMirrorPolygons(texture, mirror, sourceGeometry);
                if (mirror.RenderSource)
                {
                    polygons.InsertRange(0, sourceGeometry);
                }
                result.GeometryByElementId[mirror.Id] = polygons;
                result.LiveMirrors.Add(mirror);
            }
            else if (operation is InvertFilterElement)
            {
                result.GeometryByElementId[operation.Id] = new List<List<Vector2>>();
            }
        }

        return result;
    }
}
