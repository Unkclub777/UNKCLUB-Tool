using System.IO;
using System.Text.Json;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public enum ErrorFixPhase
{
    PreReboot,
    PostRebootPending,
    Complete
}

public static class ErrorFixStateService
{
    private const string StateFileName = "error-fix-state.json";
    private const string LegacyStateDirectory = "PreInstallTool";
    private const string StateDirectory = "UNKCLUB-Tool";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static ErrorFixPhase GetPhase()
    {
        var state = LoadState();
        return state?.Phase ?? ErrorFixPhase.PreReboot;
    }

    public static string? GetPendingModeId()
    {
        var state = LoadState();
        return state?.ModeId;
    }

    public static void MarkPostRebootPending(string? modeId = null)
    {
        SaveState(new ErrorFixState
        {
            Phase = ErrorFixPhase.PostRebootPending,
            UpdatedAt = DateTime.UtcNow,
            ModeId = modeId,
            ExecutablePath = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
        });

        PostRebootAutoStartService.WriteAutoStartLog(
            LocalizationService.Format("AutoStart_StateSavedPostRebootPending", GetStatePath()));
    }

    public static void MarkComplete() => Reset();

    public static void Reset()
    {
        PostRebootAutoStartService.Cleanup();

        foreach (var path in GetCandidateStatePaths())
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public static bool ShouldRunStep(string? stepPhase, ErrorFixPhase currentPhase)
    {
        if (string.IsNullOrWhiteSpace(stepPhase))
        {
            return true;
        }

        return stepPhase.Trim().ToLowerInvariant() switch
        {
            "pre-reboot" => currentPhase == ErrorFixPhase.PreReboot,
            "post-reboot" => currentPhase == ErrorFixPhase.PostRebootPending,
            _ => true
        };
    }

    public static bool IsPostRebootContinuation() =>
        GetPhase() == ErrorFixPhase.PostRebootPending;

    private static ErrorFixState? LoadState()
    {
        foreach (var path in GetCandidateStatePaths())
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ErrorFixState>(json, JsonOptions);
            }
            catch
            {
                // Try next location.
            }
        }

        return null;
    }

    private static void SaveState(ErrorFixState state)
    {
        var path = GetStatePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static IEnumerable<string> GetCandidateStatePaths()
    {
        yield return GetStatePath();
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LegacyStateDirectory,
            StateFileName);
    }

    private static string GetStatePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            StateDirectory,
            StateFileName);

    private sealed class ErrorFixState
    {
        public ErrorFixPhase Phase { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? ModeId { get; set; }
        public string? ExecutablePath { get; set; }
    }
}
