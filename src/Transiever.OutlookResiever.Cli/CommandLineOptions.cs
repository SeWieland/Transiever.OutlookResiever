using Transiever.SieveRuler.Models;
using System.Globalization;

namespace Transiever.OutlookResiever.Cli;

public sealed class CommandLineOptions
{
    public OutlookResieverCommand Command { get; private init; } = OutlookResieverCommand.Run;

    public string RulesFile { get; private init; } = "rules.json";

    public string SieveFile { get; private init; } = "rules.sieve";

    public string OutputFile { get; private init; } = "rules.optimized.json";

    public string CandidateFile { get; private init; } = "candidate.sieve";

    public string ReconciledRulesFile { get; private init; } =
        "reconciled-rules.json";

    public string CandidateRulesFile { get; private init; } =
        "candidate-rules.json";

    public string ServerSnapshotFile { get; private init; } = "server-active.sieve";

    public string PlanFile { get; private init; } = "deployment-plan.json";

    public string? ScriptName { get; private init; }

    public RuleOptimizationMode? OptimizationMode { get; private init; }

    public bool OptimizationChoiceSpecified { get; private init; }

    public bool? AdoptCompatible { get; private init; }

    public bool Deploy { get; private init; }

    public bool Activate { get; private init; }

    public bool Force { get; private init; }

    public bool DryRun { get; private init; }

    public int HistoryLimit { get; private init; } = 5;

    public bool PruneHistory { get; private init; } = true;

