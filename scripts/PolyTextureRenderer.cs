using System.Collections.Generic;
using Godot;

public static class PolyTextureRenderer
{
    private const int SamplesPerSegment = 16;
    private const int RoundedCornerSamples = 6;

    public static List<Vector2> BuildRectangleRegion(RectangleRegionElement region)
    {
        List<Vector2> polygon = new();
        if (region == null || !region.Enabled || region.WidthCm <= 0.0f || region.HeightCm <= 0.0f)
        {
            return polygon;
        }

        float halfWidth = region.WidthCm * 0.5f;
        float halfHeight = region.HeightCm * 0.5f;
        float radius = Mathf.Clamp(region.CornerRadiusCm, 0.0f, Mathf.Min(halfWidth, halfHeight));
        if (radius <= 0.0001f)
        {
            polygon.Add(TransformRegionPoint(region, new Vector2(halfWidth, halfHeight)));
            polygon.Add(TransformRegionPoint(region, new Vector2(-halfWidth, halfHeight)));
            polygon.Add(TransformRegionPoint(region, new Vector2(-halfWidth, -halfHeight)));
            polygon.Add(TransformRegionPoint(region, new Vector2(halfWidth, -halfHeight)));
            return polygon;
        }

        AddRoundedCorner(polygon, region, new Vector2(halfWidth - radius, halfHeight - radius), radius, 0.0f);
        AddRoundedCorner(polygon, region, new Vector2(-halfWidth + radius, halfHeight - radius), radius, 90.0f);
        AddRoundedCorner(polygon, region, new Vector2(-halfWidth + radius, -halfHeight + radius), radius, 180.0f);
        AddRoundedCorner(polygon, region, new Vector2(halfWidth - radius, -halfHeight + radius), radius, 270.0f);
        return polygon;
    }

    public static List<Vector2> BuildFilledPolygon(CenterStrokeElement element)
    {
        List<Vector2> polygon = new();
        if (element == null || !element.Enabled || element.Points.Count < 2)
        {
            return polygon;
        }

        List<Vector2> leftEdge = new();
        List<Vector2> rightEdge = new();
        BuildEdges(element, leftEdge, rightEdge);

        polygon.AddRange(leftEdge);
        for (int pointIndex = rightEdge.Count - 1; pointIndex >= 0; pointIndex--)
        {
            polygon.Add(rightEdge[pointIndex]);
        }

        return polygon;
    }

    public static List<Vector2> BuildCenterPath(CenterStrokeElement element)
    {
        List<Vector2> path = new();
        if (element == null || element.Points.Count == 0)
        {
            return path;
        }

        if (element.Points.Count == 1)
        {
            path.Add(element.Transform.TransformPoint(element.Points[0].Position));
            return path;
        }

        for (int segmentIndex = 0; segmentIndex < element.Points.Count - 1; segmentIndex++)
        {
            for (int sampleIndex = segmentIndex == 0 ? 0 : 1; sampleIndex <= SamplesPerSegment; sampleIndex++)
            {
                float t = sampleIndex / (float)SamplesPerSegment;
                Vector2 localPosition = EvaluateSegment(element, segmentIndex, t, out _);
                path.Add(element.Transform.TransformPoint(localPosition));
            }
        }

        return path;
    }

    public static Vector2 GetNormal(CenterStrokeElement element, int pointIndex)
    {
        Vector2 tangent = GetTangent(element, pointIndex);
        return new Vector2(-tangent.Y, tangent.X).Normalized();
    }

    public static Vector2 GetTangent(CenterStrokeElement element, int pointIndex)
    {
        if (element == null || element.Points.Count < 2)
        {
            return Vector2.Up;
        }

        int clampedIndex = Mathf.Clamp(pointIndex, 0, element.Points.Count - 1);
        CenterStrokePoint point = element.Points[clampedIndex];
        Vector2 tangent = Vector2.Zero;

        if (element.SupportsBezierHandles && point.HandleMode != PolyTextureHandleMode.Linear && point.OutHandle.LengthSquared() > 0.0001f)
        {
            tangent = point.OutHandle;
        }
        else if (element.SupportsBezierHandles && point.HandleMode != PolyTextureHandleMode.Linear && point.InHandle.LengthSquared() > 0.0001f)
        {
            tangent = -point.InHandle;
        }

        if (tangent.LengthSquared() < 0.0001f)
        {
            int previousIndex = Mathf.Max(0, clampedIndex - 1);
            int nextIndex = Mathf.Min(element.Points.Count - 1, clampedIndex + 1);
            tangent = element.Points[nextIndex].Position - element.Points[previousIndex].Position;
        }

        if (tangent.LengthSquared() < 0.0001f)
        {
            tangent = Vector2.Up;
        }

        return element.Transform.TransformVector(tangent).Normalized();
    }

