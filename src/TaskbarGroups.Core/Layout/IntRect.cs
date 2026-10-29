namespace TaskbarGroups.Layout;

/// <summary>Screen rectangle in physical pixels. Right/Bottom are exclusive.</summary>
public readonly record struct IntRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(IntRect other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    public bool Intersects(IntRect other) =>
        other.Left < Right && other.Right > Left && other.Top < Bottom && other.Bottom > Top;

    public static IntRect FromSize(int left, int top, int width, int height) => new(left, top, left + width, top + height);
}
