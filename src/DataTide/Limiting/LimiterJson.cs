using System.Text;
using System.Text.Json;

namespace DataTide.Limiting;

/// <summary>Shared wire settings so the UI and the helper always agree: camelCase JSON, one object per line, UTF-8 without BOM.</summary>
static class LimiterJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static readonly Encoding Utf8 = new UTF8Encoding(false);
}