    public static List<List<Vector2>> BuildSweepPolygons(PolyTextureItem texture, SweepGeneratorElement sweep)
    {
        List<List<Vector2>> polygons = new();
        foreach (CenterStrokeElement instance in BuildSweepInstances(texture, sweep))
        {
            List<Vector2> polygon = BuildFilledPolygon(instance);
            if (polygon.Count >= 3)
            {
                polygons.Add(polygon);
            }
        }

        return polygons;
    }

    public static List<CenterStrokeElement> BuildSweepInstances(PolyTextureItem texture, SweepGeneratorElement sweep)
    {
        List<CenterStrokeElement> instances = new();
        if (texture == null || sweep == null || !sweep.Enabled || sweep.Count < 1)
        {
            return instances;
        }

        CenterStrokeElement source = texture.GetElement(sweep.SourceElementId) as CenterStrokeElement;
        CenterStrokeElement target = texture.GetElement(sweep.TargetElementId) as CenterStrokeElement;
        if (source == null || target == null || source.Points.Count < 2 || target.Points.Count < 2)
        {
            return instances;
        }

        List<Vector2> targetPath = BuildCenterPath(target);
        if (targetPath.Count < 2)
        {
            return instances;
        }

        float sourceBaseAngle = GetSourceBaseAngle(source);
        for (int instanceIndex = 0; instanceIndex < sweep.Count; instanceIndex++)
        {
            float instanceT = sweep.Count == 1 ? 0.0f : instanceIndex / (float)(sweep.Count - 1);
            float pathT = Mathf.Lerp(sweep.StartT, sweep.EndT, instanceT);
            Vector2 anchor = SamplePolylineByArcLength(targetPath, pathT, out Vector2 tangent);
            Vector2 direction = sweep.Alignment == PolyTextureSweepAlignment.Normal
                ? new Vector2(-tangent.Y, tangent.X)
                : tangent;

            if (sweep.SideMode != PolyTextureSweepSideMode.Right)
            {
                AddSweepInstance(instances, source, sweep, anchor, direction, sourceBaseAngle, instanceT);
            }
            if (sweep.SideMode != PolyTextureSweepSideMode.Left)
            {
                AddSweepInstance(instances, source, sweep, anchor, -direction, sourceBaseAngle, instanceT);
            }
        }

        return instances;
    }

    public static List<Vector2> BuildMirrorPolygon(PolyTextureItem texture, MirrorGeneratorElement mirror)
    {
        CenterStrokeElement mirrored = BuildMirrorElement(texture, mirror);
        return mirrored == null ? new List<Vector2>() : BuildFilledPolygon(mirrored);
    }

    public static List<List<Vector2>> BuildMirrorPolygons(PolyTextureItem texture, MirrorGeneratorElement mirror, List<List<Vector2>> sourcePolygons)
    {
        List<List<Vector2>> mirroredPolygons = new();
        PolyTextureGuide axis = texture?.GetGuide(mirror?.AxisGuideId ?? string.Empty);
        if (mirror == null || !mirror.Enabled || axis?.Type != PolyTextureGuideType.Axis
            || (axis.AxisEnd - axis.Position).LengthSquared() < 0.0001f)
        {
            return mirroredPolygons;
        }

        foreach (List<Vector2> sourcePolygon in sourcePolygons)
        {
            List<Vector2> mirroredPolygon = new(sourcePolygon.Count);
            foreach (Vector2 point in sourcePolygon)
            {
                mirroredPolygon.Add(ReflectAcrossAxis(point, axis));
            }
            if (mirroredPolygon.Count >= 3)
            {
                mirroredPolygons.Add(mirroredPolygon);
            }
        }
        return mirroredPolygons;
    }

