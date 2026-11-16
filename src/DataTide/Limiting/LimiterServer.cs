using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace DataTide.Limiting;

/// <summary>
/// The helper's named pipe: one instance, one client at a time, newline-delimited JSON commands.
/// The ACL lets in only the interactive user and SYSTEM, and a connection is also dropped unless it comes
/// from the parent process that launched us, so no other program can steer an elevated packet filter.
/// </summary>
sealed class LimiterServer(LimiterEngine engine, int parentPid, string pipeName, SecurityIdentifier user)
{
    volatile bool clientConnected;

    public bool ClientConnected => clientConnected;

    public async Task RunAsync(CancellationTokenSource exit)
    {
        var ct = exit.Token;
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));

        // FirstPipeInstance: fail instead of joining a pipe someone else created under our name.
        using var pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 64 * 1024, 64 * 1024, security);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await pipe.WaitForConnectionAsync(ct);
            }
            catch (OperationCanceledException) { return; }

            try
            {
                if (!LimiterNative.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out int pid) || pid != parentPid) continue;
                clientConnected = true;
                using var reader = new StreamReader(pipe, LimiterJson.Utf8, false, 4096, leaveOpen: true);
                using var writer = new StreamWriter(pipe, LimiterJson.Utf8, 4096, leaveOpen: true) { NewLine = "\n" };
                while (await reader.ReadLineAsync(ct) is string line)
                {
                    string reply = Handle(line, out bool stop);
                    await writer.WriteLineAsync(reply.AsMemory(), ct);
                    await writer.FlushAsync(ct);
                    if (stop)
                    {
                        exit.Cancel();
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { } // client went away mid-message
            catch (Exception ex) { Log.Error(ex); }
            finally
            {
                clientConnected = false;
                try
                {
                    if (pipe.IsConnected) pipe.Disconnect();
                }
                catch (IOException) { }
            }
        }
    }

    /// <summary>Per-program speeds from kernel network events, started by the "monitor" command.</summary>
    public EtwNetworkMonitor? Monitor { get; private set; }

    string Handle(string line, out bool stop)
    {
        stop = false;
        LimiterRequest? req;
        try { req = JsonSerializer.Deserialize<LimiterRequest>(line, LimiterJson.Options); }
        catch (JsonException) { req = null; }
        switch (req?.Cmd.ToLowerInvariant())
        {
            case "apply":
                if (!engine.Running) return Reply(false, engine.Status().Error ?? "The speed limiter is not running.");
                engine.Apply(req.Rules ?? []);
                return Reply(true, null);
            case "status":
            {
                var st = engine.Status();
                // Kernel events see every program on every adapter, limited or not: prefer them when running.
                if (Monitor is { Running: true } m) st = st with { Processes = m.Snapshot() };
                return JsonSerializer.Serialize(st, LimiterJson.Options);
            }
            case "monitor":
                Monitor ??= new EtwNetworkMonitor();
                return Monitor.Start() ? Reply(true, null) : Reply(false, Monitor.Error);
            case "stop":
                stop = true;
                return Reply(true, null);
            default:
                return Reply(false, "Unknown command.");
        }
    }

    static string Reply(bool ok, string? error) => JsonSerializer.Serialize(new LimiterReply { Ok = ok, Error = error }, LimiterJson.Options);
}
