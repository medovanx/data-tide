namespace DataTide.Core.Limiting;

/// <summary>
/// What the limiter should enforce right now: the saved rules (when limits are on), blocks for chosen programs
/// while the connection is metered, and game mode's shared limit for everything else while a game runs.
/// The overlays are never written into the saved rules; they come and go with the game and the network.
/// </summary>
public sealed record LimitPlan(IReadOnlyList<LimitRule> Rules, CatchAllLimit? Others)
{
    public static readonly LimitPlan Empty = new([], null);

    /// <summary>True when there is anything to enforce, so the helper must be running.</summary>
    public bool Active => Rules.Count > 0 || Others != null;

    /// <param name="limitsEnabled">The user's master switch for their own rules.</param>
    /// <param name="saved">The user's rules; disabled ones are left out.</param>
    /// <param name="gameRunning">A game from the game mode list is running (and game mode is on).</param>
    /// <param name="games">Game exe paths: exempt from the shared limit.</param>
    /// <param name="metered">The connection is metered (and the metered block is on).</param>
    /// <param name="meteredBlock">Exe paths cut off while metered; this wins over any saved rule for them.</param>
    /// <param name="ownExe">Data Tide's own exe, never caught by the shared limit.</param>
    public static LimitPlan Build(bool limitsEnabled, IReadOnlyList<LimitRule> saved,
        bool gameRunning, IReadOnlyList<string> games, long othersDown, long othersUp,
        bool metered, IReadOnlyList<string> meteredBlock, string? ownExe = null)
    {
        var rules = new Dictionary<string, LimitRule>(StringComparer.OrdinalIgnoreCase);
        if (limitsEnabled)
            foreach (var r in saved)
                if (r.Enabled && !string.IsNullOrWhiteSpace(r.ExePath)) rules[r.ExePath] = r.Clone();
        if (metered)
            foreach (var exe in meteredBlock)
                if (!string.IsNullOrWhiteSpace(exe)) rules[exe] = new LimitRule { ExePath = exe, Block = true };

        CatchAllLimit? others = null;
        if (gameRunning && (othersDown > 0 || othersUp > 0))
        {
            var except = games.Where(g => !string.IsNullOrWhiteSpace(g)).ToList();
            if (!string.IsNullOrWhiteSpace(ownExe)) except.Add(ownExe);
            others = new CatchAllLimit { DownBytesPerSecond = othersDown, UpBytesPerSecond = othersUp, Except = except };
        }
        return new LimitPlan([.. rules.Values], others);
    }
}
