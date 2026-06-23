using Transiever.SieveRuler.Application;
using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.Cli;

public sealed class ConsoleSynchronizationInteraction :
    ISynchronizationInteraction,
    IOutlookRunInteraction
{
    public RuleOptimizationMode? ResolveOptimization(
        RuleOptimizationMode? explicitMode,
        bool explicitChoice)
    {
        if (explicitChoice)
        {
            return explicitMode;
        }

        if (Console.IsInputRedirected)
        {
            return null;
        }

        while (true)
        {
            Console.Write(
                "Optimize managed rules before rendering? [n]one/[c]onservative/[b]alanced/[a]ggressive: ");
            string? answer = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(answer) ||
                answer.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                answer.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                answer.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (answer.Equals("c", StringComparison.OrdinalIgnoreCase) ||
                answer.Equals("conservative", StringComparison.OrdinalIgnoreCase))
            {
                return RuleOptimizationMode.Conservative;
            }

            if (answer.Equals("b", StringComparison.OrdinalIgnoreCase) ||
                answer.Equals("balanced", StringComparison.OrdinalIgnoreCase))
            {
                return RuleOptimizationMode.Balanced;
            }

            if (answer.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                answer.Equals("aggressive", StringComparison.OrdinalIgnoreCase))
            {
                return RuleOptimizationMode.Aggressive;
            }

            Console.WriteLine(
                "Enter none, conservative, balanced, or aggressive.");
        }
    }

    public bool ConfirmUpload(bool explicitlyDeploy, string scriptName)
    {
        if (explicitlyDeploy)
        {
            return true;
        }

        if (Console.IsInputRedirected)
        {
            return false;
        }

        Console.Write(
            $"Deploy candidate for target script '{scriptName}'? [y/N] ");
        string? answer = Console.ReadLine();
        return answer?.Equals("y", StringComparison.OrdinalIgnoreCase) == true ||
            answer?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true;
    }

    public bool ResolveAdoption(bool? explicitChoice, int compatibleRuleCount)
    {
        if (compatibleRuleCount == 0)
        {
            return false;
        }

        if (explicitChoice is { } choice)
        {
            return choice;
        }

        if (Console.IsInputRedirected)
        {
            Console.WriteLine(
                $"Preserving {compatibleRuleCount} compatible external rules because input is redirected.");
            return false;
        }

        Console.Write(
            $"Adopt {compatibleRuleCount} compatible server rules into the managed region? [Y/n] ");
        string? answer = Console.ReadLine();
        return string.IsNullOrWhiteSpace(answer) ||
            answer.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            answer.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

}
