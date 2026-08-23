using Godot;
using System;
using System.Collections.Generic;

public partial class PolyTextureCanvasView : Control
{
    private const float CanvasPadding = 32.0f;
    private const float MinZoom = 0.1f;
    private const float MaxZoom = 32.0f;
    private const float PanPixelsPerSecond = 921.6f;
    private const float ZoomStepsPerSecond = 5.5f;
    private const float PointRadius = 6.0f;
    private const float PointHitRadius = 12.0f;
    private const float HandleRadius = 5.0f;
    private const float HandleHitRadius = 12.0f;
    private const float TransformGizmoAxisLength = 42.0f;
    private const float MinorGridStep = 12.5f;
    private const float MajorGridStep = 50.0f;

    private enum DragTarget
    {
        None,
        Point,
        LeftHandle,
        RightHandle,
        InHandle,
        OutHandle,
        GuidePoint,
        GuideAxisStart,
        GuideAxisEnd
    }

    private PolyTextureDocument _document;
    private Rect2 _canvasRect = new(0, 0, 1, 1);
    private float _scale = 1.0f;
    private float _fitScale = 1.0f;
    private float _zoom = 1.0f;
    private Vector2 _viewCenterDocument;
    private Vector2 _lastMouseScreenPosition;
    private int _selectedPointIndex;
    private DragTarget _dragTarget = DragTarget.None;
    private PolyTextureCanvasCursor _cursor = new();
    private bool _drawingMode;
    private bool _pointPlacementMode;
    private bool _hasPointPlacementPreview;
    private Vector2 _pointPlacementPreview;
    private PolyTextureHandleView _handleView = PolyTextureHandleView.Width;
    private bool _elementSelectionMode;
    private SweepGeneratorElement _sweepDraft;
    private bool _guideDrawingMode;
    private PolyTextureGuide _guideAxisPreview;
    private bool _guideSelectionMode;
    private MirrorGeneratorElement _mirrorDraft;
    private ImageTexture _outputPreviewTexture;

    public event Action<int> SelectedPointChanged;
    public event Action<Vector2> DrawPointRequested;
    public event Action<Vector2> PointPlacementConfirmed;
    public event Action<string> ElementSelectedOnCanvas;
    public event Action<Vector2> GuidePointRequested;
    public event Action<string> GuideSelectedOnCanvas;
    public event Action EditStarted;
    public event Action EditFinished;
    public event Action DocumentChanged;
    public event Action<PolyTextureCanvasCursor> CursorChanged;

