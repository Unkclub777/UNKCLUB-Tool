using System.IO;
using System.Net.Http;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public static class DownloadProgressReporter
{
    private const int BufferSize = 81920;
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(500);

    public static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        string fileLabel,
        IProgress<string>? log,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[BufferSize];
        long totalRead = 0;
        var lastReport = DateTime.MinValue;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;

            var now = DateTime.UtcNow;
            if (now - lastReport >= ReportInterval)
            {
                Report(fileLabel, totalRead, totalBytes, log);
                lastReport = now;
            }
        }

        Report(fileLabel, totalRead, totalBytes, log);
    }

    public static void Report(string fileLabel, long bytesRead, long? totalBytes, IProgress<string>? log)
    {
        if (log is null)
        {
            return;
        }

        var megabytesRead = bytesRead / (1024.0 * 1024.0);

        if (totalBytes is > 0)
        {
            var megabytesTotal = totalBytes.Value / (1024.0 * 1024.0);
            var percent = (int)Math.Clamp(bytesRead * 100 / totalBytes.Value, 0, 100);
            log.Report(LocalizationService.Format(
                "DownloadProgressPercent",
                fileLabel,
                megabytesRead,
                megabytesTotal,
                percent));
            return;
        }

        log.Report(LocalizationService.Format("DownloadProgressUnknown", fileLabel, megabytesRead));
    }

    public static long? TryGetContentLength(HttpResponseMessage response) =>
        response.Content.Headers.ContentLength;
}
