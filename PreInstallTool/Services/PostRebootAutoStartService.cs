using System.Diagnostics;

using System.IO;

using Microsoft.Win32;



namespace PreInstallTool.Services;



public static class PostRebootAutoStartService

{

    public const string ContinueErrorFixArgument = "--continue-error-fix";



    private const string TaskName = "PreInstallTool_ErrorFix_Continue";

    private const string RunValueName = "PreInstallTool_ErrorFix";



    public static bool Register(IProgress<string>? log = null)

    {

        var exePath = GetExecutablePath();

        if (string.IsNullOrWhiteSpace(exePath))

        {

            WriteAutoStartLog("Kayıt başarısız: yürütülebilir dosya yolu alınamadı.");

            log?.Report("Otomatik başlatma kaydı başarısız: exe yolu bulunamadı.");

            return false;

        }



        var command = BuildLaunchCommand(exePath);

        WriteAutoStartLog($"Otomatik başlatma kaydı deneniyor. Komut: {command}");



        if (TryRegisterScheduledTask(exePath, log))

        {

            WriteAutoStartLog("Görev Zamanlayıcı kaydı başarılı.");

            log?.Report("Otomatik başlatma: Görev Zamanlayıcı ile kaydedildi.");

            return true;

        }



        WriteAutoStartLog("Görev Zamanlayıcı kaydı başarısız, RunOnce deneniyor.");

        log?.Report("Görev Zamanlayıcı kaydı başarısız; RunOnce deneniyor...");



        if (TryRegisterRunOnce(command, Registry.LocalMachine, log, "HKLM RunOnce"))

        {

            WriteAutoStartLog("HKLM RunOnce kaydı başarılı.");

            log?.Report("Otomatik başlatma: HKLM RunOnce ile kaydedildi.");

            return true;

        }



        if (TryRegisterRunOnce(command, Registry.CurrentUser, log, "HKCU RunOnce"))

        {

            WriteAutoStartLog("HKCU RunOnce kaydı başarılı.");

            log?.Report("Otomatik başlatma: HKCU RunOnce ile kaydedildi.");

            return true;

        }



        WriteAutoStartLog("RunOnce kaydı başarısız, Başlangıç (Run) anahtarı deneniyor.");

        log?.Report("RunOnce kaydı başarısız; Başlangıç (Run) anahtarı deneniyor...");



        if (TryRegisterStartupRun(command, log))

        {

            WriteAutoStartLog("HKCU Run (StartupApproved) kaydı başarılı.");

            log?.Report("Otomatik başlatma: Başlangıç (Run) anahtarı ile kaydedildi.");

            return true;

        }



        WriteAutoStartLog("Tüm otomatik başlatma yöntemleri başarısız oldu.");

        log?.Report("Otomatik başlatma kaydı başarısız: tüm yöntemler denendi.");

        return false;

    }



    public static void Cleanup()

    {

        TryDeleteScheduledTask();

        TryDeleteRunOnce(Registry.LocalMachine);

        TryDeleteRunOnce(Registry.CurrentUser);

        TryDeleteStartupRun();

    }



    private static string GetExecutablePath() =>

        Environment.ProcessPath

        ?? Process.GetCurrentProcess().MainModule?.FileName

        ?? string.Empty;



    private static string BuildLaunchCommand(string exePath) =>

        $"\"{exePath}\" {ContinueErrorFixArgument}";



    private static string BuildScheduledTaskAction(string exePath) =>

        $"\\\"{exePath}\\\" {ContinueErrorFixArgument}";



    private static string GetInteractiveUserPrincipal() =>

        $"{Environment.UserDomainName}\\{Environment.UserName}";



    private static bool TryRegisterScheduledTask(string exePath, IProgress<string>? log)

