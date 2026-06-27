using PreInstallTool.Localization;

namespace PreInstallTool.Tests;

public class LocalizationFormatTests
{
    [Theory]
    [InlineData("pt-BR", "ErrorFix_VanguardStopCompleted", 0, 1)]
    [InlineData("pt-BR", "ErrorFix_VanguardShutdownSummary", 0, 1)]
    [InlineData("pt-BR", "ErrorFix_VanguardProcessStopSummary", 0, 1)]
    [InlineData("de", "ErrorFix_VanguardStopCompleted", 0, 1)]
    [InlineData("fr", "ErrorFix_VanguardStopCompleted", 0, 1)]
    [InlineData("zh-CN", "ErrorFix_VanguardStopCompleted", 0, 1)]
    [InlineData("ko", "ErrorFix_VanguardStopCompleted", 0, 1)]
    public void Format_DoesNotThrow_ForVanguardStopMessages(
        string cultureName,
        string key,
        params object[] args)
    {
        LocalizationService.SetLanguage(cultureName, persist: false);

        var formatted = LocalizationService.Format(key, args);

        Assert.False(string.IsNullOrWhiteSpace(formatted));
        Assert.DoesNotContain("{0}", formatted, StringComparison.Ordinal);
    }
}
