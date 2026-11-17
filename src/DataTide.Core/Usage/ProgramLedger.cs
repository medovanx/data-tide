using System.Globalization;

namespace DataTide.Core.Usage;

/// <summary>
/// Daily byte counts per program, stored as a small CSV (date, name, path, received, sent). Programs are keyed by
/// name so two copies of one app count as one. Only filled while the Programs list is on. Thread-safe.
/// </summary>
public sealed class ProgramLedger
{
    public const string Header = "date,program,path,received,sent";
    const string DateFormat = "yyyy-MM-dd";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    readonly object gate = new();
    readonly Dictionary<(DateOnly day, string name), (string path, double rx, double tx)> days = [];
    /// <summary>Display name per key (keys are upper-cased names).</summary>
    readonly Dictionary<string, string> names = [];

    public bool Dirty { get; private set; }

    static (DateOnly, string) Key(DateOnly day, string name) => (day, name.ToUpperInvariant());

    /// <summary>Adds bytes (fractions are kept, so many small samples still add up).</summary>
    public void Add(DateOnly day, string name, string path, double received, double sent)
    {
        if (string.IsNullOrWhiteSpace(name) || (received <= 0 && sent <= 0)) return;
        var key = Key(day, name);
        lock (gate)
        {
            days.TryGetValue(key, out var v);
            days[key] = (path.Length > 0 ? path : v.path ?? "", v.rx + Math.Max(0, received), v.tx + Math.Max(0, sent));
            names[key.Item2] = name;
            Dirty = true;
        }
    }

    /// <summary>Totals per program from <paramref name="from"/> to <paramref name="to"/>, both included, busiest first.</summary>
    public IReadOnlyList<ProgramTotal> Range(DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        lock (gate)
            return days.Where(kv => kv.Key.day >= from && kv.Key.day <= to)
                .GroupBy(kv => kv.Key.name)
                .Select(g =>
                {
                    var last = g.OrderBy(kv => kv.Key.day).Last().Value;
                    return new ProgramTotal(names.GetValueOrDefault(g.Key, g.Key), last.path,
                        (long)g.Sum(kv => kv.Value.rx), (long)g.Sum(kv => kv.Value.tx));
                })
                .Where(p => p.Total > 0)
                .OrderByDescending(p => p.Total).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
    }

    /// <summary>One program's bytes on one day.</summary>
    public long DayTotal(DateOnly day, string name)
    {
        lock (gate) return days.TryGetValue(Key(day, name), out var v) ? (long)(v.rx + v.tx) : 0;
    }

    /// <summary>Drops days before <paramref name="keepFrom"/>.</summary>
    public void Prune(DateOnly keepFrom)
    {
        lock (gate)
            foreach (var k in days.Keys.Where(k => k.day < keepFrom).ToList()) { days.Remove(k); Dirty = true; }
    }

    public void Clear()
    {
        lock (gate) { days.Clear(); Dirty = true; }
    }

    // ---------- CSV ----------

    public void Write(TextWriter w)
    {
        w.WriteLine(Header);
        lock (gate)
        {
            foreach (var (k, v) in days.OrderBy(kv => kv.Key.day).ThenBy(kv => kv.Key.name, StringComparer.Ordinal))
                w.WriteLine(string.Join(',', k.day.ToString(DateFormat, Inv), UsageLedger.Csv(names.GetValueOrDefault(k.name, k.name)),
                    UsageLedger.Csv(v.path), ((long)v.rx).ToString(Inv), ((long)v.tx).ToString(Inv)));
            Dirty = false;
        }
    }

    /// <summary>Reads a CSV written by <see cref="Write"/>, adding to what is already here. Bad lines are skipped.</summary>
    public int Read(TextReader r)
    {
        int count = 0;
        string? line;
        while ((line = r.ReadLine()) != null)
        {
            if (line.Length == 0 || line.StartsWith("date,", StringComparison.Ordinal)) continue;
            var f = UsageLedger.SplitCsv(line);
            if (f.Count < 5) continue;
            if (!DateOnly.TryParseExact(f[0], DateFormat, Inv, DateTimeStyles.None, out var day)) continue;
            if (!long.TryParse(f[3], NumberStyles.Integer, Inv, out var rx) || !long.TryParse(f[4], NumberStyles.Integer, Inv, out var tx)) continue;
            Add(day, f[1], f[2], rx, tx);
            count++;
        }
        lock (gate) Dirty = false;
        return count;
    }
}
