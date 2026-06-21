using PreInstallTool.Localization;
using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class ConfigValidator
{
    private static readonly HashSet<string> RequiredModeIds =
        new(StringComparer.OrdinalIgnoreCase) { "first-install", "error-fix" };

    public static void ValidateOrThrow(InstallConfig config)
    {
        var errors = CollectErrors(config);
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            LocalizationService.Format("ConfigValidationFailed", string.Join(Environment.NewLine, errors)));
    }

    public static IReadOnlyList<string> CollectErrors(InstallConfig config)
    {
        var errors = new List<string>();

        if (config.Modes.Count == 0 && config.Steps.Count == 0)
        {
            errors.Add(LocalizationService.Get("ConfigNoModesOrSteps"));
            return errors;
        }

        var modes = config.Modes.Count > 0
            ? config.Modes
            : [new InstallMode { Id = "default", Steps = config.Steps }];

        foreach (var requiredId in RequiredModeIds)
        {
            if (modes.All(mode => !mode.Id.Equals(requiredId, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(LocalizationService.Format("ConfigMissingMode", requiredId));
            }
        }

        foreach (var mode in modes)
        {
            if (string.IsNullOrWhiteSpace(mode.Id))
            {
                errors.Add(LocalizationService.Get("ConfigModeMissingId"));
                continue;
            }

            if (mode.Steps.Count == 0)
            {
                errors.Add(LocalizationService.Format("ConfigModeHasNoSteps", mode.Id));
            }

            var duplicateIds = mode.Steps
                .Where(static step => !string.IsNullOrWhiteSpace(step.Id))
                .GroupBy(static step => step.Id, StringComparer.OrdinalIgnoreCase)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .ToList();

            foreach (var duplicateId in duplicateIds)
            {
                errors.Add(LocalizationService.Format("ConfigDuplicateStepId", mode.Id, duplicateId));
            }

            foreach (var step in mode.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Id))
                {
                    errors.Add(LocalizationService.Format("ConfigStepMissingId", mode.Id));
                }

                if (string.IsNullOrWhiteSpace(step.Type))
                {
                    errors.Add(LocalizationService.Format("ConfigStepMissingType", mode.Id, step.Id));
                }
            }
        }

        return errors;
    }
}