    public int SelectedPointIndex
    {
        get => _document?.SelectedPointIndex ?? _selectedPointIndex;
        set
        {
            int nextValue = Mathf.Clamp(value, -1, (ActiveCenterStroke?.Points.Count ?? 0) - 1);
            if (_selectedPointIndex == nextValue)
            {
                return;
            }

            _selectedPointIndex = nextValue;
            if (_document != null)
            {
                _document.SelectedPointIndex = nextValue;
                _document.SelectionKind = nextValue >= 0 ? PolyTextureSelectionKind.Point : PolyTextureSelectionKind.Element;
            }
            SelectedPointChanged?.Invoke(_selectedPointIndex);
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.Click;
    }

    public override void _Process(double delta)
    {
        if (_document?.ActiveTexture == null)
        {
            return;
        }

        if (IsTextInputFocused())
        {
            return;
        }

        _lastMouseScreenPosition = GetLocalMousePosition();
        bool viewChanged = ApplyKeyboardFitView() | ApplyKeyboardPan((float)delta) | ApplyKeyboardZoom((float)delta);
        if (viewChanged)
        {
            UpdateCursor(_lastMouseScreenPosition);
            QueueRedraw();
        }
    }

    public void SetDocument(PolyTextureDocument document)
    {
        bool isSameDocument = ReferenceEquals(_document, document);
        _document = document;
        if (!isSameDocument)
        {
            FitView();
        }
        _selectedPointIndex = document?.SelectedPointIndex ?? -1;
        QueueRedraw();
    }

    public void FitView()
    {
        if (_document?.ActiveTexture == null)
        {
            return;
        }

        _zoom = 1.0f;
        _viewCenterDocument = new Vector2(ActiveTextureWidth * 0.5f, ActiveTextureHeight * 0.5f);
        UpdateCanvasRect();
        QueueRedraw();
    }

    public void SelectPoint(int pointIndex)
    {
        SelectedPointIndex = pointIndex;
    }

    public void SetDrawingMode(bool enabled)
    {
        _drawingMode = enabled;
        _dragTarget = DragTarget.None;
        QueueRedraw();
    }

    public void SetPointPlacementMode(bool enabled)
    {
        _pointPlacementMode = enabled;
        _hasPointPlacementPreview = false;
        _dragTarget = DragTarget.None;
        UpdatePointPlacementPreview(_lastMouseScreenPosition);
        QueueRedraw();
    }

    public void SetElementSelectionMode(bool enabled)
    {
        _elementSelectionMode = enabled;
        _dragTarget = DragTarget.None;
        QueueRedraw();
    }

    public void SetSweepDraft(SweepGeneratorElement sweepDraft)
    {
        _sweepDraft = sweepDraft;
        QueueRedraw();
    }

    public void SetGuideDrawingMode(bool enabled)
    {
        _guideDrawingMode = enabled;
        _guideAxisPreview = null;
        _dragTarget = DragTarget.None;
        QueueRedraw();
    }

    public void SetGuideAxisPreview(PolyTextureGuide guide)
    {
        _guideAxisPreview = guide;
        QueueRedraw();
    }

    public void SetGuideSelectionMode(bool enabled)
    {
        _guideSelectionMode = enabled;
        _dragTarget = DragTarget.None;
        QueueRedraw();
    }

    public void SetMirrorDraft(MirrorGeneratorElement mirrorDraft)
    {
        _mirrorDraft = mirrorDraft;
        QueueRedraw();
    }

    public void SetOutputPreview(Image image)
    {
        _outputPreviewTexture = image == null ? null : ImageTexture.CreateFromImage(image);
        QueueRedraw();
    }

    public void SetHandleView(PolyTextureHandleView handleView)
    {
        _handleView = handleView;
        _dragTarget = DragTarget.None;
        QueueRedraw();
    }

    public void RefreshCursor()
    {
        if (_document?.ActiveTexture == null)
        {
            return;
        }

        UpdateCursor(_lastMouseScreenPosition);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_document?.ActiveTexture == null)
        {
            return;
        }

        if (inputEvent is InputEventMouseMotion motion)
        {
            _lastMouseScreenPosition = motion.Position;
            UpdateCursor(motion.Position);
            if (_pointPlacementMode)
            {
                UpdatePointPlacementPreview(motion.Position);
            }
            if (_guideDrawingMode && _guideAxisPreview != null)
            {
                _guideAxisPreview.AxisEnd = SnapDocumentPosition(ClampDocumentPosition(ScreenToDocument(motion.Position)));
            }
            QueueRedraw();

            if (_dragTarget != DragTarget.None)
            {
                if (IsGuideDragTarget(_dragTarget))
                {
                    ApplyGuideDrag(_cursor.DocumentPosition);
                }
                else if (_selectedPointIndex >= 0)
                {
                    ApplyDrag(_cursor.DocumentPosition);
                }
                DocumentChanged?.Invoke();
                QueueRedraw();
            }
        }
        else if (inputEvent is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
        {
            _lastMouseScreenPosition = button.Position;
            if (button.Pressed)
            {
                GrabFocus();
                if (_guideDrawingMode)
                {
                    UpdateCanvasRect();
                    if (_canvasRect.HasPoint(button.Position))
                    {
                        GuidePointRequested?.Invoke(SnapDocumentPosition(ClampDocumentPosition(ScreenToDocument(button.Position))));
                    }
                    return;
                }

                if (_guideSelectionMode)
                {
                    if (TryHitGuide(button.Position, out string guideId))
                    {
                        GuideSelectedOnCanvas?.Invoke(guideId);
                    }
                    return;
                }

                if (_elementSelectionMode)
                {
                    if (TryHitDrawElement(button.Position, out string elementId))
                    {
                        ElementSelectedOnCanvas?.Invoke(elementId);
                    }
                    return;
                }

                if (_pointPlacementMode)
                {
                    if (_hasPointPlacementPreview)
                    {
                        PointPlacementConfirmed?.Invoke(_pointPlacementPreview);
                    }

                    return;
                }

                if (_drawingMode)
                {
                    UpdateCanvasRect();
                    if (_canvasRect.HasPoint(button.Position))
                    {
                        Vector2 documentPosition = ClampDocumentPosition(ScreenToDocument(button.Position));
                        DrawPointRequested?.Invoke(SnapDocumentPosition(documentPosition));
                    }

                    return;
                }

                BeginDrag(button.Position);
            }
            else
            {
                if (_dragTarget != DragTarget.None)
                {
                    EditFinished?.Invoke();
                }
                _dragTarget = DragTarget.None;
            }
        }
    }

    public override void _Draw()
    {
        if (_document?.ActiveTexture == null)
        {
            return;
        }

        UpdateCanvasRect();
        DrawRect(_canvasRect, Colors.White, filled: true);
        if (_outputPreviewTexture != null)
        {
            DrawTextureRect(_outputPreviewTexture, _canvasRect, tile: false);
        }
        else
        {
            DrawGridOverlay();
        }
        DrawRect(_canvasRect, new Color(0.74f, 0.76f, 0.78f), filled: false, width: 1.0f);

        if (_outputPreviewTexture == null)
        {
            foreach (PolyTextureItem texture in _document.Textures)
            {
                if (texture.Visible)
                {
                    DrawTexturePreview(texture, texture == _document.ActiveTexture);
                    DrawGuides(texture, texture == _document.ActiveTexture);
                }
            }
        }

        if (_sweepDraft != null)
        {
            DrawSweepPreview(_document.ActiveTexture, _sweepDraft, active: true, isDraft: true);
        }
        if (_mirrorDraft != null)
        {
            DrawMirrorPreview(_document.ActiveTexture, _mirrorDraft, isDraft: true);
        }
        if (_guideAxisPreview != null)
        {
            DrawGuide(_guideAxisPreview, selected: true, preview: true);
        }

        CenterStrokeElement element = ActiveCenterStroke;
        if (element != null)
        {
            DrawCenterStrokeOverlay(element);
            DrawTransformGizmo(element);
        }

        DrawCursorMarker();
        DrawPointPlacementPreview();
    }

    private void DrawTexturePreview(PolyTextureItem texture, bool active)
    {
        PolyTextureEvaluationResult evaluation = PolyTextureEvaluator.Evaluate(texture);

        foreach (PolyTextureElement element in texture.Elements)
        {
            if (element is RectangleRegionElement rectangleRegion)
            {
                bool elementActive = active && rectangleRegion == _document.ActiveElement;
                if (!evaluation.HiddenSourceIds.Contains(rectangleRegion.Id) || elementActive)
                {
                    DrawRegionPreview(evaluation.GetGeometry(rectangleRegion.Id), rectangleRegion.Opacity, elementActive);
                }
            }
            else if (element is CenterStrokeElement centerStroke)
            {
                bool elementActive = active && centerStroke == _document.ActiveElement;
                if (evaluation.HiddenSourceIds.Contains(centerStroke.Id) && !elementActive)
                {
                    continue;
                }
                bool textureSelected = active && _document.SelectionKind == PolyTextureSelectionKind.Texture;
                DrawCenterStrokePreview(centerStroke, elementActive || textureSelected, textureSelected);
            }
        }

        foreach (SweepGeneratorElement sweep in evaluation.LiveSweeps)
        {
            bool operationActive = active && sweep == _document.ActiveElement;
            if (!evaluation.HiddenSourceIds.Contains(sweep.Id) || operationActive)
            {
                DrawSweepPreview(texture, sweep, operationActive, polygons: evaluation.GetGeometry(sweep.Id));
            }
        }

        foreach (RepeatGridGeneratorElement repeat in evaluation.LiveRepeatGrids)
        {
            bool operationActive = active && repeat == _document.ActiveElement;
            if (!evaluation.HiddenSourceIds.Contains(repeat.Id) || operationActive)
            {
                DrawRegionPreview(evaluation.GetGeometry(repeat.Id), repeat.Opacity, operationActive);
            }
        }

        foreach (BranchGeneratorElement branch in evaluation.LiveBranches)
        {
            bool operationActive = active && branch == _document.ActiveElement;
            if (!evaluation.HiddenSourceIds.Contains(branch.Id) || operationActive)
            {
                DrawRegionPreview(evaluation.GetGeometry(branch.Id), branch.Opacity, operationActive);
            }
        }

        foreach (MirrorGeneratorElement mirror in evaluation.LiveMirrors)
        {
            bool operationActive = active && mirror == _document.ActiveElement;
            if (!evaluation.HiddenSourceIds.Contains(mirror.Id) || operationActive)
            {
                DrawMirrorPreview(texture, mirror, isDraft: false, polygons: evaluation.GetGeometry(mirror.Id));
            }
        }
    }

    private void DrawRegionPreview(List<List<Vector2>> polygons, float opacity, bool active)
    {
        Color fillColor = active
            ? new Color(0.23f, 0.58f, 0.72f, Mathf.Clamp(opacity, 0.0f, 1.0f))
            : new Color(0.36f, 0.63f, 0.72f, Mathf.Clamp(opacity, 0.0f, 1.0f) * 0.72f);
        Color outlineColor = active
            ? new Color(0.08f, 0.28f, 0.42f, 0.9f)
            : new Color(0.1f, 0.3f, 0.42f, 0.55f);
        foreach (List<Vector2> polygon in polygons)
        {
            Vector2[] screenPolygon = new Vector2[polygon.Count];
            for (int index = 0; index < polygon.Count; index++)
            {
                screenPolygon[index] = DocumentToScreen(polygon[index]);
            }
            if (CanFillPolygon(screenPolygon))
            {
                DrawColoredPolygon(screenPolygon, fillColor);
            }
            DrawPolyline(ClosePolyline(screenPolygon), outlineColor, width: active ? 1.8f : 1.0f);
        }
    }

    private void DrawMirrorPreview(PolyTextureItem texture, MirrorGeneratorElement mirror, bool isDraft, List<List<Vector2>> polygons = null)
    {
        if (polygons == null)
        {
            PolyTextureEvaluationResult evaluation = PolyTextureEvaluator.Evaluate(texture);
            List<List<Vector2>> sourceGeometry = evaluation.GetGeometry(mirror.SourceElementId);
            polygons = PolyTextureRenderer.BuildMirrorPolygons(texture, mirror, sourceGeometry);
            if (mirror.RenderSource)
            {
                polygons.InsertRange(0, sourceGeometry);
            }
        }

        float opacity = Mathf.Clamp(mirror.Opacity, 0.0f, 1.0f);
        Color fillColor = isDraft
            ? new Color(0.95f, 0.7f, 0.16f, opacity * 0.48f)
            : new Color(0.28f, 0.56f, 0.38f, opacity);
        Color outlineColor = isDraft
            ? new Color(0.82f, 0.46f, 0.08f, 0.92f)
            : new Color(0.1f, 0.32f, 0.2f, 0.68f);
        foreach (List<Vector2> polygon in polygons)
        {
            Vector2[] screenPolygon = new Vector2[polygon.Count];
            for (int index = 0; index < polygon.Count; index++)
            {
                screenPolygon[index] = DocumentToScreen(polygon[index]);
            }
            if (CanFillPolygon(screenPolygon))
            {
                DrawColoredPolygon(screenPolygon, fillColor);
            }
            DrawPolyline(ClosePolyline(screenPolygon), outlineColor, width: isDraft ? 1.8f : 1.2f);
        }
    }

    private void DrawSweepPreview(PolyTextureItem texture, SweepGeneratorElement sweep, bool active, bool isDraft = false, List<List<Vector2>> polygons = null)
    {
        float opacity = Mathf.Clamp(sweep.Opacity, 0.0f, 1.0f) * (active ? 1.0f : 0.72f);
        Color fillColor = isDraft
            ? new Color(0.95f, 0.7f, 0.16f, opacity * 0.48f)
            : active
                ? new Color(0.18f, 0.58f, 0.42f, opacity)
                : new Color(0.34f, 0.62f, 0.46f, opacity);
        Color outlineColor = isDraft
            ? new Color(0.82f, 0.46f, 0.08f, 0.92f)
            : new Color(0.1f, 0.32f, 0.2f, active ? 0.82f : 0.48f);

        foreach (List<Vector2> polygon in polygons ?? PolyTextureRenderer.BuildSweepPolygons(texture, sweep))
        {
            Vector2[] screenPolygon = new Vector2[polygon.Count];
            for (int index = 0; index < polygon.Count; index++)
            {
                screenPolygon[index] = DocumentToScreen(polygon[index]);
            }

            if (CanFillPolygon(screenPolygon))
            {
                DrawColoredPolygon(screenPolygon, fillColor);
            }
            DrawPolyline(ClosePolyline(screenPolygon), outlineColor, width: active ? 1.6f : 1.0f);
        }
    }

    private void DrawGuides(PolyTextureItem texture, bool active)
    {
        foreach (PolyTextureGuide guide in texture.Guides)
        {
            bool selected = active
                && _document.SelectionKind == PolyTextureSelectionKind.Guide
                && guide.Id.Equals(_document.ActiveGuideId, StringComparison.Ordinal);
            DrawGuide(guide, selected, preview: false);
        }
    }

    private void DrawGuide(PolyTextureGuide guide, bool selected, bool preview)
    {
        Color color = preview
            ? new Color(0.98f, 0.66f, 0.1f, 0.65f)
            : selected
                ? new Color(1.0f, 0.58f, 0.08f, 0.98f)
                : new Color(0.94f, 0.48f, 0.06f, 0.82f);
        if (guide.Type == PolyTextureGuideType.Axis)
        {
            DrawDashedLine(DocumentToScreen(guide.Position), DocumentToScreen(guide.AxisEnd), color, selected ? 2.4f : 1.8f);
            DrawCircle(DocumentToScreen(guide.Position), 3.5f, color);
            DrawCircle(DocumentToScreen(guide.AxisEnd), 3.5f, color);
            return;
        }

        Vector2 center = DocumentToScreen(guide.Position);
        DrawCircle(center, 3.5f, color);
        DrawDashedRing(center, 10.0f, color, selected ? 2.0f : 1.5f);
    }

    private void DrawDashedLine(Vector2 start, Vector2 end, Color color, float width)
    {
        float length = start.DistanceTo(end);
        if (length < 0.0001f)
        {
            return;
        }

        Vector2 direction = (end - start) / length;
        const float dashLength = 8.0f;
        const float gapLength = 5.0f;
        for (float distance = 0.0f; distance < length; distance += dashLength + gapLength)
        {
            float dashEnd = Mathf.Min(distance + dashLength, length);
            DrawLine(start + direction * distance, start + direction * dashEnd, color, width);
        }
    }

    private void DrawDashedRing(Vector2 center, float radius, Color color, float width)
    {
        const int segmentCount = 16;
        for (int index = 0; index < segmentCount; index += 2)
        {
            float startAngle = Mathf.Tau * index / segmentCount;
            float endAngle = Mathf.Tau * (index + 1) / segmentCount;
            DrawArc(center, radius, startAngle, endAngle, 6, color, width, antialiased: true);
        }
    }

    private void DrawCenterStrokePreview(CenterStrokeElement element, bool active, bool textureSelected)
    {
        System.Collections.Generic.List<Vector2> polygon = PolyTextureRenderer.BuildFilledPolygon(element);
        if (polygon.Count < 3)
        {
            return;
        }

        Vector2[] screenPolygon = new Vector2[polygon.Count];
        int screenPointIndex = 0;
        foreach (Vector2 point in polygon)
        {
            screenPolygon[screenPointIndex] = DocumentToScreen(point);
            screenPointIndex++;
        }

        float previewOpacity = Mathf.Clamp(element.Opacity, 0.0f, 1.0f) * (active ? 1.0f : 0.58f);
        Color fillColor = active
            ? new Color(0.22f, 0.62f, 0.28f, previewOpacity)
            : new Color(0.38f, 0.66f, 0.44f, previewOpacity);
        Color outlineColor = active
            ? new Color(0.13f, 0.35f, 0.19f, 0.8f)
            : new Color(0.13f, 0.35f, 0.19f, 0.42f);

        if (CanFillPolygon(screenPolygon))
        {
            DrawColoredPolygon(screenPolygon, fillColor);
        }

        DrawPolyline(ClosePolyline(screenPolygon), outlineColor, width: textureSelected ? 2.5f : active ? 1.8f : 1.0f);
    }

    private static bool CanFillPolygon(Vector2[] polygon)
    {
        if (polygon.Length < 3)
        {
            return false;
        }

        foreach (Vector2 point in polygon)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            {
                return false;
            }
        }

        int[] indices = Geometry2D.TriangulatePolygon(polygon);
        return indices.Length >= 3;
    }

