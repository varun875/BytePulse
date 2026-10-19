using System.Globalization;

namespace BytePulse.Core.Formatting;

/// <summary>Turns byte counts into text a person can read.</summary>
public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Formats a byte count using the largest unit that keeps the number readable.
    /// </summary>
    /// <param name="bytes">The number of bytes.</param>
    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            return "-" + Format(-bytes);
        }

        if (bytes < 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes} B");
        }

        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Keep the number to three significant figures so the column stays tidy.
        string text = value switch
        {
            >= 100 => value.ToString("F0", CultureInfo.InvariantCulture),
            >= 10 => value.ToString("F1", CultureInfo.InvariantCulture),
            _ => value.ToString("F2", CultureInfo.InvariantCulture),
        };

        return string.Create(CultureInfo.InvariantCulture, $"{text} {Units[unit]}");
    }

    /// <summary>
    /// Formats a byte count together with a count of items, such as "1.2 GB in 340 files".
    /// </summary>
    public static string FormatWithCount(long bytes, long count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{Format(bytes)} in {count:N0} {noun}");

    /// <summary>
    /// Formats a number of items with the right plural.
    /// </summary>
    public static string FormatCount(long count, string singular, string plural) =>
        string.Create(CultureInfo.InvariantCulture, $"{count:N0} {(count == 1 ? singular : plural)}");
}
