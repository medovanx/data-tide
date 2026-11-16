using System.Globalization;
using System.Text;

namespace DataTide.Core.Usage;

/// <summary>
/// Hourly byte counts per interface, kept in memory and stored as a small CSV
/// (hour, interface id, interface name, received, sent). Thread-safe.
/// </summary>
public sealed class UsageLedger
{
    public const string Header = "hour,interface_id,interface_name,received,sent";
    const string HourFormat = "yyyy-MM-dd HH:00";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    readonly object gate = new();
    readonly Dictionary<(DateTime hour, string id), (long rx, long tx)> hours = [];
    readonly Dictionary<string, string> names = [];

    public bool Dirty { get; private set; }

    static DateTime HourOf(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Unspecified);

    /// <summary>Adds bytes to the hour containing <paramref name="localTime"/>.</summary>
    public void Add(DateTime localTime, string interfaceId, string interfaceName, long received, long sent)
    {
        if (received <= 0 && sent <= 0) return;
        var key = (HourOf(localTime), interfaceId);
        lock (gate)
        {
            hours.TryGetValue(key, out var v);
            hours[key] = (v.rx + Math.Max(0, received), v.tx + Math.Max(0, sent));
            if (!string.IsNullOrWhiteSpace(interfaceName)) names[interfaceId] = interfaceName;
            Dirty = true;
        }
    }

    /// <summary>The name last seen for an interface, or its id.</summary>
    public string NameOf(string interfaceId)
    {
        lock (gate) return names.TryGetValue(interfaceId, out var n) ? n : interfaceId;
    }

    public IReadOnlyList<UsageRecord> Hours(DateTime fromInclusive, DateTime toExclusive)
    {
        lock (gate)
            return hours.Where(kv => kv.Key.hour >= fromInclusive && kv.Key.hour < toExclusive)
                .Select(kv => new UsageRecord(kv.Key.hour, kv.Key.id, NameOfUnlocked(kv.Key.id), kv.Value.rx, kv.Value.tx))
                .OrderBy(r => r.Hour).ThenBy(r => r.InterfaceName, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    /// <summary>Daily totals per interface for the days from <paramref name="from"/> to <paramref name="to"/>, both included.</summary>
    public IReadOnlyList<DailyUsage> Days(DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return Hours(start, end)
            .GroupBy(r => (DateOnly.FromDateTime(r.Hour), r.InterfaceId))
            .Select(g => new DailyUsage(g.Key.Item1, g.Key.InterfaceId, g.First().InterfaceName, g.Sum(r => r.Received), g.Sum(r => r.Sent)))
            .OrderBy(d => d.Date).ThenBy(d => d.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Every interface id that has any record, with its latest name.</summary>
    public IReadOnlyList<(string id, string name)> Interfaces()
    {
        lock (gate)
            return hours.Keys.Select(k => k.id).Distinct()
                .Select(id => (id, NameOfUnlocked(id)))
                .OrderBy(x => x.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public DateOnly? FirstDay()
    {
        lock (gate) return hours.Count == 0 ? null : DateOnly.FromDateTime(hours.Keys.Min(k => k.hour));
    }

    string NameOfUnlocked(string id) => names.TryGetValue(id, out var n) ? n : id;

    /// <summary>Drops hours older than <paramref name="keepFrom"/>.</summary>
    public void Prune(DateTime keepFrom)
    {
        lock (gate)
            foreach (var k in hours.Keys.Where(k => k.hour < keepFrom).ToList()) { hours.Remove(k); Dirty = true; }
    }

    public void Clear()
    {
        lock (gate) { hours.Clear(); Dirty = true; }
    }

    // ---------- CSV ----------

    public void Write(TextWriter w)
    {
        w.WriteLine(Header);
        lock (gate)
        {
            foreach (var (k, v) in hours.OrderBy(kv => kv.Key.hour).ThenBy(kv => kv.Key.id, StringComparer.Ordinal))
                w.WriteLine(string.Join(',', k.hour.ToString(HourFormat, Inv), Csv(k.id), Csv(NameOfUnlocked(k.id)),
                    v.rx.ToString(Inv), v.tx.ToString(Inv)));
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
            if (line.Length == 0 || line.StartsWith("hour,", StringComparison.Ordinal)) continue;
            var f = SplitCsv(line);
            if (f.Count < 5) continue;
            if (!DateTime.TryParseExact(f[0], HourFormat, Inv, DateTimeStyles.None, out var hour)) continue;
            if (!long.TryParse(f[3], NumberStyles.Integer, Inv, out var rx) || !long.TryParse(f[4], NumberStyles.Integer, Inv, out var tx)) continue;
            Add(hour, f[1], f[2], rx, tx);
            count++;
        }
        lock (gate) Dirty = false;
        return count;
    }

    static string Csv(string s) =>
        s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
