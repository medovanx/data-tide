using DataTide.Core.Formatting;

namespace DataTide.Core.Tests;

public class ByteFormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(10 * 1024 * 1024 + 512 * 1024, "10.5 MB")]
    [InlineData(150L * 1024 * 1024 * 1024, "150 GB")]
    [InlineData(-5, "0 B")]
    public void Size(long bytes, string expected) => Assert.Equal(expected, ByteFormat.Size(bytes));

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(900, "900 B/s")]
    [InlineData(1024 * 12.34, "12.3 KB/s")]
    [InlineData(1024 * 1024 * 1.256, "1.26 MB/s")]
    public void Rate(double bps, string expected) => Assert.Equal(expected, ByteFormat.Rate(bps));

    [Fact]
    public void RateInBits() => Assert.Equal("8 Mb/s", ByteFormat.Rate(1_000_000, bits: true));

    [Theory]
    [InlineData("500", 500L)]
    [InlineData("500K", 512000L)]
    [InlineData("1.5 MB/s", 1572864L)]
    [InlineData("2m", 2097152L)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    public void ParseRate(string text, long? expected) => Assert.Equal(expected, ByteFormat.ParseRate(text));
}
