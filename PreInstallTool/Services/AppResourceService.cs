using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace PreInstallTool.Services;

/// <summary>
/// Resolves installer/config paths: beside the exe (legacy folder distribution) or
/// extracted once from the embedded bundle to %LocalAppData%\UNKCLUB-Tool\.
/// </summary>
public static class AppResourceService
{
    private const string EmbeddedBundleLogicalName = "embedded.bundle.zip";
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
        using var resourceStream = OpenEmbeddedBundleStream(assembly)
            ?? throw CreateBundleNotFoundException(assembly);

        if (Directory.Exists(targetRoot))
        {
            Directory.Delete(targetRoot, recursive: true);
        }

        Directory.CreateDirectory(targetRoot);

        using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(targetRoot, overwriteFiles: true);
    }

    private static Stream? OpenEmbeddedBundleStream(Assembly assembly)
    {
        var direct = assembly.GetManifestResourceStream(EmbeddedBundleLogicalName);
        if (direct != null)
        {
            return direct;
        }

        var defaultName = $"{assembly.GetName().Name}.{EmbeddedBundleLogicalName}";
        var defaultStream = assembly.GetManifestResourceStream(defaultName);
        if (defaultStream != null)
        {
            return defaultStream;
        }

        var suffixMatch = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(EmbeddedBundleLogicalName, StringComparison.OrdinalIgnoreCase));

        return suffixMatch != null
            ? assembly.GetManifestResourceStream(suffixMatch)
            : null;
    }

    private static InvalidOperationException CreateBundleNotFoundException(Assembly assembly)
    {
        var available = assembly.GetManifestResourceNames();
        WriteResourceDebugLog(available);

        var listed = available.Length == 0
            ? "(none)"
            : string.Join(", ", available);

        return new InvalidOperationException(
            $"Embedded bundle '{EmbeddedBundleLogicalName}' was not found in the application assembly. " +
            $"Available manifest resources: {listed}");
    }

    private static void WriteResourceDebugLog(string[] availableResources)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UNKCLUB-Tool");
            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, "embedded-resource-debug.log");
            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Assembly: {Assembly.GetExecutingAssembly().FullName}");
            builder.AppendLine($"Expected: {EmbeddedBundleLogicalName}");
            builder.AppendLine("Available manifest resources:");
            foreach (var name in availableResources)
            {
                builder.AppendLine($"  - {name}");
            }

            File.WriteAllText(logPath, builder.ToString());
        }
        catch
        {
            // Best-effort debug logging only.
        }
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
