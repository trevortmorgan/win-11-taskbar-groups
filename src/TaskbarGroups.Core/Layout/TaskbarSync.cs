namespace TaskbarGroups.Layout;

/// <summary>Works out which strips to add and remove as taskbars (one per monitor) come and go.</summary>
public static class TaskbarSync
{
    /// <summary>
    /// Compares the secondary taskbars that exist now with the ones we already have a strip for. Both are window
    /// handles. A taskbar that is recreated (monitor re-plugged, Explorer restarted) gets a new handle, so it shows
    /// up as one removed and one added.
    /// </summary>
    public static (IReadOnlyList<nint> Added, IReadOnlyList<nint> Removed) Diff(IEnumerable<nint> current, IEnumerable<nint> existing)
    {
        var now = current.Distinct().ToHashSet();
        var have = existing.ToHashSet();
        return (now.Where(h => !have.Contains(h)).ToList(), have.Where(h => !now.Contains(h)).ToList());
    }
}
