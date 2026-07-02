using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;
using System.Globalization;

namespace Transiever.OutlookResiever.Cli;

public sealed class CommandLineOptions
{
    public OutlookResieverCommand Command { get; private init; } = OutlookResieverCommand.Run;

    public string RulesFile { get; private init; } = "rules.json";

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

    public bool DryRun { get; private init; }

    public int HistoryLimit { get; private init; } = 5;

    public bool PruneHistory { get; private init; } = true;

    public string? SieveHost { get; private init; }

    public int? SievePort { get; private init; }

    public string? SieveUserName { get; private init; }

    public string? SievePassword { get; private init; }

    public SieveConnectionSecurity? SieveSecurity { get; private init; }

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
        var candidateFile = "candidate.sieve";
        var reconciledRulesFile = "reconciled-rules.json";
        var candidateRulesFile = "candidate-rules.json";
        var serverSnapshotFile = "server-active.sieve";
        var planFile = "deployment-plan.json";
        string? scriptName = null;
        RuleOptimizationMode? optimizationMode = null;
        bool optimizationChoiceSpecified = false;
        bool? adoptCompatible = null;
        var deploy = false;
        var dryRun = false;
        var historyLimit = 5;
        var pruneHistory = true;
        string? sieveHost = null;
        int? sievePort = null;
        string? sieveUserName = null;
        string? sievePassword = null;
        SieveConnectionSecurity? sieveSecurity = null;

        while (index < args.Count)
        {
            string option = args[index];

            switch (option)
            {
                case "--rules":
                    rulesFile = ReadOptionValue(args, ref index, option);
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

                case "--optimize":
                    optimizationMode = ReadOptionalOptimizationMode(
                        args,
                        ref index,
                        RuleOptimizationMode.Conservative);
                    optimizationChoiceSpecified = true;
                    break;

                case "--no-optimize":
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

                case "--sieve-host":
                    sieveHost = ReadOptionValue(args, ref index, option);
                    break;

                case "--sieve-port":
                    sievePort = ReadPortOption(args, ref index, option);
                    break;

                case "--sieve-username":
                    sieveUserName = ReadOptionValue(args, ref index, option);
                    break;

                case "--sieve-password":
                    sievePassword = ReadOptionValue(args, ref index, option);
                    break;

                case "--sieve-security-mode":
                    sieveSecurity = ParseSieveSecurity(
                        ReadOptionValue(args, ref index, option));
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
            DryRun = dryRun,
            HistoryLimit = historyLimit,
            PruneHistory = pruneHistory,
            SieveHost = sieveHost,
            SievePort = sievePort,
            SieveUserName = sieveUserName,
            SievePassword = sievePassword,
            SieveSecurity = sieveSecurity
        };
    }

    private static OutlookResieverCommand? ParseCommand(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "run" => OutlookResieverCommand.Run,
            "export" => OutlookResieverCommand.Export,
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

    private static int ReadPortOption(
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
            parsed is < 1 or > 65535)
        {
            throw new ArgumentException($"{option} must be a TCP port from 1 to 65535.");
        }

        return parsed;
    }

    private static SieveConnectionSecurity ParseSieveSecurity(string value)
    {
        if (Enum.TryParse(
            value,
            ignoreCase: true,
            out SieveConnectionSecurity mode))
        {
            return mode;
        }

        throw new ArgumentException($"Unknown Sieve security mode: {value}");
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
