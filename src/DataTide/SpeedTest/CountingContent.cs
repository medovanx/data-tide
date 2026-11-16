using System.Net;

namespace DataTide.SpeedTest;

/// <summary>An upload body that writes the same buffer over and over and counts every byte as it leaves.</summary>
sealed class CountingContent(byte[] chunk, int repeats, Action<int> sent) : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
    {
        for (int i = 0; i < repeats; i++)
        {
            await stream.WriteAsync(chunk, token);
            sent(chunk.Length);
        }
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override bool TryComputeLength(out long length)
    {
        length = (long)chunk.Length * repeats;
        return true;
    }
}
