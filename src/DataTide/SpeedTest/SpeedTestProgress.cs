namespace DataTide.SpeedTest;

/// <summary>A live reading during a test: the phase, its current speed (bits per second) or ping, and how far along it is (0 to 1).</summary>
public sealed record SpeedTestProgress(SpeedTestPhase Phase, double BitsPerSecond, double PingMs, double Fraction);