    public bool ShowHelp { get; private init; }

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new CommandLineOptions { ShowHelp = true };
        }

        var index = 0;
        string commandText = args[index];

        if (IsHelp(commandText))
        {
            return new CommandLineOptions { ShowHelp = true };
        }

        OutlookResieverCommand command = ParseCommand(commandText)
            ?? throw new ArgumentException($"Unknown command: {commandText}");

        index++;

        var rulesFile = "rules.json";
        var sieveFile = "rules.sieve";
        var outputFile = "rules.optimized.json";
        var candidateFile = "candidate.sieve";
        var reconciledRulesFile = "reconciled-rules.json";
        var candidateRulesFile = "candidate-rules.json";
        var serverSnapshotFile = "server-active.sieve";
        var planFile = "deployment-plan.json";
        string? scriptName = null;
        RuleOptimizationMode? optimizationMode = command == OutlookResieverCommand.Optimize
            ? RuleOptimizationMode.Conservative
            : null;
        bool optimizationChoiceSpecified = command == OutlookResieverCommand.Optimize;
        bool? adoptCompatible = null;
        var deploy = false;
        var activate = false;
        var force = false;
        var dryRun = false;
        var historyLimit = 5;
        var pruneHistory = true;

        if (command == OutlookResieverCommand.Optimize
            && index < args.Count
            && !args[index].StartsWith("-", StringComparison.Ordinal))
        {
            optimizationMode = ParseOptimizationMode(args[index]);
            optimizationChoiceSpecified = true;
            index++;
        }

        while (index < args.Count)
        {
            string option = args[index];

            switch (option)
            {
                case "--rules":
                    rulesFile = ReadOptionValue(args, ref index, option);
                    break;

                case "--sieve":
                    sieveFile = ReadOptionValue(args, ref index, option);
                    break;

                case "--output":
                    outputFile = ReadOptionValue(args, ref index, option);
                    break;

                case "--candidate":
                    candidateFile = ReadOptionValue(args, ref index, option);
                    break;

                case "--reconciled-rules":
                    reconciledRulesFile = ReadOptionValue(args, ref index, option);
                    break;

                case "--candidate-rules":
                    candidateRulesFile = ReadOptionValue(args, ref index, option);
                    break;

                case "--server-snapshot":
                    serverSnapshotFile = ReadOptionValue(args, ref index, option);
                    break;

                case "--plan":
                    planFile = ReadOptionValue(args, ref index, option);
                    break;

                case "--script-name":
                    scriptName = ReadOptionValue(args, ref index, option);
                    break;

                case "--adopt-compatible":
                    adoptCompatible = true;
                    break;

                case "--preserve-compatible":
                    adoptCompatible = false;
                    break;

                case "--deploy":
                    deploy = true;
                    break;

                case "--activate":
                    deploy = true;
                    activate = true;
                    break;

                case "--force":
                    force = true;
                    break;

                case "--optimize":
                    optimizationMode = ReadOptionalOptimizationMode(
                        args,
                        ref index,
                        RuleOptimizationMode.Conservative);
                    optimizationChoiceSpecified = true;
                    break;

                case "--no-optimize":
                    if (command == OutlookResieverCommand.Optimize)
                    {
                        throw new ArgumentException(
                            "--no-optimize is not valid for the optimize command.");
                    }

                    optimizationMode = null;
                    optimizationChoiceSpecified = true;
                    break;

                case "--optimize-conservative":
                    optimizationMode = RuleOptimizationMode.Conservative;
                    optimizationChoiceSpecified = true;
                    break;

                case "--optimize-balanced":
                    optimizationMode = RuleOptimizationMode.Balanced;
                    optimizationChoiceSpecified = true;
                    break;

                case "--optimize-aggressive":
                    optimizationMode = RuleOptimizationMode.Aggressive;
                    optimizationChoiceSpecified = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--history-limit":
                    historyLimit = ReadNonNegativeIntOption(args, ref index, option);
                    break;

                case "--no-prune-history":
                    pruneHistory = false;
                    break;

                case "-h":
                case "--help":
                    return new CommandLineOptions { ShowHelp = true };

                default:
                    if (TryParseOptimizationShorthand(option, out RuleOptimizationMode shorthandMode))
                    {
                        optimizationMode = shorthandMode;
                        optimizationChoiceSpecified = true;
                        break;
                    }

                    throw new ArgumentException($"Unknown option: {option}");
            }

            index++;
        }

        return new CommandLineOptions
        {
            Command = command,
            RulesFile = rulesFile,
            SieveFile = sieveFile,
            OutputFile = outputFile,
            CandidateFile = candidateFile,
            ReconciledRulesFile = reconciledRulesFile,
            CandidateRulesFile = candidateRulesFile,
            ServerSnapshotFile = serverSnapshotFile,
            PlanFile = planFile,
            ScriptName = scriptName,
            OptimizationMode = optimizationMode,
            OptimizationChoiceSpecified = optimizationChoiceSpecified,
            AdoptCompatible = adoptCompatible,
            Deploy = deploy,
            Activate = activate,
            Force = force,
            DryRun = dryRun,
            HistoryLimit = historyLimit,
            PruneHistory = pruneHistory
        };
    }

    private static OutlookResieverCommand? ParseCommand(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "run" => OutlookResieverCommand.Run,
            "export" => OutlookResieverCommand.Export,
            "generate" => OutlookResieverCommand.Generate,
            "inspect" => OutlookResieverCommand.Inspect,
            "optimize" => OutlookResieverCommand.Optimize,
            "preview" => OutlookResieverCommand.Preview,
            "deploy" => OutlookResieverCommand.Deploy,
            "rollback" => OutlookResieverCommand.Rollback,
            _ => null
        };
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help";
    }

    private static string ReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string option)
    {
        index++;

        if (index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return args[index];
    }

    private static RuleOptimizationMode ReadOptionalOptimizationMode(
        IReadOnlyList<string> args,
        ref int index,
        RuleOptimizationMode defaultMode)
    {
        int valueIndex = index + 1;

        if (valueIndex >= args.Count || args[valueIndex].StartsWith("-", StringComparison.Ordinal))
        {
            return defaultMode;
        }

        index = valueIndex;
        return ParseOptimizationMode(args[valueIndex]);
    }

    private static int ReadNonNegativeIntOption(
        IReadOnlyList<string> args,
        ref int index,
        string option)
    {
        string value = ReadOptionValue(args, ref index, option);
        if (!int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int parsed) ||
            parsed < 0)
        {
            throw new ArgumentException($"{option} must be a non-negative integer.");
        }

        return parsed;
    }

    private static RuleOptimizationMode ParseOptimizationMode(string value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out RuleOptimizationMode mode))
        {
            return mode;
        }

        throw new ArgumentException($"Unknown optimization mode: {value}");
    }

    private static bool TryParseOptimizationShorthand(
        string value,
        out RuleOptimizationMode mode)
    {
        mode = default;

        if (value.Length < 2
            || value[0] != '-'
            || value[1..].Any(character => character != 'o'))
        {
            return false;
        }

        mode = value.Length switch
        {
            2 => RuleOptimizationMode.Conservative,
            3 => RuleOptimizationMode.Balanced,
            _ => RuleOptimizationMode.Aggressive
        };

        return true;
    }
}
