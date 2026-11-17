namespace DataTide.Limiting;

/// <summary>One line the UI sends the helper: {"cmd":"apply","rules":[...]}, {"cmd":"status"} or {"cmd":"stop"}.</summary>
sealed class LimiterRequest
{
    public string Cmd { get; set; } = "";
    public List<LimitRule>? Rules { get; set; }
    /// <summary>Game mode's shared limit for every program without a rule of its own, or null.</summary>
    public CatchAllLimit? Others { get; set; }
}
