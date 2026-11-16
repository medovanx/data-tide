namespace DataTide.Limiting;

/// <summary>
/// The program behind a socket. <see cref="Key"/> is the upper-case path computed once, so the shaper's
/// case-insensitive lookup does not build a new string for every packet.
/// </summary>
sealed class ProcessOwner(int pid, string path)
{
    public int Pid { get; } = pid;
    public string Path { get; } = path;
    public string Key { get; } = path.ToUpperInvariant();
}
