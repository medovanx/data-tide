using System.Diagnostics;
using System.Security.Principal;

namespace DataTide.Limiting;

/// <summary>
/// Entry point of the elevated helper, started by the UI as "DataTide.exe --limiter &lt;parentPid&gt; &lt;pipeName&gt;".
/// Packet filtering needs admin rights, the UI does not, so only this small process runs elevated.
/// It exits, putting the adapters back to normal, when the UI exits, on a stop command, or when no client
/// has been connected for 15 seconds while no rule is active.
/// </summary>
public static class LimiterHost
{
    const double IdleExitSeconds = 15;

    public static int Run(string[] args)
    {
        if (args.Length < 3 || args[0] != "--limiter" || !int.TryParse(args[1], out int parentPid) || !ValidPipeName(args[2])) return 2;
        string pipeName = args[2];

        Process parent;
        try { parent = Process.GetProcessById(parentPid); }
        catch (ArgumentException) { return 3; }

        // The pipe is for the user who runs the UI. With over-the-shoulder UAC we run as a different (admin) account,
        // so take the user from the parent's token rather than our own.
        var user = ParentUser(parentPid) ?? WindowsIdentity.GetCurrent().User!;

        using var engine = new LimiterEngine();
        try
        {
            engine.Start();
            using var exit = new CancellationTokenSource();
            void Quit()
            {
                try { exit.Cancel(); }
                catch (ObjectDisposedException) { }
            }
            try
            {
                parent.EnableRaisingEvents = true;
                parent.Exited += (_, _) => Quit();
                if (parent.HasExited) Quit();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { Quit(); }

            var server = new LimiterServer(engine, parentPid, pipeName, user);
            var serving = Task.Run(() => server.RunAsync(exit));
            var idle = Stopwatch.StartNew();
            while (!exit.IsCancellationRequested)
            {
                exit.Token.WaitHandle.WaitOne(1000);
                if (server.ClientConnected || engine.HasActiveRules) idle.Restart();
                else if (idle.Elapsed.TotalSeconds >= IdleExitSeconds) Quit();
                if (serving.IsCompleted) // the pipe could not be created or failed for good
                {
                    if (serving.Exception != null) Log.Error(serving.Exception);
                    Quit();
                }
            }
            engine.Stop();
            server.Monitor?.Dispose();
            try { serving.Wait(2000); }
            catch (AggregateException) { }
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return 1;
        }
        finally
        {
            engine.Stop();
        }
    }

    static bool ValidPipeName(string name) =>
        name.Length is > 0 and <= 200 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    static SecurityIdentifier? ParentUser(int pid)
    {
        try
        {
            using var process = LimiterNative.OpenProcess(LimiterNative.ProcessQueryLimitedInformation, false, pid);
            if (process.IsInvalid || !LimiterNative.OpenProcessToken(process, LimiterNative.TokenQuery, out var token)) return null;
            using (token)
            using (var id = new WindowsIdentity(token.DangerousGetHandle()))
                return id.User;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }
}
