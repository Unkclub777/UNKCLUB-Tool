using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public static class ToastNotificationService
{
    private const string AppUserModelId = "UNKCLUB.UNKCLUBTool";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static void ShowInstallSuccessToast()
    {
        var title = LocalizationService.Get("Toast_InstallSuccessTitle");
        var message = LocalizationService.Get("Toast_InstallSuccessMessage");

        if (TryShowWinRtToast(title, message))
        {
            return;
        }

        if (TryShowPowerShellToast(title, message))
        {
            return;
        }
    }

    private static bool TryShowWinRtToast(string title, string message)
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

            var toastXmlType = Type.GetType(
                "Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType=WindowsRuntime, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime",
                throwOnError: false);

            var managerType = Type.GetType(
                "Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime",
                throwOnError: false);

            var xmlDocType = Type.GetType(
                "Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType=WindowsRuntime, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime",
                throwOnError: false);

            if (toastXmlType is null || managerType is null || xmlDocType is null)
            {
                return false;
            }

            var xml = new StringBuilder()
                .Append("<toast><visual><binding template=\"ToastGeneric\">")
                .Append("<text>").Append(EscapeXml(title)).Append("</text>")
                .Append("<text>").Append(EscapeXml(message)).Append("</text>")
                .Append("</binding></visual></toast>")
                .ToString();

            dynamic xmlDocument = Activator.CreateInstance(xmlDocType)!;
            xmlDocument.LoadXml(xml);

            dynamic toast = Activator.CreateInstance(toastXmlType, xmlDocument)!;
            dynamic? notifier = managerType
                .GetMethod("CreateToastNotifier", [typeof(string)])?
                .Invoke(null, [AppUserModelId]);

            if (notifier is null)
            {
                return false;
            }

            notifier.Show(toast);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryShowPowerShellToast(string title, string message)
    {
        try
        {
            var script = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml(@'
<toast>
  <visual>
    <binding template=""ToastGeneric"">
      <text>{EscapeXml(title)}</text>
      <text>{EscapeXml(message)}</text>
    </binding>
  </visual>
</toast>
'@)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{AppUserModelId}').Show($toast)
";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(15000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
}
