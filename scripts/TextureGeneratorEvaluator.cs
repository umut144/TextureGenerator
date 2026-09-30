using System.Collections.Generic;
using Godot;

public static class TextureGeneratorEvaluator
{
    public static TextureGeneratorEvaluationResult Evaluate(TextureGeneratorItem texture)
    {
        TextureGeneratorEvaluationResult result = new();
        if (texture == null)
        {
            return result;
        }

        foreach (TextureGeneratorElement operation in texture.Elements)
        {
            if (!operation.Enabled)
            {
                continue;
            }

            if (operation is RectangleRegionElement rectangleRegion)
            {
                List<Vector2> polygon = TextureGeneratorRenderer.BuildRectangleRegion(rectangleRegion);
                result.GeometryByElementId[operation.Id] = polygon.Count >= 3
                    ? new List<List<Vector2>> { polygon }
                    : new List<List<Vector2>>();
            }
            else if (operation is EllipseRegionElement ellipseRegion)
            {
                List<Vector2> polygon = TextureGeneratorRenderer.BuildEllipseRegion(ellipseRegion);
                result.GeometryByElementId[operation.Id] = polygon.Count >= 3
                    ? new List<List<Vector2>> { polygon }
                    : new List<List<Vector2>>();
            }
            else if (operation is CenterStrokeElement centerStroke)
            {
                List<Vector2> polygon = TextureGeneratorRenderer.BuildFilledPolygon(centerStroke);
                result.GeometryByElementId[operation.Id] = polygon.Count >= 3
                    ? new List<List<Vector2>> { polygon }
                    : new List<List<Vector2>>();
            }
            else if (operation is SweepGeneratorElement sweep)
            {
                result.HiddenSourceIds.Add(sweep.SourceElementId);
                List<List<Vector2>> polygons = TextureGeneratorRenderer.BuildSweepPolygons(texture, sweep);
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
                List<List<Vector2>> polygons = TextureGeneratorRenderer.BuildRepeatGridPolygons(result.GetGeometry(repeat.SourceElementId), repeat);
                polygons = TextureGeneratorRenderer.ClipPolygonsToDomain(polygons, texture.DomainWidthCm, texture.DomainHeightCm);
                result.GeometryByElementId[repeat.Id] = polygons;
                result.LiveRepeatGrids.Add(repeat);
            }
            else if (operation is BranchGeneratorElement branch)
            {
                result.HiddenSourceIds.Add(branch.SourceElementId);
                List<List<Vector2>> polygons = TextureGeneratorRenderer.BuildBranchPolygons(texture, branch);
                if (branch.RenderSource)
                {
                    polygons.InsertRange(0, result.GetGeometry(branch.SourceElementId));
                }
                polygons = TextureGeneratorRenderer.ClipPolygonsToDomain(polygons, texture.DomainWidthCm, texture.DomainHeightCm);
                result.GeometryByElementId[branch.Id] = polygons;
                result.LiveBranches.Add(branch);
            }
            else if (operation is ScatterGeneratorElement scatter)
            {
                result.HiddenSourceIds.Add(scatter.SourceElementId);
                List<List<Vector2>> bounds = string.IsNullOrEmpty(scatter.BoundsElementId)
                    ? new List<List<Vector2>>()
                    : result.GetGeometry(scatter.BoundsElementId);
                List<List<Vector2>> polygons = TextureGeneratorRenderer.BuildScatterPolygons(
                    result.GetGeometry(scatter.SourceElementId),
                    bounds,
                    texture.DomainWidthCm,
                    texture.DomainHeightCm,
                    scatter);
                if (scatter.RenderSource)
                {
                    polygons.InsertRange(0, result.GetGeometry(scatter.SourceElementId));
                }
                polygons = TextureGeneratorRenderer.ClipPolygonsToDomain(polygons, texture.DomainWidthCm, texture.DomainHeightCm);
                result.GeometryByElementId[scatter.Id] = polygons;
                result.LiveScatters.Add(scatter);
            }
            else if (operation is MirrorGeneratorElement mirror)
            {
                result.HiddenSourceIds.Add(mirror.SourceElementId);
                List<List<Vector2>> sourceGeometry = result.GetGeometry(mirror.SourceElementId);
                List<List<Vector2>> polygons = TextureGeneratorRenderer.BuildMirrorPolygons(texture, mirror, sourceGeometry);
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
            else if (operation is EdgeFalloffFilterElement)
            {
                result.GeometryByElementId[operation.Id] = new List<List<Vector2>>();
            }
        }

        return result;
    }
}