    {

        try

        {

            SystemChecks.RunProcess(

                "schtasks.exe",

                $"/Delete /TN \"{TaskName}\" /F",

                null,

                [0, 1],

                30);



            var taskAction = BuildScheduledTaskAction(exePath);

            var user = GetInteractiveUserPrincipal();

            var arguments =

                $"/Create /TN \"{TaskName}\" /TR \"{taskAction}\" /SC ONLOGON /RL HIGHEST /RU \"{user}\" /F /DELAY 0000:30";



            WriteAutoStartLog($"schtasks argümanları: {arguments}");



            var result = SystemChecks.RunProcess(

                "schtasks.exe",

                arguments,

                null,

                [0],

                30);



            if (!result.Success)

            {

                var detail = string.IsNullOrWhiteSpace(result.StandardError)

                    ? result.StandardOutput

                    : result.StandardError;

                WriteAutoStartLog($"schtasks başarısız (çıkış {result.ExitCode}): {detail}".Trim());

                log?.Report($"Görev Zamanlayıcı hatası: {detail}".Trim());

                return false;

            }



            return VerifyScheduledTaskExists();

        }

        catch (Exception ex)

        {

            WriteAutoStartLog($"schtasks istisnası: {ex.Message}");

            log?.Report($"Görev Zamanlayıcı istisnası: {ex.Message}");

            return false;

        }

    }



    private static bool VerifyScheduledTaskExists()

    {

        try

        {

            var result = SystemChecks.RunProcess(

                "schtasks.exe",

                $"/Query /TN \"{TaskName}\" /FO LIST",

                null,

                [0],

                15);



            return result.Success;

        }

        catch

        {

            return false;

        }

    }



    private static bool TryRegisterRunOnce(

        string command,

        RegistryKey hive,

        IProgress<string>? log,

        string label)

    {

        try

        {

            using var key = hive.OpenSubKey(

                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",

                writable: true);



            if (key is null)

            {

                WriteAutoStartLog($"{label}: anahtar açılamadı.");

                return false;

            }



            key.SetValue(RunValueName, command, RegistryValueKind.String);

            WriteAutoStartLog($"{label}: {command}");

            return true;

        }

        catch (Exception ex)

        {

            WriteAutoStartLog($"{label} hatası: {ex.Message}");

            log?.Report($"{label} hatası: {ex.Message}");

            return false;

        }

    }



    private static bool TryRegisterStartupRun(string command, IProgress<string>? log)

    {

        try

        {

            using var runKey = Registry.CurrentUser.OpenSubKey(

                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",

                writable: true);



            if (runKey is null)

            {

                WriteAutoStartLog("HKCU Run: anahtar açılamadı.");

                return false;

            }



            runKey.SetValue(RunValueName, command, RegistryValueKind.String);



            using var approvedKey = Registry.CurrentUser.OpenSubKey(

                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",

                writable: true);



            if (approvedKey is not null)

            {

                var enabled = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

                approvedKey.SetValue(RunValueName, enabled, RegistryValueKind.Binary);

            }



            WriteAutoStartLog($"HKCU Run: {command}");

            return true;

        }

        catch (Exception ex)

        {

            WriteAutoStartLog($"HKCU Run hatası: {ex.Message}");

            log?.Report($"Başlangıç (Run) hatası: {ex.Message}");

            return false;

        }

    }



    private static void TryDeleteScheduledTask()

    {

        try

        {

            SystemChecks.RunProcess(

                "schtasks.exe",

                $"/Delete /TN \"{TaskName}\" /F",

                null,

                [0, 1],

                30);

        }

        catch

        {

            // ignored

        }

    }



    private static void TryDeleteRunOnce(RegistryKey hive)

    {

        try

        {

            using var key = hive.OpenSubKey(

                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",

                writable: true);



            key?.DeleteValue(RunValueName, throwOnMissingValue: false);

        }

        catch

        {

            // ignored

        }

    }



    private static void TryDeleteStartupRun()

    {

        try

        {

            using var runKey = Registry.CurrentUser.OpenSubKey(

                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",

                writable: true);



            runKey?.DeleteValue(RunValueName, throwOnMissingValue: false);

        }

        catch

        {

            // ignored

        }



        try

        {

            using var approvedKey = Registry.CurrentUser.OpenSubKey(

                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",

                writable: true);



            approvedKey?.DeleteValue(RunValueName, throwOnMissingValue: false);

        }

        catch

        {

            // ignored

        }

    }



    public static void WriteAutoStartLog(string message)

    {

        try

        {

            var directory = Path.Combine(

                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),

                "PreInstallTool");



            Directory.CreateDirectory(directory);



            var logPath = Path.Combine(directory, "auto-start.log");

            File.AppendAllText(

                logPath,

                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");

        }

        catch

        {

            // ignored

        }

    }

}

