namespace DataTide.Core.Limiting;

/// <summary>
/// A speed limit for one program, matched by the full path of its exe (every running copy shares it).
/// A limit of 0 means that direction is not limited.
/// </summary>
public sealed class LimitRule
{
    public string ExePath { get; set; } = "";
    public long DownBytesPerSecond { get; set; }
    public long UpBytesPerSecond { get; set; }
    public bool Enabled { get; set; } = true;

    public string Name => Path.GetFileNameWithoutExtension(ExePath);

    public bool Limits(Direction d) => Enabled && (d == Direction.Download ? DownBytesPerSecond : UpBytesPerSecond) > 0;

    public long RateFor(Direction d) => d == Direction.Download ? DownBytesPerSecond : UpBytesPerSecond;

    public LimitRule Clone() => (LimitRule)MemberwiseClone();
}
