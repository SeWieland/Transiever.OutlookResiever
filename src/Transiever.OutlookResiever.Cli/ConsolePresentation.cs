using Transiever.OutlookResiever.Application;
using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.Cli;

public static class ConsolePresentation
{
    public static void PrintHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  olrx run      Export Outlook rules, preview server changes, then ask before upload and activation.");
        Console.WriteLine("  olrx preview  Preview server changes from an existing rules JSON file. Does not read Outlook.");
        Console.WriteLine("  olrx deploy   Deploy the exact previewed candidate, preserving the active script name by default.");
        Console.WriteLine("  olrx rollback Restore the deployment plan backup or reactivate the previous source script.");
        Console.WriteLine();
        Console.WriteLine("Advanced commands:");
        Console.WriteLine("  olrx export   Export supported Outlook rules to rules.json.");
        Console.WriteLine("  olrx inspect  Inspect an existing rules JSON file.");
        Console.WriteLine("  olrx optimize Optimize rules JSON for review.");
        Console.WriteLine("  olrx generate Generate a local Sieve script from rules JSON.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --rules <file>   Export destination or input rules JSON.");
        Console.WriteLine("  --output <file>  Optimized rules JSON.");
        Console.WriteLine("  --sieve <file>   Generated Sieve script.");
        Console.WriteLine("  --candidate <file>        Reconciled candidate script.");
        Console.WriteLine("  --reconciled-rules <file> Combined rules review document.");
        Console.WriteLine("  --candidate-rules <file>  Rules rendered into the candidate script.");
        Console.WriteLine("  --server-snapshot <file>  Downloaded active script.");
        Console.WriteLine("  --plan <file>             Deployment plan.");
        Console.WriteLine("  --script-name <name>      Override the preview target script name.");
        Console.WriteLine("  --adopt-compatible        Adopt compatible external rules.");
        Console.WriteLine("  --preserve-compatible     Preserve compatible external rules.");
        Console.WriteLine("  --deploy                  In run, upload and activate after preview without prompting.");
        Console.WriteLine("  --activate                Compatibility alias for --deploy in run; deploy activates by default.");
        Console.WriteLine("  --history-limit <count>   Keep this many newest SieveRuler history scripts plus the oldest backup. Default: 5.");
        Console.WriteLine("  --no-prune-history        Disable automatic inactive SieveRuler history deletion.");
        Console.WriteLine("  --force                   Allow rollback when the current active script no longer matches the plan.");
        Console.WriteLine("  --dry-run                 Run without writing output files or mutating the server.");
        Console.WriteLine("  -h, --help       Show this help.");
        Console.WriteLine();
        Console.WriteLine("Optimization:");
        Console.WriteLine("  --optimize [conservative|balanced|aggressive]");
        Console.WriteLine("  --no-optimize");
        Console.WriteLine("  --optimize-conservative | --optimize-balanced | --optimize-aggressive");
        Console.WriteLine("  -o | -oo | -ooo    Conservative, balanced, or aggressive.");
        Console.WriteLine("  Additional 'o' characters also select aggressive.");
        Console.WriteLine();
        Console.WriteLine("Generated managed rules include provider UI metadata comments, and deploy prunes inactive SieveRuler history by default.");
    }

    public static void PrintExportDiagnostics(
        IEnumerable<OutlookRuleExportDiagnostic> diagnostics)
    {
        foreach (OutlookRuleExportDiagnostic diagnostic in diagnostics)
        {
            Console.WriteLine(
                $"Skipping rule '{diagnostic.RuleName}': {diagnostic.Message}");
        }
    }

    public static void PrintOptimization(RuleOptimizationResult result)
    {
        Console.WriteLine(
            $"Optimized {result.OriginalRuleCount} rules into {result.OptimizedRuleCount} rules.");

        foreach (RuleOptimizationDiagnostic diagnostic in result.Diagnostics)
        {
            Console.WriteLine(
                $"[{diagnostic.Severity}] {diagnostic.Action}: {diagnostic.Message}");
        }
    }

    public static void PrintReconciliationDiagnostics(
        IEnumerable<ReconciliationDiagnostic> diagnostics)
    {
        foreach (ReconciliationDiagnostic diagnostic in diagnostics)
        {
            Console.WriteLine(
                $"[{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}");
        }
    }
}
