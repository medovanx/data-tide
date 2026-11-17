namespace DataTide.Core.Limiting;

/// <summary>
/// One shared limit for every program that has no rule of its own, used by game mode: everything else together
/// gets at most this much, so a launcher or an update can't crowd out the game. <see cref="Except"/> lists exe
/// paths that stay unlimited (the games themselves, and Data Tide). DNS lookups always pass.
/// </summary>
public sealed class CatchAllLimit
{
    public long DownBytesPerSecond { get; set; }
    public long UpBytesPerSecond { get; set; }
    public List<string> Except { get; set; } = [];

    public long RateFor(Direction d) => d == Direction.Download ? DownBytesPerSecond : UpBytesPerSecond;
}
