using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TextureGeneratorItem
{
    public string Id { get; set; } = "texture";
    public string Name { get; set; } = "Texture";
    public TextureGeneratorOriginMode OriginMode { get; set; } = TextureGeneratorOriginMode.BottomLeft;
    public float DomainWidthCm { get; set; } = TextureGeneratorUnits.DefaultDomainSizeCm;
    public float DomainHeightCm { get; set; } = TextureGeneratorUnits.DefaultDomainSizeCm;
    public int PreviewWidthPx { get; set; } = 512;
    public int PreviewHeightPx { get; set; } = 512;
    public bool Visible { get; set; } = true;
    public List<TextureGeneratorElement> Elements { get; } = new();
    public List<TextureGeneratorGuide> Guides { get; } = new();
    public List<TextureGeneratorOutputBinding> Outputs { get; } = new();

    public TextureGeneratorElement GetElement(string id)
    {
        return Elements.FirstOrDefault(element => element.Id.Equals(id, StringComparison.Ordinal));
    }

    public CenterStrokeElement GetCenterStroke(string id)
    {
        return GetElement(id) as CenterStrokeElement;
    }

    public TextureGeneratorGuide GetGuide(string id)
    {
        return Guides.FirstOrDefault(guide => guide.Id.Equals(id, StringComparison.Ordinal));
    }

    public TextureGeneratorOutputBinding GetOutput(string id)
    {
        return Outputs.FirstOrDefault(output => output.Id.Equals(id, StringComparison.Ordinal));
    }

    public TextureGeneratorItem Clone()
    {
        TextureGeneratorItem clone = new()
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
        foreach (TextureGeneratorElement element in Elements)
        {
            clone.Elements.Add(element.Clone());
        }
        foreach (TextureGeneratorGuide guide in Guides)
        {
            clone.Guides.Add(guide.Clone());
        }
        foreach (TextureGeneratorOutputBinding output in Outputs)
        {
            clone.Outputs.Add(output.Clone());
        }

        return clone;
    }
}
