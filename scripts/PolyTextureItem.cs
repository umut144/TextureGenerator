using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PolyTextureItem
{
    public string Id { get; set; } = "texture";
    public string Name { get; set; } = "Texture";
    public PolyTextureOriginMode OriginMode { get; set; } = PolyTextureOriginMode.BottomLeft;
    public float DomainWidthCm { get; set; } = PolyTextureUnits.DefaultDomainSizeCm;
    public float DomainHeightCm { get; set; } = PolyTextureUnits.DefaultDomainSizeCm;
    public int PreviewWidthPx { get; set; } = 512;
    public int PreviewHeightPx { get; set; } = 512;
    public bool Visible { get; set; } = true;
    public List<PolyTextureElement> Elements { get; } = new();
    public List<PolyTextureGuide> Guides { get; } = new();

    public PolyTextureElement GetElement(string id)
    {
        return Elements.FirstOrDefault(element => element.Id.Equals(id, StringComparison.Ordinal));
    }

    public CenterStrokeElement GetCenterStroke(string id)
    {
        return GetElement(id) as CenterStrokeElement;
    }

    public PolyTextureGuide GetGuide(string id)
    {
        return Guides.FirstOrDefault(guide => guide.Id.Equals(id, StringComparison.Ordinal));
    }

    public PolyTextureItem Clone()
    {
        PolyTextureItem clone = new()
        {
            Id = Id,
            Name = Name,
            OriginMode = OriginMode,
            DomainWidthCm = DomainWidthCm,
            DomainHeightCm = DomainHeightCm,
            PreviewWidthPx = PreviewWidthPx,
            PreviewHeightPx = PreviewHeightPx,
            Visible = Visible
        };

        clone.Elements.Clear();
        foreach (PolyTextureElement element in Elements)
        {
            clone.Elements.Add(element.Clone());
        }
        foreach (PolyTextureGuide guide in Guides)
        {
            clone.Guides.Add(guide.Clone());
        }

        return clone;
    }
}
