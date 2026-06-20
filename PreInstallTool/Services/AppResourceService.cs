using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace PreInstallTool.Services;

/// <summary>
/// Resolves installer/config paths: beside the exe (legacy folder distribution) or
/// extracted once from the embedded bundle to %LocalAppData%\UNKCLUB-Tool\.
/// </summary>
public static class AppResourceService
{
    private const string EmbeddedBundleName = "embedded.bundle.zip";
    private const string ExtractedVersionFileName = ".extracted-version";

    private static readonly object InitLock = new();
    private static string? _resourceRoot;
    private static bool _initialized;

    public static string ResourceRoot
    {
        get
        {
            EnsureInitialized();
            return _resourceRoot!;
        }
    }

    public static string GetPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(ResourceRoot, normalized));
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            _resourceRoot = ResolveResourceRoot();
            _initialized = true;
        }
    }

    private static string ResolveResourceRoot()
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        var externalInstallers = Path.Combine(baseDirectory, "Installers");
        var externalConfig = Path.Combine(baseDirectory, "install-config.json");

        if (File.Exists(externalConfig) &&
            Directory.Exists(externalInstallers) &&
            Directory.EnumerateFileSystemEntries(externalInstallers).Any())
        {
            return baseDirectory;
        }

        var extractRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UNKCLUB-Tool");

        var versionMarker = Path.Combine(extractRoot, ExtractedVersionFileName);
        var currentVersion = GetCurrentAssemblyVersionLabel();

        if (IsExtractedBundleValid(extractRoot, versionMarker, currentVersion))
        {
            return extractRoot;
        }

        ExtractEmbeddedBundle(extractRoot);
        Directory.CreateDirectory(extractRoot);
        File.WriteAllText(versionMarker, currentVersion);
        return extractRoot;
    }

    private static bool IsExtractedBundleValid(string extractRoot, string versionMarker, string currentVersion)
    {
        if (!File.Exists(versionMarker) ||
            !File.ReadAllText(versionMarker).Trim().Equals(currentVersion, StringComparison.Ordinal))
        {
            return false;
        }

        var installersRoot = Path.Combine(extractRoot, "Installers");
        var configPath = Path.Combine(extractRoot, "install-config.json");

        return Directory.Exists(installersRoot) &&
               Directory.EnumerateFileSystemEntries(installersRoot).Any() &&
               File.Exists(configPath);
    }

    private static void ExtractEmbeddedBundle(string targetRoot)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var resourceStream = assembly.GetManifestResourceStream(EmbeddedBundleName)
            ?? throw new InvalidOperationException(
                $"Embedded bundle '{EmbeddedBundleName}' was not found in the application assembly.");

        if (Directory.Exists(targetRoot))
        {
            Directory.Delete(targetRoot, recursive: true);
        }

        Directory.CreateDirectory(targetRoot);

        using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(targetRoot, overwriteFiles: true);
    }

    private static string GetCurrentAssemblyVersionLabel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+')[0].Trim();
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
