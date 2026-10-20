using Windows.UI;

namespace BytePulse.Visualization;

/// <summary>One slice of a chart.</summary>
/// <param name="Label">What the slice represents.</param>
/// <param name="Value">How much of the whole the slice accounts for.</param>
public readonly record struct ChartSegment(string Label, long Value);

/// <summary>
/// The palette the charts draw with.
/// </summary>
/// <remarks>
/// The colours are picked to stay distinguishable next to each other and to read
/// acceptably on both the light and the dark theme.
/// </remarks>
internal static class ChartPalette
{
    private static readonly Color[] Colors =
    [
        Color.FromArgb(255, 0x4C, 0x8B, 0xF5),
        Color.FromArgb(255, 0xE8, 0x6A, 0x5C),
        Color.FromArgb(255, 0x4F, 0xC1, 0x8C),
        Color.FromArgb(255, 0xE9, 0xB4, 0x4C),
        Color.FromArgb(255, 0x9B, 0x6B, 0xE8),
        Color.FromArgb(255, 0x3F, 0xB8, 0xC9),
        Color.FromArgb(255, 0xD9, 0x6B, 0xA8),
        Color.FromArgb(255, 0x8D, 0xA5, 0x5C),
        Color.FromArgb(255, 0xC9, 0x7F, 0x3F),
        Color.FromArgb(255, 0x6C, 0x7F, 0xD9),
    ];

    /// <summary>The colour for a slice at the given index.</summary>
    public static Color At(int index) => Colors[index % Colors.Length];

    /// <summary>A muted colour, for the "everything else" slice.</summary>
    public static Color Remainder => Color.FromArgb(255, 0x6E, 0x6E, 0x76);
}
