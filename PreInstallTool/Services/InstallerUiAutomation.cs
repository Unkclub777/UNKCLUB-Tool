using System.Diagnostics;

using FlaUI.Core;

using FlaUI.Core.AutomationElements;

using FlaUI.Core.Definitions;

using FlaUI.UIA3;

using PreInstallTool.Localization;



namespace PreInstallTool.Services;



public static class InstallerUiAutomation

{

    private static readonly string[] NextButtonNames =

    [

        "Sonraki >", "Sonraki", "İleri >", "İleri", "Next >", "Next", "&Sonraki >", "&Next >"

    ];



    private static readonly string[] FinishButtonNames =

    [

        "Bitir", "Finish", "Kapat", "Close", "Tamam", "OK", "&Finish", "&Bitir"

    ];



    private static readonly string[] AcceptHints =

    [

        "accept", "kabul", "i accept", "sözleşmeyi kabul", "lisans"

    ];



    private static readonly string[] YesInstallHints =

    [

        "yes", "evet", "install", "kur", "run", "çalıştır"

    ];



    public static string GetWindowTitle(AutomationElement window) =>

        window.AsWindow()?.Title ?? window.Name ?? string.Empty;



    public static void SelectAcceptAgreement(AutomationElement window, IProgress<string>? log)

    {

        var radios = window.FindAllDescendants(cf => cf.ByControlType(ControlType.RadioButton));

        foreach (var radio in radios)

        {

            var name = radio.Name ?? string.Empty;

            if (AcceptHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)))

            {

                TrySelect(radio);

                log?.Report(LocalizationService.Get("LicenseAccepted"));

                return;

            }

        }



        if (radios.Length >= 2)

        {

            radios.OrderByDescending(r => r.BoundingRectangle.Bottom).FirstOrDefault()?.Click();

        }

    }



    public static void EnsureBingBarUnchecked(AutomationElement window, IProgress<string>? log)

    {

        foreach (var checkbox in window.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox)))

        {

            var name = checkbox.Name ?? string.Empty;

            if (!name.Contains("Bing", StringComparison.OrdinalIgnoreCase))

            {

                continue;

            }



            if (checkbox.Patterns.Toggle.IsSupported &&

                checkbox.Patterns.Toggle.Pattern.ToggleState == ToggleState.On)

            {

                checkbox.Patterns.Toggle.Pattern.Toggle();

                log?.Report(LocalizationService.Get("BingBarUnchecked"));

            }

        }

    }



    public static bool TryDismissCommonDialogs(IProgress<string>? log)

    {

        var handled = false;



        using var automation = new UIA3Automation();

        foreach (var process in Process.GetProcesses())

        {

            if (process.HasExited || process.MainWindowHandle == IntPtr.Zero)

            {

                continue;

            }



            try

            {

                using var app = Application.Attach(process);

                foreach (var window in app.GetAllTopLevelWindows(automation))

                {

                    if (TryClickFinish(window, log) ||

                        TryClickNext(window, log) ||

                        TryClickYesInstall(window, log))

                    {

                        handled = true;

                    }

                }

            }

            catch

            {

                // ignored

            }

        }



        return handled;

    }



    public static bool TryClickNext(AutomationElement window, IProgress<string>? log)

    {

        foreach (var label in NextButtonNames)

        {

            var button = FindButton(window, label);

            if (button is not null && button.IsEnabled && !button.IsOffscreen)

            {

                button.Click();

                log?.Report(LocalizationService.Format("ButtonClicked", label));

                return true;

            }

        }



        foreach (var button in window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)))

        {

            var name = button.Name ?? string.Empty;

            if ((name.Contains("Sonraki", StringComparison.OrdinalIgnoreCase) ||

                 name.Contains("Next", StringComparison.OrdinalIgnoreCase) ||

                 name.Contains("İleri", StringComparison.OrdinalIgnoreCase)) &&

                button.IsEnabled && !button.IsOffscreen)

            {

                button.Click();

                log?.Report(LocalizationService.Format("ButtonClicked", name));

                return true;

            }

        }



        return false;

    }



    public static bool TryClickFinish(AutomationElement window, IProgress<string>? log)

    {

        foreach (var label in FinishButtonNames)

        {

            var button = FindButton(window, label);

            if (button is not null && button.IsEnabled && !button.IsOffscreen)

            {

                button.Click();

                log?.Report(LocalizationService.Format("ButtonClicked", label));

                return true;

            }

        }



        return false;

    }



    public static bool IsProgressPage(AutomationElement window)

    {

        if (window.FindAllDescendants(cf => cf.ByControlType(ControlType.ProgressBar)).Length > 0)

        {

            return true;

        }



        return window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))

            .Any(t =>

            {

                var text = t.Name ?? string.Empty;

                return text.Contains("download", StringComparison.OrdinalIgnoreCase) ||

                       text.Contains("indir", StringComparison.OrdinalIgnoreCase) ||

                       text.Contains("install", StringComparison.OrdinalIgnoreCase) ||

                       text.Contains("kurul", StringComparison.OrdinalIgnoreCase);

            });

    }



    private static bool TryClickYesInstall(AutomationElement window, IProgress<string>? log)

    {

        foreach (var button in window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)))

        {

            var name = button.Name ?? string.Empty;

            if (YesInstallHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)) &&

                button.IsEnabled && !button.IsOffscreen)

            {

                button.Click();

                log?.Report(LocalizationService.Format("ConfirmButtonClicked", name));

                return true;

            }

        }



        return false;

    }



    private static AutomationElement? FindButton(AutomationElement window, string name) =>

        window.FindFirstDescendant(cf =>

            cf.ByControlType(ControlType.Button).And(cf.ByName(name)));



    private static void TrySelect(AutomationElement radio)

    {

        if (radio.Patterns.SelectionItem.IsSupported)

        {

            radio.Patterns.SelectionItem.Pattern.Select();

        }

        else

        {

            radio.Click();

        }

    }

}

