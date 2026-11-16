namespace DataTide.Core.SpeedTest;

/// <summary>Bytes moved by all streams together during one short slice of a speed test.</summary>
public readonly record struct SpeedSample(double Seconds, long Bytes);