    public static CenterStrokeElement BuildMirrorElement(PolyTextureItem texture, MirrorGeneratorElement mirror)
    {
        if (texture == null || mirror == null || !mirror.Enabled)
        {
            return null;
        }

        CenterStrokeElement source = texture.GetElement(mirror.SourceElementId) as CenterStrokeElement;
        PolyTextureGuide axis = texture.GetGuide(mirror.AxisGuideId);
        if (source == null || axis?.Type != PolyTextureGuideType.Axis || (axis.AxisEnd - axis.Position).LengthSquared() < 0.0001f)
        {
            return null;
        }

        CenterStrokeElement mirrored = (CenterStrokeElement)source.Clone();
        mirrored.Transform = new PolyTextureElementTransform();
        for (int pointIndex = 0; pointIndex < mirrored.Points.Count; pointIndex++)
        {
            CenterStrokePoint sourcePoint = source.Points[pointIndex];
            CenterStrokePoint mirroredPoint = mirrored.Points[pointIndex];
            Vector2 sourcePosition = source.Transform.TransformPoint(sourcePoint.Position);
            Vector2 mirroredPosition = ReflectAcrossAxis(sourcePosition, axis);
            mirroredPoint.Position = mirroredPosition;
            // A reflection reverses the local handedness, so the visual left and right widths exchange sides.
            mirroredPoint.LeftWidth = sourcePoint.RightWidth * source.Transform.WidthScale;
            mirroredPoint.RightWidth = sourcePoint.LeftWidth * source.Transform.WidthScale;

            if (source.SupportsBezierHandles)
            {
                Vector2 inEnd = source.Transform.TransformPoint(sourcePoint.Position + sourcePoint.InHandle);
                Vector2 outEnd = source.Transform.TransformPoint(sourcePoint.Position + sourcePoint.OutHandle);
                mirroredPoint.InHandle = ReflectAcrossAxis(inEnd, axis) - mirroredPosition;
                mirroredPoint.OutHandle = ReflectAcrossAxis(outEnd, axis) - mirroredPosition;
            }
        }
        mirrored.MakePointsLocal();
        return mirrored;
    }

    private static void AddSweepInstance(List<CenterStrokeElement> instances, CenterStrokeElement source, SweepGeneratorElement sweep, Vector2 anchor, Vector2 direction, float sourceBaseAngle, float instanceT)
    {
        CenterStrokeElement instance = (CenterStrokeElement)source.Clone();
        float directionAngle = Mathf.RadToDeg(direction.Angle());
        instance.Transform.Position = anchor;
        instance.Transform.RotationDegrees = directionAngle - sourceBaseAngle + source.Transform.RotationDegrees + sweep.RotationOffsetDegrees;
        instance.Transform.LengthScale = source.Transform.LengthScale * Mathf.Lerp(sweep.LengthScaleStart, sweep.LengthScaleEnd, instanceT);
        instance.Transform.WidthScale = source.Transform.WidthScale * Mathf.Lerp(sweep.WidthScaleStart, sweep.WidthScaleEnd, instanceT);
        instances.Add(instance);
    }

    private static void AddRoundedCorner(List<Vector2> polygon, RectangleRegionElement region, Vector2 center, float radius, float startDegrees)
    {
        for (int sample = 0; sample <= RoundedCornerSamples; sample++)
        {
            float angle = Mathf.DegToRad(startDegrees + sample / (float)RoundedCornerSamples * 90.0f);
            Vector2 localPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            polygon.Add(TransformRegionPoint(region, localPoint));
        }
    }

    private static Vector2 TransformRegionPoint(RectangleRegionElement region, Vector2 localPoint)
    {
        return region.Position + localPoint.Rotated(Mathf.DegToRad(region.RotationDegrees));
    }

    private static Vector2 ReflectAcrossAxis(Vector2 point, PolyTextureGuide axis)
    {
        Vector2 axisVector = axis.AxisEnd - axis.Position;
        float axisLengthSquared = axisVector.LengthSquared();
        Vector2 projection = axis.Position + axisVector * ((point - axis.Position).Dot(axisVector) / axisLengthSquared);
        return projection * 2.0f - point;
    }

    private static float GetSourceBaseAngle(CenterStrokeElement source)
    {
        Vector2 direction = source.Points[1].Position - source.Points[0].Position;
        if (direction.LengthSquared() < 0.0001f)
        {
            direction = Vector2.Up;
        }
        return Mathf.RadToDeg(direction.Angle());
    }

