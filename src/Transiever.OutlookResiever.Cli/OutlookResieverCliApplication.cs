using Transiever.OutlookResiever.Application;
using Transiever.SieveRuler.Application;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli;

public sealed class OutlookResieverCliApplication(
    OutlookExportApplication outlook,
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
            OutlookResieverCommand.Rollback => RollbackLatestAsync(options, cancellationToken),
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

    private async Task<int> RunWorkflowAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        ExportRulesResult export = await outlook.ExportAsync(
            new ExportRulesRequest(
                options.RulesFile,
                options.DryRun || !options.WriteArtifacts),
            cancellationToken);
        ConsolePresentation.PrintExportDiagnostics(export.Diagnostics);
        PrintExportResult(export);

        RuleOptimizationMode? optimizationMode = runInteraction.ResolveOptimization(
            options.OptimizationMode,
            options.OptimizationChoiceSpecified);
        SieveServerConfiguration configuration =
            configurationProvider.GetConfiguration(options);
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
                options.ScriptName,
                options.WriteArtifacts),
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
            preview.TargetScriptName ?? options.PlanFile))
        {
            Console.WriteLine("Deployment skipped. No server changes were made.");
            return 0;
        }

        DeploySynchronizationResult deploy = await synchronization.DeployAsync(
            new DeploySynchronizationRequest(
                configuration,
                options.PlanFile,
                HistoryLimit: options.HistoryLimit,
                PruneHistory: options.PruneHistory,
                Plan: preview.Plan ??
                    throw new InvalidOperationException(
                        "Preview did not return deployment metadata.")),
            cancellationToken);

        return PrintDeployResult(deploy);
    }

    private async Task<int> RollbackLatestAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        HistoryRestoreResult result = await synchronization.RestoreHistoryAsync(
            new HistoryRestoreRequest(
                configurationProvider.GetConfiguration(options),
                "latest",
                DryRun: options.DryRun),
            cancellationToken);

        return PrintHistoryRestoreResult(result);
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

    private static int PrintHistoryRestoreResult(HistoryRestoreResult result)
    {
        switch (result.Status)
        {
            case HistoryRestoreStatus.PlanValidated:
                Console.WriteLine(
                    $"Latest backup '{result.SourceScriptName}' can be restored. No server changes were made.");
                return 0;
            case HistoryRestoreStatus.AlreadyActive:
                Console.WriteLine(
                    $"Latest backup '{result.SourceScriptName}' already matches the active state.");
                return 0;
            case HistoryRestoreStatus.RestoredScript:
                Console.WriteLine(
                    $"Restored latest backup '{result.SourceScriptName}' into active script '{result.TargetScriptName}'. Backup '{result.BackupScriptName}' was retained.");
                return 0;
            case HistoryRestoreStatus.DisabledActive:
                Console.WriteLine(
                    $"Restored original no-active state from '{result.SourceScriptName}'. Backup '{result.BackupScriptName}' was retained.");
                return 0;
            default:
                throw new InvalidOperationException(
                    $"Unsupported history restore status: {result.Status}");
        }
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
