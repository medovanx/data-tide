namespace DataTide.Services;

/// <summary>Icons of programs by exe path, extracted once and cached (the generic app icon when a file has none).</summary>
static class ProgramIcons
{
    static readonly Dictionary<string, Image?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Image? For(string exePath)
    {
        if (Cache.TryGetValue(exePath, out var img)) return img;
        try
        {
            using var icon = File.Exists(exePath) ? Icon.ExtractAssociatedIcon(exePath) : null;
            img = icon?.ToBitmap();
        }
        catch { img = null; }
        img ??= SystemIcons.Application.ToBitmap();
        Cache[exePath] = img;
        return img;
    }
}
