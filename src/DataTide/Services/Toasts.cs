using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace DataTide.Services;

/// <summary>
/// Windows notifications (toasts) under Data Tide's own name and icon, so the app needs no tray icon.
/// An unpackaged app gets that identity by registering an AppUserModelID under HKCU and using it for this process.
/// </summary>
static class Toasts
{
    public const string AppId = "medovanx.DataTide";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    static bool ready;

    /// <summary>Registers the app id (name and icon shown on toasts) and claims it for this process. Safe to call more than once.</summary>
    public static void Register()
    {
        if (ready) return;
        try
        {
            string icon = Path.Combine(AppPaths.Data, "toast-icon.png");
            if (!File.Exists(icon))
            {
                Directory.CreateDirectory(AppPaths.Data);
                using var bmp = IconMaker.Render(256);
                bmp.Save(icon, System.Drawing.Imaging.ImageFormat.Png);
            }
            using (var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{AppId}"))
            {
                k.SetValue("DisplayName", L.T("AppName"));
                k.SetValue("IconUri", icon);
            }
            SetCurrentProcessExplicitAppUserModelID(AppId);
            ready = true;
        }
        catch (Exception ex) { Log.Error(ex); }
    }

    /// <summary>Shows a notification with a title and one line of text. Failures are logged, never thrown.</summary>
    public static void Show(string title, string body)
    {
        try
        {
            Register();
            var xml = new XmlDocument();
            xml.LoadXml($"<toast><visual><binding template=\"ToastGeneric\"><text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(body)}</text></binding></visual></toast>");
            ToastNotificationManager.CreateToastNotifier(AppId).Show(new ToastNotification(xml));
        }
        catch (Exception ex) { Log.Error(ex); }
    }
}
