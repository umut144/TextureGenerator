public abstract class TextureGeneratorElement
{
    public string Id { get; set; } = "element";
    public string Name { get; set; } = "Element";
    public bool Enabled { get; set; } = true;
    public float Opacity { get; set; } = 0.86f;

    public abstract string Type { get; }
    public abstract TextureGeneratorElement Clone();
}
