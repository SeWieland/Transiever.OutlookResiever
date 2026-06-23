using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.Cli;

public interface IOutlookRunInteraction
{
    RuleOptimizationMode? ResolveOptimization(
        RuleOptimizationMode? explicitMode,
        bool explicitChoice);

    bool ConfirmUpload(bool explicitlyDeploy, string scriptName);
}
