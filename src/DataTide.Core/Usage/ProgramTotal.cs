namespace DataTide.Core.Usage;

/// <summary>Bytes one program received and sent over a range (or one day). Programs are keyed by display name.</summary>
public sealed record ProgramTotal(string Name, string Path, long Received, long Sent)
{
    public long Total => Received + Sent;
}
