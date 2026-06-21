using System.IO;
using System.Text;
using System.Text.Json;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public static class ServiceRestoreScriptService
{
    private const string DisabledServicesFileName = "disabled-services.json";
    private const string RestoreScriptFileName = "restore-services.ps1";

    public sealed class DisabledServiceRecord
    {
        public string Name { get; init; } = string.Empty;
        public string StartType { get; init; } = "demand";
        public int OriginalStart { get; init; }
    }

    public static string MapRegistryStartToScConfig(int startValue) =>
        startValue switch
        {
            2 => "auto",
            3 => "demand",
            0 or 1 => "auto",
            _ => "demand"
        };

    public static void WriteRestoreArtifacts(
        IReadOnlyList<DisabledServiceRecord> disabledServices,
        IProgress<string>? log)
    {
        var folderPath = DesktopPathService.GetEmulatorFolderPath();
        Directory.CreateDirectory(folderPath);

        var jsonPath = Path.Combine(folderPath, DisabledServicesFileName);
        var scriptPath = Path.Combine(folderPath, RestoreScriptFileName);

        var payload = disabledServices
            .OrderBy(static record => record.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static record => new
            {
                name = record.Name,
                startType = record.StartType,
                originalStart = record.OriginalStart
            })
            .ToList();

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, jsonOptions), Encoding.UTF8);
        File.WriteAllText(scriptPath, BuildRestoreScript(payload), Encoding.UTF8);

        log?.Report(LocalizationService.Format("ServiceRestore_Saved", scriptPath));
        log?.Report(LocalizationService.Format("ServiceRestore_JsonSaved", jsonPath));
    }

    private static string BuildRestoreScript(IEnumerable<object> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# UNKCLUB Tool — restore services disabled during install");
        builder.AppendLine("# Run this script as Administrator to re-enable third-party services.");
        builder.AppendLine("# Original start types are stored in disabled-services.json");
        builder.AppendLine("$ErrorActionPreference = 'Continue'");
        builder.AppendLine("$jsonPath = Join-Path $PSScriptRoot 'disabled-services.json'");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $jsonPath)) {");
        builder.AppendLine("    Write-Error \"disabled-services.json not found beside this script.\"");
        builder.AppendLine("    exit 1");
        builder.AppendLine("}");
        builder.AppendLine("$services = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json");
        builder.AppendLine("foreach ($entry in $services) {");
        builder.AppendLine("    $name = $entry.name");
        builder.AppendLine("    $start = if ($entry.startType) { $entry.startType } else { 'demand' }");
        builder.AppendLine("    Write-Host \"Restoring $name (start= $start)...\"");
        builder.AppendLine("    & sc.exe config $name \"start= $start\" | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine("Write-Host 'Service restore completed.'");
        return builder.ToString();
    }
}
