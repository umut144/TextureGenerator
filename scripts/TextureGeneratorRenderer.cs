using System.Collections.Generic;
using Godot;

public static class TextureGeneratorRenderer
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

    public static List<Vector2> BuildEllipseRegion(EllipseRegionElement region)
    {
        List<Vector2> polygon = new();
        if (region == null || !region.Enabled || region.WidthCm <= 0.0f || region.HeightCm <= 0.0f)
        {
            return polygon;
        }
        const int sampleCount = 48;
        Vector2 radii = new(region.WidthCm * 0.5f, region.HeightCm * 0.5f);
        float rotation = Mathf.DegToRad(region.RotationDegrees);
        for (int sample = 0; sample < sampleCount; sample++)
        {
            float angle = Mathf.Tau * sample / sampleCount;
            Vector2 local = new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
            polygon.Add(region.Position + local.Rotated(rotation));
        }
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

        if (element.SupportsBezierHandles && point.HandleMode != TextureGeneratorHandleMode.Linear && point.OutHandle.LengthSquared() > 0.0001f)
        {
            tangent = point.OutHandle;
        }
        else if (element.SupportsBezierHandles && point.HandleMode != TextureGeneratorHandleMode.Linear && point.InHandle.LengthSquared() > 0.0001f)
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

    public static List<List<Vector2>> BuildSweepPolygons(TextureGeneratorItem texture, SweepGeneratorElement sweep)
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

    public static List<CenterStrokeElement> BuildSweepInstances(TextureGeneratorItem texture, SweepGeneratorElement sweep)
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
            Vector2 direction = sweep.Alignment == TextureGeneratorSweepAlignment.Normal
                ? new Vector2(-tangent.Y, tangent.X)
                : tangent;

            if (sweep.SideMode != TextureGeneratorSweepSideMode.Right)
            {
                AddSweepInstance(instances, source, sweep, anchor, direction, sourceBaseAngle, instanceT);
            }
            if (sweep.SideMode != TextureGeneratorSweepSideMode.Left)
            {
                AddSweepInstance(instances, source, sweep, anchor, -direction, sourceBaseAngle, instanceT);
            }
        }

        return instances;
    }

    public static List<Vector2> BuildMirrorPolygon(TextureGeneratorItem texture, MirrorGeneratorElement mirror)
    {
        CenterStrokeElement mirrored = BuildMirrorElement(texture, mirror);
        return mirrored == null ? new List<Vector2>() : BuildFilledPolygon(mirrored);
    }

    public static List<List<Vector2>> BuildMirrorPolygons(TextureGeneratorItem texture, MirrorGeneratorElement mirror, List<List<Vector2>> sourcePolygons)
    {
        List<List<Vector2>> mirroredPolygons = new();
        TextureGeneratorGuide axis = texture?.GetGuide(mirror?.AxisGuideId ?? string.Empty);
        if (mirror == null || !mirror.Enabled || axis?.Type != TextureGeneratorGuideType.Axis
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

    public static List<List<Vector2>> BuildRepeatGridPolygons(List<List<Vector2>> sourcePolygons, RepeatGridGeneratorElement repeat)
    {
        List<List<Vector2>> result = new();
        if (repeat == null || !repeat.Enabled || repeat.Columns < 1 || repeat.Rows < 1)
        {
            return result;
        }
        for (int row = 0; row < repeat.Rows; row++)
        {
            float rowOffset = row % 2 == 0 ? 0.0f : repeat.AlternateRowOffsetXCm;
            for (int column = 0; column < repeat.Columns; column++)
            {
                Vector2 offset = new(column * repeat.StepXCm + rowOffset, row * repeat.StepYCm);
                foreach (List<Vector2> sourcePolygon in sourcePolygons)
                {
                    List<Vector2> instance = new(sourcePolygon.Count);
                    foreach (Vector2 point in sourcePolygon)
                    {
                        instance.Add(point + offset);
                    }
                    result.Add(instance);
                }
            }
        }
        return result;
    }

    public static List<CenterStrokeElement> BuildBranchStrokes(TextureGeneratorItem texture, BranchGeneratorElement branch)
    {
        List<CenterStrokeElement> result = new();
        if (branch == null || !branch.Enabled || branch.Count < 1 || branch.Segments < 1
            || texture?.GetElement(branch.SourceElementId) is not CrackLineElement source)
        {
            return result;
        }

        List<Vector2> path = BuildCenterPath(source);
        if (path.Count < 2)
        {
            return result;
        }

        uint randomState = unchecked((uint)branch.Seed) ^ 0x9E3779B9u;
        int derivedIndex = 0;
        List<CenterStrokeElement> generation = AppendBranchGeneration(
            result,
            path,
            AverageStrokeWidth(source) * source.Transform.WidthScale,
            branch.Count,
            1.0f,
            branch,
            ref randomState,
            ref derivedIndex);

        for (int depth = 2; depth <= branch.Depth; depth++)
        {
            List<CenterStrokeElement> nextGeneration = new();
            float lengthScale = Mathf.Pow(branch.DepthLengthScale, depth - 1);
            foreach (CenterStrokeElement parent in generation)
            {
                List<CenterStrokeElement> children = AppendBranchGeneration(
                    result,
                    BuildCenterPath(parent),
                    AverageStrokeWidth(parent),
                    branch.ChildrenPerBranch,
                    lengthScale,
                    branch,
                    ref randomState,
                    ref derivedIndex);
                nextGeneration.AddRange(children);
            }
            generation = nextGeneration;
            if (generation.Count == 0 || result.Count >= 4096)
            {
                break;
            }
        }
        return result;
    }

    private static List<CenterStrokeElement> AppendBranchGeneration(
        List<CenterStrokeElement> allStrokes,
        List<Vector2> path,
        float sourceWidth,
        int count,
        float lengthScale,
        BranchGeneratorElement branch,
        ref uint randomState,
        ref int derivedIndex)
    {
        List<CenterStrokeElement> generation = new();
        if (path.Count < 2 || count < 1)
        {
            return generation;
        }
        List<float> cumulativeLengths = BuildCumulativeLengths(path);
        float totalLength = cumulativeLengths[^1];
        if (totalLength <= 0.0001f)
        {
            return generation;
        }

        for (int branchIndex = 0; branchIndex < count && allStrokes.Count < 4096; branchIndex++)
        {
            float cellT = (branchIndex + 0.5f) / count;
            float baseT = Mathf.Lerp(branch.StartT, branch.EndT, cellT);
            float jitterRange = (branch.EndT - branch.StartT) / count * 0.7f;
            float t = Mathf.Clamp(baseT + (NextRandom(ref randomState) - 0.5f) * jitterRange, branch.StartT, branch.EndT);
            SamplePolyline(path, cumulativeLengths, totalLength, t, out Vector2 anchor, out Vector2 tangent);

            float side = NextRandom(ref randomState) < 0.5f ? -1.0f : 1.0f;
            float angle = Mathf.DegToRad(Mathf.Lerp(branch.AngleMinDegrees, branch.AngleMaxDegrees, NextRandom(ref randomState))) * side;
            float length = Mathf.Lerp(branch.LengthMinCm, branch.LengthMaxCm, NextRandom(ref randomState)) * lengthScale;
            Vector2 direction = tangent.Rotated(angle).Normalized();
            float segmentLength = length / branch.Segments;
            CenterStrokeElement stroke = new()
            {
                Id = $"{branch.Id}_derived_{derivedIndex}",
                Name = $"{branch.Name} {derivedIndex + 1}",
                Symmetry = true,
                Opacity = branch.Opacity
            };
            derivedIndex++;

            Vector2 position = anchor;
            for (int segmentIndex = 0; segmentIndex <= branch.Segments; segmentIndex++)
            {
                float normalized = segmentIndex / (float)branch.Segments;
                float width = Mathf.Max(0.01f, sourceWidth * branch.WidthScale * Mathf.Lerp(1.0f, 0.12f, normalized));
                stroke.Points.Add(new CenterStrokePoint
                {
                    X = position.X,
                    Y = position.Y,
                    LeftWidth = width,
                    RightWidth = width,
                    HandleMode = TextureGeneratorHandleMode.Linear
                });
                if (segmentIndex < branch.Segments)
                {
                    float bendDegrees = (NextRandom(ref randomState) * 2.0f - 1.0f) * branch.Irregularity * 45.0f;
                    direction = direction.Rotated(Mathf.DegToRad(bendDegrees)).Normalized();
                    position += direction * segmentLength;
                }
            }
            allStrokes.Add(stroke);
            generation.Add(stroke);
        }
        return generation;
    }

    public static List<List<Vector2>> BuildBranchPolygons(TextureGeneratorItem texture, BranchGeneratorElement branch)
    {
        List<List<Vector2>> polygons = new();
        foreach (CenterStrokeElement stroke in BuildBranchStrokes(texture, branch))
        {
            List<Vector2> polygon = BuildFilledPolygon(stroke);
            if (polygon.Count >= 3)
            {
                polygons.Add(polygon);
            }
        }
        return polygons;
    }

    public static List<List<Vector2>> BuildScatterPolygons(
        List<List<Vector2>> sourcePolygons,
        List<List<Vector2>> boundsPolygons,
        float domainWidthCm,
        float domainHeightCm,
        ScatterGeneratorElement scatter)
    {
        List<List<Vector2>> result = new();
        if (scatter == null || !scatter.Enabled || scatter.Count < 1 || sourcePolygons.Count == 0
            || domainWidthCm <= 0.0f || domainHeightCm <= 0.0f)
        {
            return result;
        }

        Rect2 sourceBounds = GetPolygonBounds(sourcePolygons, new Rect2(Vector2.Zero, Vector2.One));
        Vector2 pivot = sourceBounds.Position + sourceBounds.Size * 0.5f;
        Rect2 sampleBounds = boundsPolygons.Count > 0
            ? GetPolygonBounds(boundsPolygons, new Rect2(Vector2.Zero, new Vector2(domainWidthCm, domainHeightCm)))
            : new Rect2(Vector2.Zero, new Vector2(domainWidthCm, domainHeightCm));
        uint randomState = unchecked((uint)scatter.Seed) ^ 0x85EBCA6Bu;
        List<Vector2> clusterCenters = new();
        for (int index = 0; index < scatter.ClusterCount; index++)
        {
            if (TrySampleUniformPoint(sampleBounds, boundsPolygons, ref randomState, out Vector2 center))
            {
                clusterCenters.Add(center);
            }
        }

        for (int instanceIndex = 0; instanceIndex < scatter.Count; instanceIndex++)
        {
            Vector2 position;
            bool useCluster = clusterCenters.Count > 0 && NextRandom(ref randomState) < scatter.ClusterStrength;
            bool sampled = useCluster
                ? TrySampleClusterPoint(clusterCenters, scatter.ClusterRadiusCm, sampleBounds, boundsPolygons, ref randomState, out position)
                : TrySampleUniformPoint(sampleBounds, boundsPolygons, ref randomState, out position);
            if (!sampled && !TrySampleUniformPoint(sampleBounds, boundsPolygons, ref randomState, out position))
            {
                continue;
            }

            float scale = Mathf.Lerp(scatter.ScaleMin, scatter.ScaleMax, NextRandom(ref randomState));
            float rotation = Mathf.DegToRad(Mathf.Lerp(scatter.RotationMinDegrees, scatter.RotationMaxDegrees, NextRandom(ref randomState)));
            foreach (List<Vector2> sourcePolygon in sourcePolygons)
            {
                List<Vector2> instance = new(sourcePolygon.Count);
                foreach (Vector2 point in sourcePolygon)
                {
                    instance.Add(position + ((point - pivot) * scale).Rotated(rotation));
                }
                if (instance.Count >= 3)
                {
                    result.Add(instance);
                }
            }
        }
        return result;
    }

    public static List<List<Vector2>> ClipPolygonsToDomain(List<List<Vector2>> polygons, float widthCm, float heightCm)
    {
        List<List<Vector2>> clipped = new();
        foreach (List<Vector2> polygon in polygons)
        {
            List<Vector2> result = new(polygon);
            result = ClipAgainstBoundary(result, point => point.X >= 0.0f, (start, end) => IntersectVertical(start, end, 0.0f));
            result = ClipAgainstBoundary(result, point => point.X <= widthCm, (start, end) => IntersectVertical(start, end, widthCm));
            result = ClipAgainstBoundary(result, point => point.Y >= 0.0f, (start, end) => IntersectHorizontal(start, end, 0.0f));
            result = ClipAgainstBoundary(result, point => point.Y <= heightCm, (start, end) => IntersectHorizontal(start, end, heightCm));
            if (result.Count >= 3)
            {
                clipped.Add(result);
            }
        }
        return clipped;
    }

    public static CenterStrokeElement BuildMirrorElement(TextureGeneratorItem texture, MirrorGeneratorElement mirror)
    {
        if (texture == null || mirror == null || !mirror.Enabled)
        {
            return null;
        }

        CenterStrokeElement source = texture.GetElement(mirror.SourceElementId) as CenterStrokeElement;
        TextureGeneratorGuide axis = texture.GetGuide(mirror.AxisGuideId);
        if (source == null || axis?.Type != TextureGeneratorGuideType.Axis || (axis.AxisEnd - axis.Position).LengthSquared() < 0.0001f)
        {
            return null;
        }

        CenterStrokeElement mirrored = (CenterStrokeElement)source.Clone();
        mirrored.Transform = new TextureGeneratorElementTransform();
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

    private static List<float> BuildCumulativeLengths(List<Vector2> path)
    {
        List<float> lengths = new(path.Count) { 0.0f };
        for (int index = 1; index < path.Count; index++)
        {
            lengths.Add(lengths[^1] + path[index - 1].DistanceTo(path[index]));
        }
        return lengths;
    }

    private static Rect2 GetPolygonBounds(List<List<Vector2>> polygons, Rect2 fallback)
    {
        Vector2 minimum = new(float.MaxValue, float.MaxValue);
        Vector2 maximum = new(float.MinValue, float.MinValue);
        bool hasPoint = false;
        foreach (List<Vector2> polygon in polygons)
        {
            foreach (Vector2 point in polygon)
            {
                minimum = new Vector2(Mathf.Min(minimum.X, point.X), Mathf.Min(minimum.Y, point.Y));
                maximum = new Vector2(Mathf.Max(maximum.X, point.X), Mathf.Max(maximum.Y, point.Y));
                hasPoint = true;
            }
        }
        return hasPoint ? new Rect2(minimum, maximum - minimum) : fallback;
    }

    private static bool TrySampleUniformPoint(Rect2 bounds, List<List<Vector2>> boundsPolygons, ref uint randomState, out Vector2 point)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            point = bounds.Position + new Vector2(NextRandom(ref randomState) * bounds.Size.X, NextRandom(ref randomState) * bounds.Size.Y);
            if (boundsPolygons.Count == 0 || IsPointInAnyPolygon(point, boundsPolygons))
            {
                return true;
            }
        }
        point = Vector2.Zero;
        return false;
    }

    private static bool TrySampleClusterPoint(List<Vector2> centers, float radiusCm, Rect2 bounds, List<List<Vector2>> boundsPolygons, ref uint randomState, out Vector2 point)
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            int centerIndex = Mathf.Min(centers.Count - 1, Mathf.FloorToInt(NextRandom(ref randomState) * centers.Count));
            float angle = NextRandom(ref randomState) * Mathf.Tau;
            float distance = Mathf.Sqrt(NextRandom(ref randomState)) * Mathf.Max(0.0f, radiusCm);
            point = centers[centerIndex] + Vector2.Right.Rotated(angle) * distance;
            if (bounds.HasPoint(point) && (boundsPolygons.Count == 0 || IsPointInAnyPolygon(point, boundsPolygons)))
            {
                return true;
            }
        }
        point = Vector2.Zero;
        return false;
    }

    private static bool IsPointInAnyPolygon(Vector2 point, List<List<Vector2>> polygons)
    {
        foreach (List<Vector2> polygon in polygons)
        {
            bool inside = false;
            for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
            {
                Vector2 currentPoint = polygon[index];
                Vector2 previousPoint = polygon[previous];
                bool crosses = (currentPoint.Y > point.Y) != (previousPoint.Y > point.Y)
                    && point.X < (previousPoint.X - currentPoint.X) * (point.Y - currentPoint.Y)
                        / (previousPoint.Y - currentPoint.Y) + currentPoint.X;
                if (crosses)
                {
                    inside = !inside;
                }
            }
            if (inside)
            {
                return true;
            }
        }
        return false;
    }

    private static void SamplePolyline(List<Vector2> path, List<float> cumulativeLengths, float totalLength, float t, out Vector2 position, out Vector2 tangent)
    {
        float targetLength = Mathf.Clamp(t, 0.0f, 1.0f) * totalLength;
        int segmentIndex = 0;
        while (segmentIndex + 1 < cumulativeLengths.Count - 1 && cumulativeLengths[segmentIndex + 1] < targetLength)
        {
            segmentIndex++;
        }
        float segmentLength = cumulativeLengths[segmentIndex + 1] - cumulativeLengths[segmentIndex];
        float segmentT = segmentLength <= 0.0001f ? 0.0f : (targetLength - cumulativeLengths[segmentIndex]) / segmentLength;
        position = path[segmentIndex].Lerp(path[segmentIndex + 1], segmentT);
        tangent = path[segmentIndex + 1] - path[segmentIndex];
        tangent = tangent.LengthSquared() < 0.0001f ? Vector2.Up : tangent.Normalized();
    }

    private static float AverageStrokeWidth(CenterStrokeElement stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return 1.0f;
        }
        float sum = 0.0f;
        foreach (CenterStrokePoint point in stroke.Points)
        {
            sum += (point.LeftWidth + point.RightWidth) * 0.5f;
        }
        return Mathf.Max(0.01f, sum / stroke.Points.Count);
    }

    private static float NextRandom(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0x00FFFFFFu) / 16777216.0f;
    }

    private static List<Vector2> ClipAgainstBoundary(List<Vector2> polygon, System.Func<Vector2, bool> isInside, System.Func<Vector2, Vector2, Vector2> intersection)
    {
        List<Vector2> result = new();
        if (polygon.Count == 0)
        {
            return result;
        }
        Vector2 previous = polygon[^1];
        bool previousInside = isInside(previous);
        foreach (Vector2 current in polygon)
        {
            bool currentInside = isInside(current);
            if (currentInside != previousInside)
            {
                result.Add(intersection(previous, current));
            }
            if (currentInside)
            {
                result.Add(current);
            }
            previous = current;
            previousInside = currentInside;
        }
        return result;
    }

    private static Vector2 IntersectVertical(Vector2 start, Vector2 end, float x)
    {
        float delta = end.X - start.X;
        float t = Mathf.Abs(delta) < 0.000001f ? 0.0f : (x - start.X) / delta;
        return new Vector2(x, Mathf.Lerp(start.Y, end.Y, t));
    }

    private static Vector2 IntersectHorizontal(Vector2 start, Vector2 end, float y)
    {
        float delta = end.Y - start.Y;
        float t = Mathf.Abs(delta) < 0.000001f ? 0.0f : (y - start.Y) / delta;
        return new Vector2(Mathf.Lerp(start.X, end.X, t), y);
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

    private static Vector2 ReflectAcrossAxis(Vector2 point, TextureGeneratorGuide axis)
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
            || (start.HandleMode == TextureGeneratorHandleMode.Linear && end.HandleMode == TextureGeneratorHandleMode.Linear)
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

        if (start.HandleMode != TextureGeneratorHandleMode.Linear)
        {
            p1 += start.OutHandle;
        }

        if (end.HandleMode != TextureGeneratorHandleMode.Linear)
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
