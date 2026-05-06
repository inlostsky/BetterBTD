using System.Windows;

namespace BetterBTD.Models;

public readonly record struct TemplateMatchInfo(
    int X,
    int Y,
    int Width,
    int Height,
    double Score,
    double Threshold)
{
    public bool IsMatch => Score >= Threshold;

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public Point TopLeft => new(X, Y);

    public Point Center => new(X + Width / 2d, Y + Height / 2d);

    public Rect Bounds => new(X, Y, Width, Height);
}
