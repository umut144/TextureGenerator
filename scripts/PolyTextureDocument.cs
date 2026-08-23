using System;
using System.Collections.Generic;

public sealed class PolyTextureDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string DocumentType { get; set; } = "polytexture";
    public string Name { get; set; } = "Untitled";
    public int DefaultPreviewWidthPx { get; set; } = 512;
    public int DefaultPreviewHeightPx { get; set; } = 512;
    public bool SnapEnabled { get; set; }
    public float SnapStepCm { get; set; } = PolyTextureUnits.DefaultSnapStepCm;
    public string ActiveTextureId { get; set; } = string.Empty;
    public string ActiveElementId { get; set; } = string.Empty;
    public string ActiveGuideId { get; set; } = string.Empty;
    public int SelectedPointIndex { get; set; } = -1;
    public PolyTextureSelectionKind SelectionKind { get; set; } = PolyTextureSelectionKind.Texture;
    public List<PolyTextureItem> Textures { get; } = new();

    public PolyTextureItem ActiveTexture
    {
        get
        {
            foreach (PolyTextureItem texture in Textures)
            {
                if (texture.Id.Equals(ActiveTextureId, StringComparison.Ordinal))
                {
                    return texture;
                }
            }

            return Textures.Count > 0 ? Textures[0] : null;
        }
    }

    public PolyTextureElement ActiveElement => ActiveTexture?.GetElement(ActiveElementId);
    public PolyTextureGuide ActiveGuide => ActiveTexture?.GetGuide(ActiveGuideId);

    public CenterStrokeElement ActiveCenterStroke => ActiveElement as CenterStrokeElement;

    public PolyTexturePoint ActivePoint
    {
        get
        {
            CenterStrokeElement centerStroke = ActiveCenterStroke;
            return centerStroke == null || SelectedPointIndex < 0 || SelectedPointIndex >= centerStroke.Points.Count
                ? null
                : centerStroke.Points[SelectedPointIndex];
        }
    }

    public void EnsureSelection()
    {
        if (Textures.Count == 0)
        {
            ActiveTextureId = string.Empty;
            ActiveElementId = string.Empty;
            ActiveGuideId = string.Empty;
            SelectedPointIndex = -1;
            SelectionKind = PolyTextureSelectionKind.Texture;
            return;
        }

        ActiveTextureId = ActiveTexture?.Id ?? Textures[0].Id;
        PolyTextureItem activeTexture = ActiveTexture;
        if (activeTexture.GetElement(ActiveElementId) == null)
        {
            ActiveElementId = activeTexture.Elements.Count > 0 ? activeTexture.Elements[0].Id : string.Empty;
        }

        if (activeTexture.GetGuide(ActiveGuideId) == null)
        {
            ActiveGuideId = string.Empty;
        }

        CenterStrokeElement centerStroke = ActiveCenterStroke;
        if (centerStroke == null || SelectedPointIndex < 0 || SelectedPointIndex >= centerStroke.Points.Count)
        {
            SelectedPointIndex = -1;
            if (SelectionKind == PolyTextureSelectionKind.Point)
            {
                SelectionKind = string.IsNullOrEmpty(ActiveElementId)
                    ? PolyTextureSelectionKind.Texture
                    : PolyTextureSelectionKind.Element;
            }
        }

        if (string.IsNullOrEmpty(ActiveElementId) && SelectionKind != PolyTextureSelectionKind.Texture)
        {
            SelectionKind = SelectionKind == PolyTextureSelectionKind.Guide && ActiveGuide != null
                ? PolyTextureSelectionKind.Guide
                : PolyTextureSelectionKind.Texture;
        }
    }

    public PolyTextureDocument Clone()
    {
        PolyTextureDocument clone = new()
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
            SelectedPointIndex = SelectedPointIndex,
            SelectionKind = SelectionKind
        };

        clone.Textures.Clear();
        foreach (PolyTextureItem texture in Textures)
        {
            clone.Textures.Add(texture.Clone());
        }

        clone.EnsureSelection();
        return clone;
    }
}
