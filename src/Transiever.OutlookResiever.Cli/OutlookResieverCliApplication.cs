using Transiever.OutlookResiever.Application;
using Transiever.SieveRuler.Application;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli;

public sealed class OutlookResieverCliApplication(
    OutlookExportApplication outlook,
    SieveRulerApplication sieveRuler,
    ISieveSynchronizationWorkflow synchronization,
    ISieveServerConfigurationProvider configurationProvider,
    IOutlookRunInteraction runInteraction)
{
    public Task<int> RunAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken = default) =>
        options.Command switch
        {
            OutlookResieverCommand.Run => RunWorkflowAsync(options, cancellationToken),
            OutlookResieverCommand.Export => ExportAsync(options, cancellationToken),
            OutlookResieverCommand.Generate => GenerateAsync(options, cancellationToken),
            OutlookResieverCommand.Inspect => InspectAsync(options, cancellationToken),
            OutlookResieverCommand.Optimize => OptimizeAsync(options, cancellationToken),
            OutlookResieverCommand.Preview => PreviewAsync(options, cancellationToken),
            OutlookResieverCommand.Deploy => DeployAsync(options, cancellationToken),
            OutlookResieverCommand.Rollback => RollbackAsync(options, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported command: {options.Command}")
        };

    private async Task<int> ExportAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        ExportRulesResult result = await outlook.ExportAsync(
            new ExportRulesRequest(options.RulesFile, options.DryRun),
            cancellationToken);
        ConsolePresentation.PrintExportDiagnostics(result.Diagnostics);
        PrintExportResult(result);
        return 0;
    }

    private async Task<int> GenerateAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        GenerateSieveResult result = await sieveRuler.GenerateAsync(
            CreateGenerateRequest(options),
            cancellationToken);
        PrintGenerateResult(result);
        return 0;
    }

    private async Task<int> RunWorkflowAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        ExportRulesResult export = await outlook.ExportAsync(
            new ExportRulesRequest(options.RulesFile, options.DryRun),
            cancellationToken);
        ConsolePresentation.PrintExportDiagnostics(export.Diagnostics);
        PrintExportResult(export);

        RuleOptimizationMode? optimizationMode = runInteraction.ResolveOptimization(
            options.OptimizationMode,
            options.OptimizationChoiceSpecified);
        SieveServerConfiguration configuration =
            configurationProvider.GetConfiguration();
        PreviewSynchronizationResult preview = await synchronization.PreviewAsync(
            new PreviewSynchronizationRequest(
                configuration,
                options.RulesFile,
                options.ReconciledRulesFile,
                options.CandidateRulesFile,
                options.ServerSnapshotFile,
                options.CandidateFile,
                options.PlanFile,
                options.AdoptCompatible,
                optimizationMode,
                options.DryRun,
                export.Document,
                options.ScriptName),
            cancellationToken);
        ConsolePresentation.PrintReconciliationDiagnostics(preview.Diagnostics);

        int previewExitCode = PrintPreviewResult(
            preview,
            options,
            includeRulesFile: true);
        if (previewExitCode != 0 || preview.Status != PreviewSynchronizationStatus.Prepared)
        {
            return previewExitCode;
        }

        if (options.DryRun)
        {
            return 0;
        }

        if (!runInteraction.ConfirmUpload(
            options.Deploy,
            preview.TargetScriptName ?? preview.SuggestedScriptName ?? options.PlanFile))
        {
            Console.WriteLine("Deployment skipped. No server changes were made.");
            return 0;
        }

        DeploySynchronizationResult deploy = await synchronization.DeployAsync(
            new DeploySynchronizationRequest(
                configuration,
                options.PlanFile,
                options.Activate,
                HistoryLimit: options.HistoryLimit,
                PruneHistory: options.PruneHistory),
            cancellationToken);

        return PrintDeployResult(deploy);
    }

    private async Task<int> InspectAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        InspectRulesResult result = await sieveRuler.InspectAsync(
            new InspectRulesRequest(options.RulesFile),
            cancellationToken);
        RuleInspector.Print(result.Document.Rules, result.SourceFile);
        return 0;
    }

    private async Task<int> OptimizeAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        OptimizeRulesResult result = await sieveRuler.OptimizeAsync(
            new OptimizeRulesRequest(
                options.RulesFile,
                options.OutputFile,
                options.OptimizationMode ?? RuleOptimizationMode.Conservative,
                options.DryRun),
            cancellationToken);
        if (result.FilesWritten)
        {
            Console.WriteLine($"Wrote {result.OutputFile}.");
        }

        ConsolePresentation.PrintOptimization(result.Optimization);
        return 0;
    }

    private async Task<int> PreviewAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        PreviewSynchronizationResult result = await synchronization.PreviewAsync(
            new PreviewSynchronizationRequest(
                configurationProvider.GetConfiguration(),
                options.RulesFile,
                options.ReconciledRulesFile,
                options.CandidateRulesFile,
                options.ServerSnapshotFile,
                options.CandidateFile,
                options.PlanFile,
                options.AdoptCompatible,
                options.OptimizationMode,
                options.DryRun,
                TargetScriptName: options.ScriptName),
            cancellationToken);
        ConsolePresentation.PrintReconciliationDiagnostics(result.Diagnostics);

        return PrintPreviewResult(result, options, includeRulesFile: false);
    }

    private async Task<int> DeployAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        DeploySynchronizationResult result = await synchronization.DeployAsync(
            new DeploySynchronizationRequest(
                options.DryRun
                    ? null
                    : configurationProvider.GetConfiguration(),
                options.PlanFile,
                options.Activate,
                options.DryRun,
                options.HistoryLimit,
                options.PruneHistory),
            cancellationToken);

        return PrintDeployResult(result);
    }

    private async Task<int> RollbackAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        RollbackSynchronizationResult result = await synchronization.RollbackAsync(
            new RollbackSynchronizationRequest(
                options.DryRun
                    ? null
                    : configurationProvider.GetConfiguration(),
                options.PlanFile,
                options.Force,
                options.DryRun),
            cancellationToken);

        return PrintRollbackResult(result);
    }

    private static int PrintDeployResult(DeploySynchronizationResult result)
    {
        switch (result.Status)
        {
            case DeploySynchronizationStatus.PlanValidated:
                Console.WriteLine(
                    $"Deployment plan is valid for target script '{result.ScriptName}'. No server changes were made.");
                PrintDeploymentCleanup(result);
                return 0;
            case DeploySynchronizationStatus.Skipped:
                Console.WriteLine(
                    "Deployment skipped. No server changes were made.");
                PrintDeploymentCleanup(result);
                return 0;
            case DeploySynchronizationStatus.UploadedInactive:
                Console.WriteLine(
                    $"Uploaded inactive script '{result.ScriptName}'.");
                PrintDeploymentCleanup(result);
                return 0;
            case DeploySynchronizationStatus.Activated:
                Console.WriteLine(
                    $"Activated '{result.ScriptName}'. Previous script '{result.PreviousActiveScriptName}' was retained.");
                PrintDeploymentCleanup(result);
                return 0;
            case DeploySynchronizationStatus.ReplacedActive:
                Console.WriteLine(
                    $"Replaced active script '{result.ScriptName}'. Backup '{result.BackupScriptName}' was retained.");
                PrintDeploymentCleanup(result);
                return 0;
            case DeploySynchronizationStatus.InsufficientSpace:
                PrintDeploymentCleanup(result);
                return PrintError(
                    "The server reported insufficient space for the target or backup script.");
            default:
                throw new InvalidOperationException(
                    $"Unsupported deployment status: {result.Status}");
        }
    }

    private static int PrintRollbackResult(RollbackSynchronizationResult result)
    {
        switch (result.Status)
        {
            case RollbackSynchronizationStatus.PlanValidated:
                Console.WriteLine(
                    $"Rollback plan is valid for target script '{result.TargetScriptName}'. No server changes were made.");
                return 0;
            case RollbackSynchronizationStatus.ReactivatedSource:
                Console.WriteLine(
                    result.RestoredScriptName is null
                        ? "Rollback disabled active Sieve processing."
                        : $"Rollback reactivated '{result.RestoredScriptName}'.");
                return 0;
            case RollbackSynchronizationStatus.RestoredBackup:
                Console.WriteLine(
                    $"Rollback restored '{result.TargetScriptName}' from backup '{result.BackupScriptName}'.");
                return 0;
            default:
                throw new InvalidOperationException(
                    $"Unsupported rollback status: {result.Status}");
        }
    }

    private static int PrintPreviewResult(
        PreviewSynchronizationResult result,
        CommandLineOptions options,
        bool includeRulesFile)
    {
        switch (result.Status)
        {
            case PreviewSynchronizationStatus.Prepared:
                Console.WriteLine(
                    $"Prepared candidate with {result.ManagedRuleCount} managed rules. No server changes were made.");
                if (result.TargetScriptName is not null)
                {
                    Console.WriteLine(
                        result.ReplacesActiveScript
                            ? $"Target script '{result.TargetScriptName}' is the current active script and will be replaced in place during deployment."
                            : $"Target script: {result.TargetScriptName}");
                }

                if (result.FilesWritten)
                {
                    string reviewFiles = includeRulesFile
                        ? $"{options.RulesFile}, {options.ReconciledRulesFile}, {options.CandidateRulesFile}, {options.ServerSnapshotFile}, {options.CandidateFile}, and {options.PlanFile}"
                        : $"{options.ReconciledRulesFile}, {options.CandidateRulesFile}, {options.ServerSnapshotFile}, {options.CandidateFile}, and {options.PlanFile}";
                    Console.WriteLine($"Review {reviewFiles}.");
                }

                return 0;
            case PreviewSynchronizationStatus.Blocked:
                return PrintError(
                    "Candidate generation is blocked by reconciliation errors.");
            case PreviewSynchronizationStatus.MissingCapabilities:
                return PrintError(
                    $"Server does not advertise required Sieve capabilities: {string.Join(", ", result.MissingCapabilities)}.");
            case PreviewSynchronizationStatus.InsufficientSpace:
                return PrintError(
                    "The server reported insufficient space for the candidate script.");
            default:
                throw new InvalidOperationException(
                    $"Unsupported preview status: {result.Status}");
        }
    }

    private static GenerateSieveRequest CreateGenerateRequest(
        CommandLineOptions options) =>
        new(
            options.RulesFile,
            options.OutputFile,
            options.SieveFile,
            options.OptimizationMode,
            options.DryRun);

    private static void PrintGenerateResult(GenerateSieveResult result)
    {
        if (result.Optimization is not null)
        {
            ConsolePresentation.PrintOptimization(result.Optimization);
        }

        Console.WriteLine(
            result.FilesWritten
                ? $"Generated {result.SieveFile} from {result.RuleCount} rules."
                : $"Generated Sieve from {result.RuleCount} rules. No files written.");
    }

    private static void PrintExportResult(ExportRulesResult result)
    {
        Console.WriteLine(
            result.FilesWritten
                ? $"Exported {result.Document.Rules.Count} rules to {result.RulesFile}."
                : $"Exported {result.Document.Rules.Count} rules. No files written.");
    }

    private static int PrintError(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }

    private static void PrintDeploymentCleanup(DeploySynchronizationResult result)
    {
        foreach (string scriptName in result.DeletedHistoryScriptNames)
        {
            Console.WriteLine(
                $"Deleted obsolete SieveRuler history script '{scriptName}'.");
        }

        foreach (string warning in result.CleanupWarnings)
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }
    }
}
