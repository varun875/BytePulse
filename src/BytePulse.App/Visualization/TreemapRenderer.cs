using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace BytePulse.Visualization;

/// <summary>One box of a treemap.</summary>
/// <param name="Label">What the box represents.</param>
/// <param name="Value">How much of the whole the box accounts for.</param>
public readonly record struct TreemapItem(string Label, long Value);

/// <summary>
/// Draws a folder's children as boxes whose area is proportional to their size.
/// </summary>
/// <remarks>
/// <para>
/// Each level is split into two groups whose totals are as close to equal as the order
/// allows, and the group with the larger total is given the larger share of the longer
/// side of the rectangle. Splitting that way is much simpler than the usual squarified
/// layout and still gives boxes that are readable rather than long ribbons.
/// </para>
/// <para>
/// The recursion is depth first and bounded by the number of items, so a folder with
/// thousands of children cannot overflow the stack; items too thin to see are dropped.
/// </para>
/// </remarks>
internal static class TreemapRenderer
{
    /// <summary>Boxes thinner than this are not worth drawing.</summary>
    private const double MinimumSide = 3;

    /// <summary>Draws the map into a canvas.</summary>
    /// <param name="canvas">The canvas to draw into.</param>
    /// <param name="items">The values to draw.</param>
    /// <param name="width">The width of the map.</param>
    /// <param name="height">The height of the map.</param>
    /// <param name="foreground">The brush used for the outlines and labels.</param>
    /// <param name="onSelected">Called with the box that was tapped, if any.</param>
    public static void Draw(
        Canvas canvas,
        IReadOnlyList<TreemapItem> items,
        double width,
        double height,
        Brush foreground,
        Action<TreemapItem>? onSelected = null)
    {
        canvas.Children.Clear();
        canvas.Width = width;
        canvas.Height = height;

        var sorted = items
            .Where(i => i.Value > 0)
            .OrderByDescending(i => i.Value)
            .ToList();

        if (sorted.Count == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var boxes = new List<(TreemapItem Item, Rect Rect)>();
        Subdivide(sorted, new Rect(0, 0, width, height), boxes);

        for (int i = 0; i < boxes.Count; i++)
        {
            (TreemapItem item, Rect rect) = boxes[i];

            if (rect.Width < MinimumSide || rect.Height < MinimumSide)
            {
                continue;
            }

            var box = new Rectangle
            {
                Width = rect.Width,
                Height = rect.Height,
                Fill = new SolidColorBrush(ChartPalette.At(i)),
                Stroke = foreground,
                StrokeThickness = 0.5,
                RadiusX = 2,
                RadiusY = 2,
            };

            if (onSelected is not null)
            {
                TreemapItem captured = item;
                box.Tapped += (_, _) => onSelected(captured);
            }

            Canvas.SetLeft(box, rect.X);
            Canvas.SetTop(box, rect.Y);
            canvas.Children.Add(box);

            if (rect.Width > 54 && rect.Height > 28)
            {
                var label = new TextBlock
                {
                    Text = item.Label,
                    Foreground = foreground,
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = rect.Width - 6,
                    TextWrapping = TextWrapping.NoWrap,
                };

                Canvas.SetLeft(label, rect.X + 3);
                Canvas.SetTop(label, rect.Y + 2);
                canvas.Children.Add(label);
            }
        }
    }

    /// <summary>
    /// Splits a rectangle between the items, depth first.
    /// </summary>
    /// <remarks>
    /// The items are always split at a point where the two groups come closest to holding
    /// the same total, which keeps the boxes from degenerating into slivers.
    /// </remarks>
    private static void Subdivide(IReadOnlyList<TreemapItem> items, Rect rect, List<(TreemapItem, Rect)> output)
    {
        if (items.Count == 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (items.Count == 1)
        {
            output.Add((items[0], rect));
            return;
        }

        double total = items.Sum(i => (double)i.Value);
        if (total <= 0)
        {
            return;
        }

        int split = ChooseSplit(items, total / 2);
        double firstTotal = items.Take(split).Sum(i => (double)i.Value);
        double ratio = firstTotal / total;

        Rect first;
        Rect second;

        if (rect.Width >= rect.Height)
        {
            double cut = rect.Width * ratio;
            first = new Rect(rect.X, rect.Y, cut, rect.Height);
            second = new Rect(rect.X + cut, rect.Y, rect.Width - cut, rect.Height);
        }
        else
        {
            double cut = rect.Height * ratio;
            first = new Rect(rect.X, rect.Y, rect.Width, cut);
            second = new Rect(rect.X, rect.Y + cut, rect.Width, rect.Height - cut);
        }

        var firstItems = new List<TreemapItem>(split);
        var secondItems = new List<TreemapItem>(items.Count - split);

        for (int i = 0; i < split; i++)
        {
            firstItems.Add(items[i]);
        }

        for (int i = split; i < items.Count; i++)
        {
            secondItems.Add(items[i]);
        }

        Subdivide(firstItems, first, output);
        Subdivide(secondItems, second, output);
    }

    /// <summary>The split point whose running total is closest to the target.</summary>
    private static int ChooseSplit(IReadOnlyList<TreemapItem> items, double target)
    {
        double running = 0;
        int best = 1;
        double bestDistance = double.MaxValue;

        for (int i = 0; i < items.Count - 1; i++)
        {
            running += items[i].Value;
            double distance = Math.Abs(running - target);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i + 1;
            }
        }

        return Math.Clamp(best, 1, items.Count - 1);
    }
}
