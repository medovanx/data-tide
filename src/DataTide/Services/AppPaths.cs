namespace DataTide.Services;

/// <summary>Where Data Tide keeps its files: %APPDATA%\DataTide.</summary>
static class AppPaths
{
    public static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DataTide");
    public static string Settings => Path.Combine(Data, "settings.json");
    public static string Usage => Path.Combine(Data, "usage.csv");
}
