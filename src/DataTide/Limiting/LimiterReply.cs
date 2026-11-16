namespace DataTide.Limiting;

/// <summary>The helper's answer to apply and stop (status answers with a <see cref="LimiterStatus"/>).</summary>
sealed class LimiterReply
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
}
