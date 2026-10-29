namespace TaskbarGroups.Layout;

/// <summary>
/// Works out where on a horizontal taskbar we may draw without covering any of Windows' own controls.
/// </summary>
public static class TaskbarGapFinder
{
    /// <summary>
    /// Returns the horizontal spans of <paramref name="taskbar"/> not covered by any <paramref name="occupied"/> rect,
    /// keeping <paramref name="margin"/> pixels clear of every control and of the taskbar's ends.
    /// </summary>
    public static IReadOnlyList<IntRect> FindGaps(IntRect taskbar, IEnumerable<IntRect> occupied, int margin)
    {
        var spans = occupied
            .Where(r => !r.IsEmpty && r.Right > taskbar.Left && r.Left < taskbar.Right)
            .Select(r => (Left: r.Left - margin, Right: r.Right + margin))
            .OrderBy(s => s.Left);

        var gaps = new List<IntRect>();
        var x = taskbar.Left + margin;
        foreach (var (left, right) in spans)
        {
            if (left > x)
                gaps.Add(new IntRect(x, taskbar.Top, left, taskbar.Bottom));
            x = Math.Max(x, right);
        }

        var end = taskbar.Right - margin;
        if (end > x)
            gaps.Add(new IntRect(x, taskbar.Top, end, taskbar.Bottom));
        return gaps;
    }

    /// <summary>
    /// Picks where the groups go: the first gap after the app buttons (matching the mockup, where groups follow
    /// Windows' icons), otherwise the widest gap. Returns null if no gap is at least <paramref name="minWidth"/>.
    /// </summary>
    public static IntRect? PickGap(IReadOnlyList<IntRect> gaps, int appButtonsRight, int minWidth)
    {
        var usable = gaps.Where(g => g.Width >= minWidth).ToList();
        if (usable.Count == 0)
            return null;

        return usable.Where(g => g.Left >= appButtonsRight).OrderBy(g => g.Left).Cast<IntRect?>().FirstOrDefault()
            ?? usable.MaxBy(g => g.Width);
    }
}
