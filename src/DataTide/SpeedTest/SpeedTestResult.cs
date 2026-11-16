namespace DataTide.SpeedTest;

/// <summary>A finished test. Speeds are in bits per second.</summary>
public sealed record SpeedTestResult(DateTime When, double PingMs, double JitterMs, double DownBitsPerSecond, double UpBitsPerSecond);
