namespace DataTide.Services;

/// <summary>
/// Loads and saves the usage ledger (%APPDATA%\DataTide\usage.csv) and the per-program log (programs.csv),
/// each written to a temp file and then swapped in.
/// </summary>
static class UsageStore
{
    public static UsageLedger Load()
    {
        var l = new UsageLedger();
        ReadFile(AppPaths.Usage, r => l.Read(r));
        return l;
    }

    public static ProgramLedger LoadPrograms()
    {
        var l = new ProgramLedger();
        ReadFile(AppPaths.Programs, r => l.Read(r));
        return l;
    }

    public static void Save(UsageLedger l) => WriteFile(AppPaths.Usage, l.Write);
    public static void Save(ProgramLedger l) => WriteFile(AppPaths.Programs, l.Write);

    static void ReadFile(string path, Action<TextReader> read)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var r = new StreamReader(path);
            read(r);
        }
        catch (Exception ex) { Log.Error(ex); }
    }

    static void WriteFile(string path, Action<TextWriter> write)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Data);
            var tmp = path + ".tmp";
            using (var w = new StreamWriter(tmp)) write(w);
            File.Move(tmp, path, true);
        }
        catch (Exception ex) { Log.Error(ex); }
    }
}
