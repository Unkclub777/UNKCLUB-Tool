using System.IO;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public enum InstallerRunMode
{
    Silent,
    DirectXWizard
}

public sealed record InstallerProfile(
    string Arguments,
    int[] SuccessExitCodes,
    SkipCondition? SkipIf,
    InstallerRunMode RunMode);

public static class InstallerCatalog
{
    public static readonly int[] StandardSuccessCodes = [0, 1638, 3010, 5100, -9, 2, 1];

    public static InstallerProfile GetProfile(string filePath)
    {
        var name = Path.GetFileName(filePath).ToLowerInvariant();

        if (name.StartsWith("dxwebsetup"))
        {
            return new InstallerProfile(
                string.Empty,
                [0, -9, 2, 3010],
                new SkipCondition
                {
                    PathExists = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "d3dx9_43.dll")
                },
                InstallerRunMode.DirectXWizard);
        }

        if (name.Contains("vcredist2005"))
        {
            return new InstallerProfile(
                "/Q",
                StandardSuccessCodes,
                GetVcRuntimeSkip("8.0", name),
                InstallerRunMode.Silent);
        }

        if (name.Contains("vcredist2008"))
        {
            return new InstallerProfile(
                "/Q",
                StandardSuccessCodes,
                GetVcRuntimeSkip("9.0", name),
                InstallerRunMode.Silent);
        }

        if (name.Contains("vcredist2010") || name.Contains("vcredist2012") || name.Contains("vcredist2013"))
        {
            return new InstallerProfile(
                "/quiet /norestart",
                StandardSuccessCodes,
                GetVcRuntimeSkip(name),
                InstallerRunMode.Silent);
        }

        if (name.Contains("vcredist_v14") || name.Contains("vc_redist"))
        {
            return new InstallerProfile(
                "/install /quiet /norestart",
                StandardSuccessCodes,
                GetV14Skip(name),
                InstallerRunMode.Silent);
        }

        return new InstallerProfile(string.Empty, StandardSuccessCodes, null, InstallerRunMode.Silent);
    }

    public static bool ShouldSkip(string filePath)
    {
        var profile = GetProfile(filePath);
        if (profile.SkipIf is not null && SystemChecks.EvaluateSkipCondition(profile.SkipIf))
        {
            return true;
        }

        var name = Path.GetFileName(filePath).ToLowerInvariant();
        return name.StartsWith("dxwebsetup") && SystemChecks.IsDirectX9RuntimeInstalled();
    }

    private static SkipCondition? GetV14Skip(string fileName)
    {
        var isX64 = fileName.Contains("x64") || fileName.Contains(".x64");
        var key = isX64
            ? @"HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
            : @"HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86";

        return new SkipCondition
        {
            RegistryKey = key,
            RegistryValueName = "Installed",
            RegistryExpectedValue = "1"
        };
    }

    private static SkipCondition? GetVcRuntimeSkip(string version, string fileName)
    {
        var isX64 = fileName.Contains("x64");
        var arch = isX64 ? "x64" : "x86";
        return new SkipCondition
        {
            RegistryKey = $@"HKLM\SOFTWARE\Microsoft\VisualStudio\{version}\VC\Runtimes\{arch}",
            RegistryValueName = "Installed",
            RegistryExpectedValue = "1"
        };
    }

    private static SkipCondition? GetVcRuntimeSkip(string fileName) =>
        fileName switch
        {
            _ when fileName.Contains("2013") => GetVcRuntimeSkip("12.0", fileName),
            _ when fileName.Contains("2012") => GetVcRuntimeSkip("11.0", fileName),
            _ when fileName.Contains("2010") => GetVcRuntimeSkip("10.0", fileName),
            _ => null
        };
}
