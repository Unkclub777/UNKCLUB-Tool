namespace PreInstallTool.Localization;

public sealed class LanguageOption
{
    public LanguageOption(string cultureName, string displayName)
    {
        CultureName = cultureName;
        DisplayName = displayName;
    }

    public string CultureName { get; }
    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}
