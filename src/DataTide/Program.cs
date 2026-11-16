namespace DataTide;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // The elevated limiter helper is this same exe, started with --limiter (see LimiterClient).
        if (args.Length > 0 && args[0] == "--limiter")
            return LimiterHost.Run(args);
        if (args.Length > 1 && args[0] == "--shots")
            return Shots.Run(args[1]);
        if (args.Length > 1 && args[0] == "--render-icon")
            return IconMaker.Run(args[1]);

        using var mutex = new Mutex(false, @"Local\DataTide.SingleInstance");
        bool created;
        try
        {
            // After an update the old version is still exiting: wait for it to let go.
            created = mutex.WaitOne(args.Contains("--updated") ? TimeSpan.FromSeconds(20) : TimeSpan.Zero);
        }
        catch (AbandonedMutexException) { created = true; }
        if (!created)
        {
            // Already running: launching again opens the traffic flyout.
            Native.AllowSetForegroundWindow(-1); // ASFW_ANY: let the running instance take focus
            Native.PostMessage((IntPtr)0xFFFF, Native.RegisterWindowMessage(DataTideApp.ShowFlyoutMessage), IntPtr.Zero, IntPtr.Zero);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error(e.ExceptionObject as Exception);
        Application.Run(new DataTideApp());
        return 0;
    }
}
