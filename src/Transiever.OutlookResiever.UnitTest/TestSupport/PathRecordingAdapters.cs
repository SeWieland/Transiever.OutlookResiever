using Transiever.OutlookResiever.Cli;
using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Application;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.UnitTest;

internal sealed class RecordingOutlookRuleExporter(IOutlookRuleExporter inner)
    : IOutlookRuleExporter
{
    public int ExportCount { get; private set; }

    public OutlookRuleExportResult? LastResult { get; private set; }

    public OutlookRuleExportResult Export()
    {
        ExportCount++;
        return LastResult = inner.Export();
    }
}

internal sealed class RecordingSynchronizationWorkflow(ISieveSynchronizationWorkflow inner)
    : ISieveSynchronizationWorkflow
{
    public int PreviewCount { get; private set; }

    public int DeployCount { get; private set; }

    public int RestoreHistoryCount { get; private set; }

    public PreviewSynchronizationRequest? LastPreviewRequest { get; private set; }

    public PreviewSynchronizationResult? LastPreviewResult { get; private set; }

    public DeploySynchronizationRequest? LastDeployRequest { get; private set; }

    public DeploySynchronizationResult? LastDeployResult { get; private set; }

    public HistoryRestoreRequest? LastHistoryRestoreRequest { get; private set; }

    public HistoryRestoreResult? LastHistoryRestoreResult { get; private set; }

    public async Task<PreviewSynchronizationResult> PreviewAsync(
        PreviewSynchronizationRequest request,
        CancellationToken cancellationToken)
    {
        PreviewCount++;
        LastPreviewRequest = request;
        return LastPreviewResult = await inner.PreviewAsync(request, cancellationToken);
    }

    public async Task<DeploySynchronizationResult> DeployAsync(
        DeploySynchronizationRequest request,
        CancellationToken cancellationToken)
    {
        DeployCount++;
        LastDeployRequest = request;
        return LastDeployResult = await inner.DeployAsync(request, cancellationToken);
    }

    public Task<RollbackSynchronizationResult> RollbackAsync(
        RollbackSynchronizationRequest request,
        CancellationToken cancellationToken) =>
        inner.RollbackAsync(request, cancellationToken);

    public Task<HistoryListResult> ListHistoryAsync(
        HistoryListRequest request,
        CancellationToken cancellationToken) =>
        inner.ListHistoryAsync(request, cancellationToken);

    public Task<HistoryShowResult> ShowHistoryAsync(
        HistoryShowRequest request,
        CancellationToken cancellationToken) =>
        inner.ShowHistoryAsync(request, cancellationToken);

    public async Task<HistoryRestoreResult> RestoreHistoryAsync(
        HistoryRestoreRequest request,
        CancellationToken cancellationToken)
    {
        RestoreHistoryCount++;
        LastHistoryRestoreRequest = request;
        return LastHistoryRestoreResult = await inner.RestoreHistoryAsync(
            request,
            cancellationToken);
    }

    public Task<HistoryDeleteResult> DeleteHistoryAsync(
        HistoryDeleteRequest request,
        CancellationToken cancellationToken) =>
        inner.DeleteHistoryAsync(request, cancellationToken);

    public Task<HistoryPruneResult> PruneHistoryAsync(
        HistoryPruneRequest request,
        CancellationToken cancellationToken) =>
        inner.PruneHistoryAsync(request, cancellationToken);
}

internal sealed class FixedSynchronizationInteraction(bool adopt)
    : ISynchronizationInteraction
{
    public bool ResolveAdoption(bool? explicitChoice, int compatibleRuleCount) => adopt;
}

internal sealed class FixedSieveServerConfigurationProvider(
    SieveServerConfiguration configuration) : ISieveServerConfigurationProvider
{
    public SieveServerConfiguration GetConfiguration(CommandLineOptions options) =>
        configuration;
}
