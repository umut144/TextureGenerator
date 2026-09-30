using System;
using System.Collections.Generic;

public sealed class TextureGeneratorDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string DocumentType { get; set; } = "texturegenerator";
    public string Name { get; set; } = "Untitled";
    public int DefaultPreviewWidthPx { get; set; } = 512;
    public int DefaultPreviewHeightPx { get; set; } = 512;
    public bool SnapEnabled { get; set; }
    public float SnapStepCm { get; set; } = TextureGeneratorUnits.DefaultSnapStepCm;
    public string ActiveTextureId { get; set; } = string.Empty;
    public string ActiveElementId { get; set; } = string.Empty;
    public string ActiveGuideId { get; set; } = string.Empty;
    public string ActiveOutputId { get; set; } = string.Empty;
    private int _selectedPointIndex = -1;
    public List<int> SelectedPointIndices { get; } = new();
    public int SelectedPointIndex
    {
        get => _selectedPointIndex;
        set
        {
            _selectedPointIndex = value;
            SelectedPointIndices.Clear();
            if (value >= 0)
            {
                SelectedPointIndices.Add(value);
            }
        }
    }
    public TextureGeneratorSelectionKind SelectionKind { get; set; } = TextureGeneratorSelectionKind.Texture;
    public List<TextureGeneratorItem> Textures { get; } = new();

    public TextureGeneratorItem ActiveTexture
    {
        get
        {
            foreach (TextureGeneratorItem texture in Textures)
            {
                if (texture.Id.Equals(ActiveTextureId, StringComparison.Ordinal))
                {
                    return texture;
                }
            }

            return Textures.Count > 0 ? Textures[0] : null;
        }
    }

    public TextureGeneratorElement ActiveElement => ActiveTexture?.GetElement(ActiveElementId);
    public TextureGeneratorGuide ActiveGuide => ActiveTexture?.GetGuide(ActiveGuideId);
    public TextureGeneratorOutputBinding ActiveOutput => ActiveTexture?.GetOutput(ActiveOutputId);

    public CenterStrokeElement ActiveCenterStroke => ActiveElement as CenterStrokeElement;

    public TextureGeneratorPoint ActivePoint
    {
        get
        {
            CenterStrokeElement centerStroke = ActiveCenterStroke;
            return centerStroke == null || SelectedPointIndex < 0 || SelectedPointIndex >= centerStroke.Points.Count
                ? null
                : centerStroke.Points[SelectedPointIndex];
        }
    }

    public bool IsPointSelected(int pointIndex)
    {
        return SelectedPointIndices.Contains(pointIndex);
    }

    public void SetPointSelection(IEnumerable<int> pointIndices, int primaryPointIndex)
    {
        SelectedPointIndices.Clear();
        foreach (int pointIndex in pointIndices)
        {
            if (pointIndex >= 0 && !SelectedPointIndices.Contains(pointIndex))
            {
                SelectedPointIndices.Add(pointIndex);
            }
        }
        _selectedPointIndex = SelectedPointIndices.Contains(primaryPointIndex)
            ? primaryPointIndex
            : SelectedPointIndices.Count > 0 ? SelectedPointIndices[^1] : -1;
        SelectionKind = _selectedPointIndex >= 0 ? TextureGeneratorSelectionKind.Point : TextureGeneratorSelectionKind.Element;
    }

    public void TogglePointSelection(int pointIndex)
    {
        if (pointIndex < 0)
        {
            return;
        }
        if (SelectedPointIndices.Contains(pointIndex))
        {
            SelectedPointIndices.Remove(pointIndex);
            _selectedPointIndex = SelectedPointIndices.Count > 0 ? SelectedPointIndices[^1] : -1;
        }
        else
        {
            SelectedPointIndices.Add(pointIndex);
            _selectedPointIndex = pointIndex;
        }
        SelectionKind = _selectedPointIndex >= 0 ? TextureGeneratorSelectionKind.Point : TextureGeneratorSelectionKind.Element;
    }

    public void EnsureSelection()
    {
        if (Textures.Count == 0)
        {
            ActiveTextureId = string.Empty;
            ActiveElementId = string.Empty;
            ActiveGuideId = string.Empty;
            ActiveOutputId = string.Empty;
            SelectedPointIndex = -1;
            SelectionKind = TextureGeneratorSelectionKind.Texture;
            return;
        }

        ActiveTextureId = ActiveTexture?.Id ?? Textures[0].Id;
        TextureGeneratorItem activeTexture = ActiveTexture;
        if (activeTexture.GetElement(ActiveElementId) == null)
        {
            ActiveElementId = activeTexture.Elements.Count > 0 ? activeTexture.Elements[0].Id : string.Empty;
        }

        if (activeTexture.GetGuide(ActiveGuideId) == null)
        {
            ActiveGuideId = string.Empty;
        }

        if (activeTexture.GetOutput(ActiveOutputId) == null)
        {
            ActiveOutputId = string.Empty;
        }

        CenterStrokeElement centerStroke = ActiveCenterStroke;
        if (centerStroke == null || SelectedPointIndex < 0 || SelectedPointIndex >= centerStroke.Points.Count)
        {
            SelectedPointIndex = -1;
            if (SelectionKind == TextureGeneratorSelectionKind.Point)
            {
                SelectionKind = string.IsNullOrEmpty(ActiveElementId)
                    ? TextureGeneratorSelectionKind.Texture
                    : TextureGeneratorSelectionKind.Element;
            }
        }
        else
        {
            SelectedPointIndices.RemoveAll(index => index < 0 || index >= centerStroke.Points.Count);
            if (!SelectedPointIndices.Contains(SelectedPointIndex))
            {
                SelectedPointIndices.Add(SelectedPointIndex);
            }
        }

        if (string.IsNullOrEmpty(ActiveElementId) && SelectionKind != TextureGeneratorSelectionKind.Texture)
        {
            SelectionKind = SelectionKind switch
            {
                TextureGeneratorSelectionKind.Guide when ActiveGuide != null => TextureGeneratorSelectionKind.Guide,
                TextureGeneratorSelectionKind.Output when ActiveOutput != null => TextureGeneratorSelectionKind.Output,
                _ => TextureGeneratorSelectionKind.Texture
            };
        }
    }

    public TextureGeneratorDocument Clone()
    {
        TextureGeneratorDocument clone = new()
        {
            SchemaVersion = SchemaVersion,
            DocumentType = DocumentType,
            Name = Name,
            DefaultPreviewWidthPx = DefaultPreviewWidthPx,
            DefaultPreviewHeightPx = DefaultPreviewHeightPx,
            SnapEnabled = SnapEnabled,
            SnapStepCm = SnapStepCm,
            ActiveTextureId = ActiveTextureId,
            ActiveElementId = ActiveElementId,
            ActiveGuideId = ActiveGuideId,
            ActiveOutputId = ActiveOutputId,
            SelectedPointIndex = SelectedPointIndex,
            SelectionKind = SelectionKind
        };

        clone.Textures.Clear();
        foreach (TextureGeneratorItem texture in Textures)
        {
            clone.Textures.Add(texture.Clone());
        }

        clone.SetPointSelection(SelectedPointIndices, SelectedPointIndex);

        clone.EnsureSelection();
        return clone;
    }
}
