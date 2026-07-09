using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.Cli;

public static class ConsolePresentation
{
    public static void PrintHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  olrx run       Export Outlook rules, preview server changes, then ask before upload and activation.");
        Console.WriteLine("  olrx rollback  Restore the newest inactive SieveRuler backup from the server.");
        Console.WriteLine("  olrx export    Export supported Outlook rules to rules.json.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --write-artifacts         In run, write review files for inspection.");
        Console.WriteLine("  --rules <file>            Export destination or review rules JSON.");
        Console.WriteLine("  --candidate <file>        Review candidate script.");
        Console.WriteLine("  --reconciled-rules <file> Combined rules review document.");
        Console.WriteLine("  --candidate-rules <file>  Rules rendered into the candidate script.");
        Console.WriteLine("  --server-snapshot <file>  Downloaded active script.");
        Console.WriteLine("  --plan <file>             Advanced deployment plan artifact.");
        Console.WriteLine("  --script-name <name>      Override the preview target script name.");
        Console.WriteLine("  --sieve-host <host>       ManageSieve host override.");
        Console.WriteLine("  --sieve-port <port>       ManageSieve port override.");
        Console.WriteLine("  --sieve-username <name>   ManageSieve username override.");
        Console.WriteLine("  --sieve-password <value>  ManageSieve password override.");
        Console.WriteLine("  --sieve-security-mode <mode> ManageSieve security mode override.");
        Console.WriteLine("  --adopt-compatible        Adopt compatible external rules.");
        Console.WriteLine("  --preserve-compatible     Preserve compatible external rules.");
        Console.WriteLine("  --deploy                  In run, upload and activate after preview without prompting.");
        Console.WriteLine("  --history-limit <count>   Keep this many newest SieveRuler history scripts plus the oldest backup. Default: 5.");
        Console.WriteLine("  --no-prune-history        Disable automatic inactive SieveRuler history deletion.");
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
        Console.WriteLine("By default, run writes no local files and deploy retains a server-side backup for rollback.");
    }

    public static void PrintExportDiagnostics(
        IEnumerable<OutlookRuleExportDiagnostic> diagnostics)
    {
        foreach (OutlookRuleExportDiagnostic diagnostic in diagnostics)
        {
            Console.WriteLine(
                $"Rule '{diagnostic.RuleName}': {diagnostic.Message}");
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
