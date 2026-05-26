using System.Text.Json.Serialization;

namespace D4LootBench.Core.Models;

public sealed class FilterRule
{
    public FilterRule() { }

    public FilterRule(string name, Visibility visibility, uint color, IEnumerable<Condition> conditions, bool isEnabled = true)
    {
        Name       = name;
        Visibility = visibility;
        Color      = color;
        Conditions = conditions.ToList();
        IsEnabled  = isEnabled;
    }

    public string          Name       { get; set; } = "";
    public Visibility      Visibility { get; set; } = Visibility.Show;
    public uint            Color      { get; set; }
    public List<Condition> Conditions { get; set; } = [];
    public bool            IsEnabled  { get; set; } = true;

    /// <summary>Deconstructs the packed ARGB uint into individual channels.</summary>
    [JsonIgnore]
    public (byte R, byte G, byte B, byte A) ColorChannels => (
        R: (byte)((Color >> 16) & 0xFF),
        G: (byte)((Color >> 8) & 0xFF),
        B: (byte)(Color & 0xFF),
        A: (byte)((Color >> 24) & 0xFF)
    );

    public static uint PackColor(byte r, byte g, byte b, byte a = 255)
        => ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
}
