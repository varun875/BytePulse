using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace BytePulse.Visualization;

/// <summary>
/// Draws a folder's largest children as a ring, with the rest gathered into one
/// slice.
/// </summary>
/// <remarks>
/// WinUI 3 has no chart control in a stable release, and the experimental one only
/// draws cartesian series, so the ring is drawn directly. Each slice is a filled
/// wedge and the hole is a circle in the background colour, which is simpler and
/// renders more crisply than an annular sector built from two arcs.
/// </remarks>
internal static class DonutChartRenderer
{
    /// <summary>How many children get their own slice before the rest are grouped.</summary>
    private const int MaxSlices = 8;

    /// <summary>Draws the chart into a canvas.</summary>
    /// <param name="canvas">The canvas to draw into.</param>
    /// <param name="segments">The values to draw, largest first.</param>
    /// <param name="diameter">The size of the ring.</param>
    /// <param name="holeBrush">The brush for the centre of the ring.</param>
    public static void Draw(Canvas canvas, IReadOnlyList<ChartSegment> segments, double diameter, Brush holeBrush)
    {
        canvas.Children.Clear();
        canvas.Width = diameter;
        canvas.Height = diameter;

        if (diameter <= 0 || segments.Count == 0)
        {
            return;
        }

        var slices = BuildSlices(segments);
        double total = slices.Sum(s => (double)s.Value);

        if (total <= 0)
        {
            return;
        }

        double radius = diameter / 2;
        var center = new Point(radius, radius);

        for (int i = 0; i < slices.Count; i++)
        {
            double start = slices.Take(i).Sum(s => (double)s.Value) / total * Math.Tau;
            double sweep = (double)slices[i].Value / total * Math.Tau;

            canvas.Children.Add(CreateWedge(center, radius, start, sweep, ChartPalette.At(i)));
        }

        double hole = diameter * 0.58;
        var holeShape = new Ellipse
        {
            Width = hole,
            Height = hole,
            Fill = holeBrush,
        };

        Canvas.SetLeft(holeShape, radius - (hole / 2));
        Canvas.SetTop(holeShape, radius - (hole / 2));
        canvas.Children.Add(holeShape);
    }

    /// <summary>
    /// Takes the largest few children and gathers the rest into a single slice, so the
    /// ring stays readable when a folder has hundreds of children.
    /// </summary>
    public static IReadOnlyList<ChartSegment> BuildSlices(IReadOnlyList<ChartSegment> segments)
    {
        var ordered = segments
            .Where(s => s.Value > 0)
            .OrderByDescending(s => s.Value)
            .ToList();

        if (ordered.Count <= MaxSlices)
        {
            return ordered;
        }

        var kept = ordered.Take(MaxSlices - 1).ToList();
        long rest = ordered.Skip(MaxSlices - 1).Sum(s => s.Value);

        if (rest > 0)
        {
            kept.Add(new ChartSegment("Everything else", rest));
        }

        return kept;
    }

    private static Microsoft.UI.Xaml.Shapes.Path CreateWedge(Point center, double radius, double start, double sweep, Windows.UI.Color color)
    {
        var figure = new PathFigure
        {
            StartPoint = center,
            IsClosed = true,
            IsFilled = true,
        };

        figure.Segments.Add(new LineSegment { Point = PointOnCircle(center, radius, start) });

        figure.Segments.Add(new ArcSegment
        {
            Point = PointOnCircle(center, radius, start + sweep),
            Size = new Size(radius, radius),
            IsLargeArc = sweep > Math.PI,
            SweepDirection = SweepDirection.Clockwise,
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(color),
        };
    }

    private static Point PointOnCircle(Point center, double radius, double angle) =>
        new(center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle)));
}
