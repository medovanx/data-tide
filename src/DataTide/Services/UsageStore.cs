namespace DataTide.Services;

/// <summary>Loads and saves the usage ledger in %APPDATA%\DataTide\usage.csv (written to a temp file, then swapped in).</summary>
static class UsageStore
{
    public static UsageLedger Load()
    {
        var l = new UsageLedger();
        try
        {
            if (File.Exists(AppPaths.Usage))
            {
                using var r = new StreamReader(AppPaths.Usage);
                l.Read(r);
            }
        }
        catch (Exception ex) { Log.Error(ex); }
        return l;
    }

    public static void Save(UsageLedger l)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Data);
            var tmp = AppPaths.Usage + ".tmp";
            using (var w = new StreamWriter(tmp)) l.Write(w);
            File.Move(tmp, AppPaths.Usage, true);
        }
        catch (Exception ex) { Log.Error(ex); }
    }
}
