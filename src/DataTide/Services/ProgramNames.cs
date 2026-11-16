using System.Diagnostics;

namespace DataTide.Services;

/// <summary>A program's own description ("Google Chrome") when its exe has one, else its file name. Cached per path.</summary>
static class ProgramNames
{
    static readonly Dictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string For(string path)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var n)) return n;
            n = Path.GetFileNameWithoutExtension(path);
            try
            {
                var d = File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim() : null;
                if (!string.IsNullOrEmpty(d) && d.Length <= 60) n = d;
            }
            catch { }
            if (path == "System") n = "Windows (System)";
            return Cache[path] = n;
        }
    }
}