    private bool TryHitDrawElement(Vector2 screenPosition, out string elementId)
    {
        elementId = string.Empty;
        PolyTextureItem texture = _document?.ActiveTexture;
        if (texture == null)
        {
            return false;
        }

        for (int elementIndex = texture.Elements.Count - 1; elementIndex >= 0; elementIndex--)
        {
            PolyTextureElement candidate = texture.Elements[elementIndex];
            if (!candidate.Enabled)
            {
                continue;
            }

            if (candidate is RectangleRegionElement rectangleRegion)
            {
                List<Vector2> regionPolygon = PolyTextureRenderer.BuildRectangleRegion(rectangleRegion);
                Vector2[] screenRegion = new Vector2[regionPolygon.Count];
                for (int pointIndex = 0; pointIndex < regionPolygon.Count; pointIndex++)
                {
                    screenRegion[pointIndex] = DocumentToScreen(regionPolygon[pointIndex]);
                }
                if (screenRegion.Length >= 3 && Geometry2D.IsPointInPolygon(screenPosition, screenRegion))
                {
                    elementId = rectangleRegion.Id;
                    return true;
                }
                continue;
            }

            if (candidate is not CenterStrokeElement element)
            {
                continue;
            }

            List<Vector2> polygon = PolyTextureRenderer.BuildFilledPolygon(element);
            if (polygon.Count >= 3)
            {
                Vector2[] screenPolygon = new Vector2[polygon.Count];
                for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
                {
                    screenPolygon[pointIndex] = DocumentToScreen(polygon[pointIndex]);
                }
                if (Geometry2D.IsPointInPolygon(screenPosition, screenPolygon))
                {
                    elementId = element.Id;
                    return true;
                }
            }

            List<Vector2> path = PolyTextureRenderer.BuildCenterPath(element);
            for (int pointIndex = 0; pointIndex < path.Count; pointIndex++)
            {
                if (screenPosition.DistanceTo(DocumentToScreen(path[pointIndex])) <= PointHitRadius)
                {
                    elementId = element.Id;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryHitGuide(Vector2 screenPosition, out string guideId)
    {
        guideId = string.Empty;
        PolyTextureItem texture = _document?.ActiveTexture;
        if (texture == null)
        {
            return false;
        }

        foreach (PolyTextureGuide guide in texture.Guides)
        {
            if (guide.Type != PolyTextureGuideType.Axis)
            {
                continue;
            }

            Vector2 start = DocumentToScreen(guide.Position);
            Vector2 end = DocumentToScreen(guide.AxisEnd);
            Vector2 segment = end - start;
            float segmentLengthSquared = segment.LengthSquared();
            float t = segmentLengthSquared < 0.0001f
                ? 0.0f
                : Mathf.Clamp((screenPosition - start).Dot(segment) / segmentLengthSquared, 0.0f, 1.0f);
            if (screenPosition.DistanceTo(start.Lerp(end, t)) <= 12.0f)
            {
                guideId = guide.Id;
                return true;
            }
        }
        return false;
    }

    private void BeginDrag(Vector2 screenPosition)
    {
        if (_document?.SelectionKind == PolyTextureSelectionKind.Guide && _document.ActiveGuide != null)
        {
            DragTarget guideTarget = HitTestActiveGuide(screenPosition);
            if (guideTarget != DragTarget.None)
            {
                _dragTarget = guideTarget;
                EditStarted?.Invoke();
                QueueRedraw();
            }
            return;
        }

        CenterStrokeElement element = ActiveCenterStroke;
        if (element == null)
        {
            return;
        }

        DragTarget hitTarget = HitTest(screenPosition, out int pointIndex);
        if (hitTarget == DragTarget.None)
        {
            return;
        }

        _selectedPointIndex = pointIndex;
        _document.SelectedPointIndex = pointIndex;
        _document.SelectionKind = PolyTextureSelectionKind.Point;
        _dragTarget = hitTarget;
        EditStarted?.Invoke();
        SelectedPointChanged?.Invoke(_selectedPointIndex);
        QueueRedraw();
    }

    private DragTarget HitTestActiveGuide(Vector2 screenPosition)
    {
        PolyTextureGuide guide = _document?.ActiveGuide;
        if (guide == null)
        {
            return DragTarget.None;
        }

        if (guide.Type == PolyTextureGuideType.Point)
        {
            return screenPosition.DistanceTo(DocumentToScreen(guide.Position)) <= HandleHitRadius
                ? DragTarget.GuidePoint
                : DragTarget.None;
        }

        if (screenPosition.DistanceTo(DocumentToScreen(guide.Position)) <= HandleHitRadius)
        {
            return DragTarget.GuideAxisStart;
        }
        return screenPosition.DistanceTo(DocumentToScreen(guide.AxisEnd)) <= HandleHitRadius
            ? DragTarget.GuideAxisEnd
            : DragTarget.None;
    }

    private void ApplyGuideDrag(Vector2 documentPosition)
    {
        PolyTextureGuide guide = _document?.ActiveGuide;
        if (guide == null)
        {
            return;
        }

        Vector2 snappedPosition = SnapDocumentPosition(ClampDocumentPosition(documentPosition));
        if (_dragTarget == DragTarget.GuidePoint || _dragTarget == DragTarget.GuideAxisStart)
        {
            guide.Position = snappedPosition;
        }
        else if (_dragTarget == DragTarget.GuideAxisEnd)
        {
            guide.AxisEnd = snappedPosition;
        }
    }

    private static bool IsGuideDragTarget(DragTarget target)
    {
        return target is DragTarget.GuidePoint or DragTarget.GuideAxisStart or DragTarget.GuideAxisEnd;
    }

    private DragTarget HitTest(Vector2 screenPosition, out int pointIndex)
    {
        pointIndex = -1;
        CenterStrokeElement element = ActiveCenterStroke;
        if (element == null)
        {
            return DragTarget.None;
        }

        if (element.SupportsBezierHandles
            && _handleView == PolyTextureHandleView.Bezier
            && _selectedPointIndex >= 0
            && _selectedPointIndex < element.Points.Count)
        {
            CenterStrokePoint selectedPoint = element.Points[_selectedPointIndex];
            if (selectedPoint.HandleMode != PolyTextureHandleMode.Linear
                && selectedPoint.InHandle.LengthSquared() > 0.0001f
                && screenPosition.DistanceTo(DocumentToScreen(element.Transform.TransformPoint(selectedPoint.Position + selectedPoint.InHandle))) <= HandleHitRadius)
            {
                pointIndex = _selectedPointIndex;
                return DragTarget.InHandle;
            }

            if (selectedPoint.HandleMode != PolyTextureHandleMode.Linear
                && selectedPoint.OutHandle.LengthSquared() > 0.0001f
                && screenPosition.DistanceTo(DocumentToScreen(element.Transform.TransformPoint(selectedPoint.Position + selectedPoint.OutHandle))) <= HandleHitRadius)
            {
                pointIndex = _selectedPointIndex;
                return DragTarget.OutHandle;
            }

        }

        if (_handleView == PolyTextureHandleView.Width)
        {
            for (int index = 0; index < element.Points.Count; index++)
            {
                CenterStrokePoint point = element.Points[index];
                Vector2 normal = PolyTextureRenderer.GetNormal(element, index);
                Vector2 center = element.Transform.TransformPoint(point.Position);
                Vector2 leftHandle = DocumentToScreen(center + normal * point.LeftWidth * element.Transform.WidthScale);
                Vector2 rightHandle = DocumentToScreen(center - normal * point.RightWidth * element.Transform.WidthScale);

                if (screenPosition.DistanceTo(leftHandle) <= HandleHitRadius)
                {
                    pointIndex = index;
                    return DragTarget.LeftHandle;
                }

                if (screenPosition.DistanceTo(rightHandle) <= HandleHitRadius)
                {
                    pointIndex = index;
                    return DragTarget.RightHandle;
                }
            }
        }

        for (int index = 0; index < element.Points.Count; index++)
        {
            if (screenPosition.DistanceTo(DocumentToScreen(element.Transform.TransformPoint(element.Points[index].Position))) <= PointHitRadius)
            {
                pointIndex = index;
                return DragTarget.Point;
            }
        }

        return DragTarget.None;
    }

    private void ApplyDrag(Vector2 documentPosition)
    {
        CenterStrokeElement element = ActiveCenterStroke;
        if (element == null)
        {
            return;
        }
        CenterStrokePoint point = element.Points[_selectedPointIndex];

        if (_dragTarget == DragTarget.Point)
        {
            Vector2 snappedDocumentPosition = SnapDocumentPosition(ClampDocumentPosition(documentPosition));
            point.Position = element.Transform.InverseTransformPoint(snappedDocumentPosition);
            element.NormalizeLocalAxes();
            return;
        }

        if (_dragTarget == DragTarget.InHandle || _dragTarget == DragTarget.OutHandle)
        {
            ApplyBezierHandleDrag(element, point, documentPosition, _dragTarget == DragTarget.InHandle);
            return;
        }

        Vector2 normal = PolyTextureRenderer.GetNormal(element, _selectedPointIndex);
        Vector2 pointDocumentPosition = element.Transform.TransformPoint(point.Position);
        float signedDistance = (documentPosition - pointDocumentPosition).Dot(normal);
        float width = SnapDistance(Mathf.Max(0.0f, Mathf.Abs(signedDistance) / Mathf.Max(element.Transform.WidthScale, 0.0001f)));

        if (_dragTarget == DragTarget.LeftHandle)
        {
            point.LeftWidth = width;
            if (element.Symmetry)
            {
                point.RightWidth = width;
            }
        }
        else if (_dragTarget == DragTarget.RightHandle)
        {
            point.RightWidth = width;
            if (element.Symmetry)
            {
                point.LeftWidth = width;
            }
        }
    }

    private static void ApplyBezierHandleDrag(CenterStrokeElement element, CenterStrokePoint point, Vector2 documentPosition, bool isInHandle)
    {
        Vector2 handle = element.Transform.InverseTransformPoint(documentPosition) - point.Position;
        if (isInHandle)
        {
            point.InHandle = handle;
            if (point.HandleMode is PolyTextureHandleMode.Aligned or PolyTextureHandleMode.Mirrored)
            {
                float oppositeLength = point.HandleMode == PolyTextureHandleMode.Mirrored
                    ? handle.Length()
                    : Mathf.Max(point.OutHandle.Length(), handle.Length());
                point.OutHandle = handle.LengthSquared() < 0.0001f
                    ? Vector2.Zero
                    : -handle.Normalized() * oppositeLength;
            }
        }
        else
        {
            point.OutHandle = handle;
            if (point.HandleMode is PolyTextureHandleMode.Aligned or PolyTextureHandleMode.Mirrored)
            {
                float oppositeLength = point.HandleMode == PolyTextureHandleMode.Mirrored
                    ? handle.Length()
                    : Mathf.Max(point.InHandle.Length(), handle.Length());
                point.InHandle = handle.LengthSquared() < 0.0001f
                    ? Vector2.Zero
                    : -handle.Normalized() * oppositeLength;
            }
        }
    }

    private Vector2 ClampDocumentPosition(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.X, 0.0f, ActiveTextureWidth),
            Mathf.Clamp(position.Y, 0.0f, ActiveTextureHeight));
    }

    private Vector2 SnapDocumentPosition(Vector2 position)
    {
        if (!_document.SnapEnabled)
        {
            return position;
        }

        Vector2 origin = GetOriginDocumentPosition();
        float step = Mathf.Max(0.0001f, _document.SnapStepCm);
        Vector2 offset = position - origin;
        Vector2 snapped = origin + new Vector2(SnapValue(offset.X, step), SnapValue(offset.Y, step));
        return ClampDocumentPosition(snapped);
    }

    private float SnapDistance(float value)
    {
        if (!_document.SnapEnabled)
        {
            return value;
        }

        return Mathf.Max(0.0f, SnapValue(value, Mathf.Max(0.0001f, _document.SnapStepCm)));
    }

    private static float SnapValue(float value, float step)
    {
        return Mathf.Round(value / step) * step;
    }

    private void UpdateCursor(Vector2 screenPosition)
    {
        UpdateCanvasRect();
        Vector2 documentPosition = ScreenToDocument(screenPosition);
        Vector2 clampedPosition = ClampDocumentPosition(documentPosition);
        Vector2 snappedPosition = SnapDocumentPosition(clampedPosition);
        _cursor = new PolyTextureCanvasCursor
        {
            IsInsideTexture = _canvasRect.HasPoint(screenPosition),
            SnapEnabled = _document.SnapEnabled,
            DocumentPosition = clampedPosition,
            DisplayPosition = ToDisplayPosition(clampedPosition),
            SnappedDocumentPosition = snappedPosition,
            SnappedDisplayPosition = ToDisplayPosition(snappedPosition)
        };
        CursorChanged?.Invoke(_cursor);
    }

    private void DrawCenterStrokeOverlay(CenterStrokeElement element)
    {
        if (element.Points.Count == 0)
        {
            return;
        }

        List<Vector2> path = PolyTextureRenderer.BuildCenterPath(element);
        Vector2[] centerLine = new Vector2[path.Count];
        for (int pointIndex = 0; pointIndex < path.Count; pointIndex++)
        {
            centerLine[pointIndex] = DocumentToScreen(path[pointIndex]);
        }

        if (centerLine.Length >= 2)
        {
            DrawPolyline(centerLine, new Color(0.1f, 0.22f, 0.72f, 0.72f), width: 2.0f);
        }

        for (int index = 0; index < element.Points.Count; index++)
        {
            CenterStrokePoint point = element.Points[index];
            Vector2 pointDocumentPosition = element.Transform.TransformPoint(point.Position);
            Vector2 center = DocumentToScreen(pointDocumentPosition);
            bool selected = index == _selectedPointIndex;

            if (element.SupportsBezierHandles && _handleView == PolyTextureHandleView.Bezier)
            {
                if (selected)
                {
                    DrawBezierHandles(element, point);
                }
            }
            else if (_handleView == PolyTextureHandleView.Width)
            {
                Vector2 normal = PolyTextureRenderer.GetNormal(element, index);
                Vector2 leftHandle = DocumentToScreen(pointDocumentPosition + normal * point.LeftWidth * element.Transform.WidthScale);
                Vector2 rightHandle = DocumentToScreen(pointDocumentPosition - normal * point.RightWidth * element.Transform.WidthScale);
                DrawLine(leftHandle, rightHandle, new Color(0.18f, 0.18f, 0.18f, 0.6f), width: 1.5f);
                DrawCircle(leftHandle, HandleRadius + 2.0f, new Color(1.0f, 0.98f, 0.72f));
                DrawCircle(rightHandle, HandleRadius + 2.0f, new Color(1.0f, 0.98f, 0.72f));
                DrawCircle(leftHandle, HandleRadius, new Color(0.9f, 0.42f, 0.12f));
                DrawCircle(rightHandle, HandleRadius, new Color(0.9f, 0.42f, 0.12f));
            }

            DrawCircle(center, selected ? PointRadius + 2.0f : PointRadius, selected ? new Color(0.98f, 0.84f, 0.2f) : new Color(0.18f, 0.36f, 0.95f));
        }
    }

    private void DrawBezierHandles(CenterStrokeElement element, CenterStrokePoint point)
    {
        if (point.HandleMode == PolyTextureHandleMode.Linear)
        {
            return;
        }

        Vector2 center = DocumentToScreen(element.Transform.TransformPoint(point.Position));
        Vector2 inHandle = DocumentToScreen(element.Transform.TransformPoint(point.Position + point.InHandle));
        Vector2 outHandle = DocumentToScreen(element.Transform.TransformPoint(point.Position + point.OutHandle));
        Color handleColor = new(0.72f, 0.14f, 0.78f, 0.95f);
        DrawLine(center, inHandle, handleColor, width: 2.0f);
        DrawLine(center, outHandle, handleColor, width: 2.0f);
        DrawCircle(inHandle, HandleRadius + 1.0f, Colors.White);
        DrawCircle(outHandle, HandleRadius + 1.0f, Colors.White);
        DrawCircle(inHandle, HandleRadius, handleColor);
        DrawCircle(outHandle, HandleRadius, handleColor);
    }

    private void DrawTransformGizmo(CenterStrokeElement element)
    {
        Vector2 anchor = DocumentToScreen(element.Transform.Position);
        float rotation = Mathf.DegToRad(element.Transform.RotationDegrees);
        Vector2 xAxisDocument = Vector2.Right.Rotated(rotation);
        Vector2 yAxisDocument = Vector2.Down.Rotated(rotation);
        Vector2 xAxisScreen = DocumentVectorToScreenDirection(xAxisDocument);
        Vector2 yAxisScreen = DocumentVectorToScreenDirection(yAxisDocument);
        Color xColor = new(0.9f, 0.18f, 0.16f, 0.95f);
        Color yColor = new(0.14f, 0.68f, 0.28f, 0.95f);

        DrawLine(anchor, anchor + xAxisScreen * TransformGizmoAxisLength, xColor, width: 2.5f);
        DrawLine(anchor, anchor + yAxisScreen * TransformGizmoAxisLength, yColor, width: 2.5f);
        DrawCircle(anchor + xAxisScreen * TransformGizmoAxisLength, 4.0f, xColor);
        DrawCircle(anchor + yAxisScreen * TransformGizmoAxisLength, 4.0f, yColor);
        DrawCircle(anchor, 6.5f, Colors.White);
        DrawCircle(anchor, 4.5f, new Color(0.12f, 0.14f, 0.18f));
    }

    private Vector2 DocumentVectorToScreenDirection(Vector2 documentVector)
    {
        Vector2 screenVector = new(documentVector.X, -documentVector.Y);
        return screenVector.LengthSquared() < 0.0001f ? Vector2.Right : screenVector.Normalized();
    }

    private void DrawCursorMarker()
    {
        if (!_cursor.IsInsideTexture)
        {
            return;
        }

        Vector2 rawScreen = DocumentToScreen(_cursor.DocumentPosition);
        Color rawColor = new(0.10f, 0.16f, 0.22f, 0.58f);
        DrawLine(rawScreen + new Vector2(-7.0f, 0.0f), rawScreen + new Vector2(7.0f, 0.0f), rawColor, width: 1.2f);
        DrawLine(rawScreen + new Vector2(0.0f, -7.0f), rawScreen + new Vector2(0.0f, 7.0f), rawColor, width: 1.2f);

        if (!_cursor.SnapEnabled)
        {
            return;
        }

        Vector2 snappedScreen = DocumentToScreen(_cursor.SnappedDocumentPosition);
        Color snapColor = new(0.02f, 0.38f, 1.0f, 0.96f);
        Color haloColor = new(0.02f, 0.38f, 1.0f, 0.20f);
        DrawCircle(snappedScreen, 12.0f, haloColor);
        DrawCircle(snappedScreen, 4.5f, snapColor);
        DrawLine(snappedScreen + new Vector2(-16.0f, 0.0f), snappedScreen + new Vector2(16.0f, 0.0f), snapColor, width: 2.0f);
        DrawLine(snappedScreen + new Vector2(0.0f, -16.0f), snappedScreen + new Vector2(0.0f, 16.0f), snapColor, width: 2.0f);
    }

    private void DrawPointPlacementPreview()
    {
        if (!_pointPlacementMode || !_hasPointPlacementPreview)
        {
            return;
        }

        Vector2 screenPosition = DocumentToScreen(_pointPlacementPreview);
        Color previewColor = new(0.78f, 0.12f, 0.82f, 0.98f);
        Color haloColor = new(0.78f, 0.12f, 0.82f, 0.24f);
        DrawCircle(screenPosition, 15.0f, haloColor);
        DrawCircle(screenPosition, 7.0f, Colors.White);
        DrawCircle(screenPosition, 5.0f, previewColor);
        DrawLine(screenPosition + new Vector2(-18.0f, 0.0f), screenPosition + new Vector2(18.0f, 0.0f), previewColor, width: 2.0f);
        DrawLine(screenPosition + new Vector2(0.0f, -18.0f), screenPosition + new Vector2(0.0f, 18.0f), previewColor, width: 2.0f);
    }

    private void UpdatePointPlacementPreview(Vector2 screenPosition)
    {
        _hasPointPlacementPreview = false;
        if (!_pointPlacementMode || !_canvasRect.HasPoint(screenPosition) || ActiveCenterStroke == null)
        {
            return;
        }

        Vector2 documentPosition = ClampDocumentPosition(ScreenToDocument(screenPosition));
        _hasPointPlacementPreview = TryGetClosestCenterStrokePosition(ActiveCenterStroke, documentPosition, out _pointPlacementPreview);
    }

    private static bool TryGetClosestCenterStrokePosition(CenterStrokeElement element, Vector2 position, out Vector2 closestPosition)
    {
        closestPosition = Vector2.Zero;
        if (element.Points.Count == 0)
        {
            return false;
        }

        if (element.Points.Count == 1)
        {
            closestPosition = element.Points[0].Position;
            return true;
        }

        float closestDistanceSquared = float.MaxValue;
        for (int index = 0; index < element.Points.Count - 1; index++)
        {
            Vector2 start = element.Points[index].Position;
            Vector2 end = element.Points[index + 1].Position;
            Vector2 segment = end - start;
            float segmentLengthSquared = segment.LengthSquared();
            float interpolation = segmentLengthSquared <= 0.0001f
                ? 0.0f
                : Mathf.Clamp((position - start).Dot(segment) / segmentLengthSquared, 0.0f, 1.0f);
            Vector2 candidate = start.Lerp(end, interpolation);
            float distanceSquared = candidate.DistanceSquaredTo(position);
            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestPosition = candidate;
            }
        }

        return true;
    }

    private void DrawGridOverlay()
    {
        Vector2 origin = GetOriginDocumentPosition();
        DrawGridLines(origin);
        DrawOriginAxes(origin);
        DrawOriginGizmo(origin);
    }

    private void DrawGridLines(Vector2 origin)
    {
        DrawGridAxisLines(
            origin.X,
            ActiveTextureWidth,
            x => new Vector2(x, 0.0f),
            x => new Vector2(x, ActiveTextureHeight));
        DrawGridAxisLines(
            origin.Y,
            ActiveTextureHeight,
            y => new Vector2(0.0f, y),
            y => new Vector2(ActiveTextureWidth, y));
    }

    private void DrawGridAxisLines(float originCoordinate, float maxCoordinate, Func<float, Vector2> start, Func<float, Vector2> end)
    {
        int minIndex = Mathf.CeilToInt((0.0f - originCoordinate) / MinorGridStep);
        int maxIndex = Mathf.FloorToInt((maxCoordinate - originCoordinate) / MinorGridStep);
        for (int index = minIndex; index <= maxIndex; index++)
        {
            float coordinate = originCoordinate + index * MinorGridStep;
            if (coordinate < 0.0f || coordinate > maxCoordinate || Mathf.IsZeroApprox(index))
            {
                continue;
            }

            bool major = Mathf.PosMod(Mathf.Abs(index), Mathf.RoundToInt(MajorGridStep / MinorGridStep)) == 0;
            Color color = major
                ? new Color(0.56f, 0.62f, 0.68f, 0.34f)
                : new Color(0.58f, 0.64f, 0.70f, 0.18f);
            DrawLine(DocumentToScreen(start(coordinate)), DocumentToScreen(end(coordinate)), color, width: major ? 1.2f : 1.0f);
        }
    }

    private void DrawOriginAxes(Vector2 origin)
    {
        Color axisColor = new(0.18f, 0.28f, 0.38f, 0.58f);
        DrawLine(DocumentToScreen(new Vector2(origin.X, 0.0f)), DocumentToScreen(new Vector2(origin.X, ActiveTextureHeight)), axisColor, width: 1.8f);
        DrawLine(DocumentToScreen(new Vector2(0.0f, origin.Y)), DocumentToScreen(new Vector2(ActiveTextureWidth, origin.Y)), axisColor, width: 1.8f);
    }

    private void DrawOriginGizmo(Vector2 origin)
    {
        Vector2 screen = DocumentToScreen(origin);
        Color fillColor = new(0.08f, 0.18f, 0.28f, 0.92f);
        Color xColor = new(0.84f, 0.16f, 0.16f, 0.92f);
        Color yColor = new(0.12f, 0.52f, 0.22f, 0.92f);
        DrawCircle(screen, 10.0f, Colors.White);
        DrawCircle(screen, 6.5f, fillColor);
        DrawLine(screen, screen + new Vector2(34.0f, 0.0f), xColor, width: 3.4f);
        DrawLine(screen, screen + new Vector2(0.0f, -34.0f), yColor, width: 3.4f);
        DrawCircle(screen + new Vector2(34.0f, 0.0f), 5.0f, xColor);
        DrawCircle(screen + new Vector2(0.0f, -34.0f), 5.0f, yColor);
    }

    private Vector2 GetOriginDocumentPosition()
    {
        PolyTextureItem texture = _document.ActiveTexture;
        return texture.OriginMode switch
        {
            PolyTextureOriginMode.BottomCenter => new Vector2(texture.DomainWidthCm * 0.5f, 0.0f),
            PolyTextureOriginMode.Center => new Vector2(texture.DomainWidthCm * 0.5f, texture.DomainHeightCm * 0.5f),
            _ => Vector2.Zero
        };
    }

    private Vector2 ToDisplayPosition(Vector2 storedPosition)
    {
        PolyTextureItem texture = _document.ActiveTexture;
        return texture.OriginMode switch
        {
            PolyTextureOriginMode.BottomCenter => storedPosition - new Vector2(texture.DomainWidthCm * 0.5f, 0.0f),
            PolyTextureOriginMode.Center => storedPosition - new Vector2(texture.DomainWidthCm * 0.5f, texture.DomainHeightCm * 0.5f),
            _ => storedPosition
        };
    }

    private void UpdateCanvasRect()
    {
        if (_document?.ActiveTexture == null)
        {
            _canvasRect = new Rect2(Size * 0.5f, Vector2.Zero);
            return;
        }

        Vector2 available = Size - new Vector2(CanvasPadding * 2.0f, CanvasPadding * 2.0f);
        float scaleX = available.X / Mathf.Max(1, ActiveTextureWidth);
        float scaleY = available.Y / Mathf.Max(1, ActiveTextureHeight);
        _fitScale = Mathf.Max(0.1f, Mathf.Min(scaleX, scaleY));
        _scale = _fitScale * _zoom;
        Vector2 canvasSize = new(ActiveTextureWidth * _scale, ActiveTextureHeight * _scale);
        Vector2 viewCenterScreen = Size * 0.5f;
        Vector2 topLeft = viewCenterScreen - new Vector2(_viewCenterDocument.X * _scale, (ActiveTextureHeight - _viewCenterDocument.Y) * _scale);
        _canvasRect = new Rect2(topLeft, canvasSize);
    }

    private Vector2 DocumentToScreen(Vector2 documentPosition)
    {
        return _canvasRect.Position + new Vector2(documentPosition.X * _scale, (ActiveTextureHeight - documentPosition.Y) * _scale);
    }

    private Vector2 ScreenToDocument(Vector2 screenPosition)
    {
        UpdateCanvasRect();
        Vector2 local = (screenPosition - _canvasRect.Position) / _scale;
        return new Vector2(local.X, ActiveTextureHeight - local.Y);
    }

    private CenterStrokeElement ActiveCenterStroke => _document?.ActiveCenterStroke;

    private float ActiveTextureWidth => _document?.ActiveTexture?.DomainWidthCm ?? 1.0f;

    private float ActiveTextureHeight => _document?.ActiveTexture?.DomainHeightCm ?? 1.0f;

    private bool ApplyKeyboardPan(float delta)
    {
        if (IsCommandModifierPressed())
        {
            return false;
        }

        Vector2 panDirection = Vector2.Zero;

        if (Input.IsKeyPressed(Key.A))
        {
            panDirection.X -= 1.0f;
        }

        if (Input.IsKeyPressed(Key.D))
        {
            panDirection.X += 1.0f;
        }

        if (Input.IsKeyPressed(Key.W))
        {
            panDirection.Y += 1.0f;
        }

        if (Input.IsKeyPressed(Key.S))
        {
            panDirection.Y -= 1.0f;
        }

        if (panDirection == Vector2.Zero)
        {
            return false;
        }

        _viewCenterDocument += panDirection.Normalized() * PanPixelsPerSecond * delta / Mathf.Max(_scale, 0.001f);
        return true;
    }

    private bool ApplyKeyboardZoom(float delta)
    {
        float zoomDirection = 0.0f;

        if (Input.IsKeyPressed(Key.E))
        {
            zoomDirection += 1.0f;
        }

        if (Input.IsKeyPressed(Key.Q))
        {
            zoomDirection -= 1.0f;
        }

        if (Mathf.IsZeroApprox(zoomDirection))
        {
            return false;
        }

        Vector2 beforeZoom = ScreenToDocument(_lastMouseScreenPosition);
        float zoomFactor = Mathf.Pow(1.2f, zoomDirection * ZoomStepsPerSecond * delta);
        _zoom = Mathf.Clamp(_zoom * zoomFactor, MinZoom, MaxZoom);
        UpdateCanvasRect();
        Vector2 afterZoom = ScreenToDocument(_lastMouseScreenPosition);
        _viewCenterDocument += beforeZoom - afterZoom;
        return true;
    }

    private bool ApplyKeyboardFitView()
    {
        if (!Input.IsKeyPressed(Key.F))
        {
            return false;
        }

        FitView();
        return true;
    }

    private static bool IsCommandModifierPressed()
    {
        return Input.IsKeyPressed(Key.Meta);
    }

    private bool IsTextInputFocused()
    {
        Control focusedControl = GetViewport()?.GuiGetFocusOwner();
        return focusedControl is LineEdit or SpinBox;
    }

    private static Vector2[] ClosePolyline(Vector2[] points)
    {
        if (points.Length == 0)
        {
            return points;
        }

        Vector2[] closed = new Vector2[points.Length + 1];
        for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
        {
            closed[pointIndex] = points[pointIndex];
        }

        closed[^1] = points[0];
        return closed;
    }
}
