namespace DataTide.Services;

/// <summary>One program in the flyout: its live speeds and the data it moved today since Data Tide started watching.</summary>
public sealed record ProgramUsage(string Path, string Name, double Down, double Up, long Today);
