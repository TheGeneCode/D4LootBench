namespace D4LootBench.Core.Data;

/// <summary>Standard D4 filter highlight colors in ABGR packed uint32 format.</summary>
public static class FilterColors
{
    // Packed ARGB: A=bits 31-24, R=bits 23-16, G=bits 15-8, B=bits 7-0
    // In-game color picker displays the lower 24 bits as 6-char RGB hex (e.g. "E82222" = red).
    public const uint GameDefault = 0x00000000; // 0 = no override; game renders native item color
    public const uint Blue        = 0xFF0000FF; // R=0,   G=0,   B=255
    public const uint Cyan        = 0xFF00FFFF; // R=0,   G=255, B=255
    public const uint Green       = 0xFF00C800; // R=0,   G=200, B=0
    public const uint Orange      = 0xFFFF8C00; // R=255, G=140, B=0
    public const uint Gold        = 0xFFFFD700; // R=255, G=215, B=0
}
