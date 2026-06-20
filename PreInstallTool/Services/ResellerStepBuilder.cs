using PreInstallTool.Models;

namespace PreInstallTool.Services;

public static class ResellerStepBuilder
{
    public static InstallStep CreateOtherResellerPromptStep(string id) =>
        new()
        {
            Id = id,
            Name = "Reseller other prompt",
            Description = "Reseller other prompt",
            Type = "message",
            Enabled = true,
            MessageKey = "Reseller_OtherPromptMessage",
            MessageTitleKey = "Reseller_OtherPromptTitle"
        };

    public static List<InstallStep> ApplyResellerSteps(IReadOnlyList<InstallStep> steps, InstallMode mode)
    {
        var result = new List<InstallStep>(steps);

        if (mode.Id.Equals("first-install", StringComparison.OrdinalIgnoreCase))
        {
            if (ResellerSelectionService.IsOtherReseller)
            {
                result.RemoveAll(static step =>
                    step.Id.Equals("deploy-unkclub", StringComparison.OrdinalIgnoreCase));
                result.Add(CreateOtherResellerPromptStep("prompt-other-reseller-first"));
            }
        }
        else if ((mode.Id.Equals("error-fix", StringComparison.OrdinalIgnoreCase) ||
                  mode.Id.Equals("first-install", StringComparison.OrdinalIgnoreCase)) &&
                 ResellerSelectionService.IsOtherReseller &&
                 ErrorFixStateService.GetPhase() == ErrorFixPhase.PostRebootPending)
        {
            var runStepId = mode.Id.Equals("first-install", StringComparison.OrdinalIgnoreCase)
                ? "first-install-run-app"
                : "error-fix-run-unkclub";

            var runIndex = result.FindIndex(step =>
                step.Id.Equals(runStepId, StringComparison.OrdinalIgnoreCase));

            if (runIndex >= 0)
            {
                var promptId = mode.Id.Equals("first-install", StringComparison.OrdinalIgnoreCase)
                    ? "prompt-other-reseller-first-post"
                    : "prompt-other-reseller-errorfix";
                result.Insert(runIndex, CreateOtherResellerPromptStep(promptId));
            }
        }

        return result;
    }
}