    private static Vector2 SamplePolylineByArcLength(List<Vector2> path, float normalizedDistance, out Vector2 tangent)
    {
        float totalLength = 0.0f;
        for (int index = 0; index < path.Count - 1; index++)
        {
            totalLength += path[index].DistanceTo(path[index + 1]);
        }

        float targetDistance = Mathf.Clamp(normalizedDistance, 0.0f, 1.0f) * totalLength;
        float traversed = 0.0f;
        for (int index = 0; index < path.Count - 1; index++)
        {
            Vector2 segment = path[index + 1] - path[index];
            float segmentLength = segment.Length();
            if (segmentLength < 0.0001f)
            {
                continue;
            }

            if (traversed + segmentLength >= targetDistance)
            {
                float segmentT = (targetDistance - traversed) / segmentLength;
                tangent = segment / segmentLength;
                return path[index].Lerp(path[index + 1], segmentT);
            }
            traversed += segmentLength;
        }

        tangent = (path[^1] - path[^2]).Normalized();
        return path[^1];
    }

    private static void BuildEdges(CenterStrokeElement element, List<Vector2> leftEdge, List<Vector2> rightEdge)
    {
        int segmentCount = element.Points.Count - 1;
        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            for (int sampleIndex = segmentIndex == 0 ? 0 : 1; sampleIndex <= SamplesPerSegment; sampleIndex++)
            {
                float segmentT = sampleIndex / (float)SamplesPerSegment;
                float normalizedT = (segmentIndex + segmentT) / segmentCount;
                Vector2 localPosition = EvaluateSegment(element, segmentIndex, segmentT, out Vector2 localTangent);
                Vector2 position = element.Transform.TransformPoint(localPosition);
                Vector2 tangent = element.Transform.TransformVector(localTangent).Normalized();
                Vector2 normal = new Vector2(-tangent.Y, tangent.X).Normalized();
                CenterStrokePoint start = element.Points[segmentIndex];
                CenterStrokePoint end = element.Points[segmentIndex + 1];
                float leftWidth = Mathf.Lerp(start.LeftWidth, end.LeftWidth, segmentT) * element.Transform.WidthScale;
                float rightWidth = Mathf.Lerp(start.RightWidth, end.RightWidth, segmentT) * element.Transform.WidthScale;
                float falloffFactor = GetFalloffFactor(element, normalizedT);
                leftEdge.Add(position + normal * leftWidth * falloffFactor);
                rightEdge.Add(position - normal * rightWidth * falloffFactor);
            }
        }
    }

    private static Vector2 EvaluateSegment(CenterStrokeElement element, int segmentIndex, float t, out Vector2 tangent)
    {
        CenterStrokePoint start = element.Points[segmentIndex];
        CenterStrokePoint end = element.Points[segmentIndex + 1];

        if (!element.SupportsBezierHandles
            || (start.HandleMode == PolyTextureHandleMode.Linear && end.HandleMode == PolyTextureHandleMode.Linear)
            || (start.OutHandle.LengthSquared() < 0.0001f && end.InHandle.LengthSquared() < 0.0001f))
        {
            tangent = end.Position - start.Position;
            if (tangent.LengthSquared() < 0.0001f)
            {
                tangent = Vector2.Up;
            }

            tangent = tangent.Normalized();
            return start.Position.Lerp(end.Position, t);
        }

        Vector2 p0 = start.Position;
        Vector2 p1 = start.Position;
        Vector2 p2 = end.Position;
        Vector2 p3 = end.Position;

        if (start.HandleMode != PolyTextureHandleMode.Linear)
        {
            p1 += start.OutHandle;
        }

        if (end.HandleMode != PolyTextureHandleMode.Linear)
        {
            p2 += end.InHandle;
        }

        float inverse = 1.0f - t;
        Vector2 position = inverse * inverse * inverse * p0
            + 3.0f * inverse * inverse * t * p1
            + 3.0f * inverse * t * t * p2
            + t * t * t * p3;

        tangent = 3.0f * inverse * inverse * (p1 - p0)
            + 6.0f * inverse * t * (p2 - p1)
            + 3.0f * t * t * (p3 - p2);
        if (tangent.LengthSquared() < 0.0001f)
        {
            tangent = p3 - p0;
        }

        if (tangent.LengthSquared() < 0.0001f)
        {
            tangent = Vector2.Up;
        }

        tangent = tangent.Normalized();
        return position;
    }

    private static float GetFalloffFactor(CenterStrokeElement element, float normalizedT)
    {
        if (element.Points.Count <= 1 || element.Falloff <= 0.0f)
        {
            return 1.0f;
        }

        float arch = Mathf.Sin(normalizedT * Mathf.Pi);
        return Mathf.Pow(Mathf.Clamp(arch, 0.0f, 1.0f), element.Falloff);
    }
}
