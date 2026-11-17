using Windows.Networking.Connectivity;

namespace DataTide.Services;

/// <summary>
/// Whether Windows treats the internet connection as metered (a phone hotspot, a mobile plan, or one the user
/// set as metered in Settings), from its connection cost. Rechecked when the network changes and every 30 s.
/// </summary>
sealed class MeteredWatcher : IDisposable
{
    readonly Control ui;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 30_000 };
    bool started;

    public bool Metered { get; private set; }
    public event Action? Changed;

    public MeteredWatcher(Control ui)
    {
        this.ui = ui;
        timer.Tick += (_, _) => Check();
    }

    public void Start()
    {
        if (started) return;
        started = true;
        Metered = Read();
        NetworkInformation.NetworkStatusChanged += OnNetworkChanged;
        timer.Start();
    }

    // Raised on a thread-pool thread: hop to the UI thread before anything else.
    void OnNetworkChanged(object? sender)
    {
        try { if (ui.IsHandleCreated) ui.BeginInvoke(Check); }
        catch (InvalidOperationException) { }
    }

    void Check()
    {
        bool now = Read();
        if (now == Metered) return;
        Metered = now;
        Changed?.Invoke();
    }

    public static bool Read()
    {
        try
        {
            var cost = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost();
            return cost != null && (cost.NetworkCostType is NetworkCostType.Fixed or NetworkCostType.Variable || cost.Roaming || cost.OverDataLimit);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    public void Dispose()
    {
        if (started) NetworkInformation.NetworkStatusChanged -= OnNetworkChanged;
        timer.Dispose();
    }
}
