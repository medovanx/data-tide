namespace DataTide.Limiting.Ndis;

/// <summary>One TCP/IP-bound adapter the driver filters. <see cref="Handle"/> is the driver's key for every per-adapter call.</summary>
sealed record NdisAdapter(nint Handle, string Name, string FriendlyName, uint Medium, string Mac, ushort Mtu);
