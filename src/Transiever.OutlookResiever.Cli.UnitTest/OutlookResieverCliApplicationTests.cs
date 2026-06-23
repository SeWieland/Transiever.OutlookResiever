using Transiever.OutlookResiever.Application;
using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Application;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli.UnitTest;

public sealed class OutlookResieverCliApplicationTests
{
    [Fact]
    public async Task Preview_ReadsRulesFileWithoutExportingOutlookRules()
    {
        var exporter = new FakeExporter();
        var synchronization = new FakeSynchronization();
        var interaction = new FakeRunInteraction();
        OutlookResieverCliApplication application = CreateApplication(
            exporter,
            synchronization,
            interaction);

        int exitCode = await application.RunAsync(
            CommandLineOptions.Parse(
                [
                    "preview",
                    "--rules", "rules.json",
                    "--candidate-rules", "candidate-rules.json"
                ]),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, exporter.ExportCount);
        Assert.Equal(1, synchronization.PreviewCount);
        Assert.Null(synchronization.LastPreviewRequest?.SourceDocument);
        Assert.Equal(
            "candidate-rules.json",
            synchronization.LastPreviewRequest?.CandidateRulesFile);
    }

    [Fact]
    public async Task Run_ExportsPromptsPreviewsAndSkipsUploadWhenDeclined()
    {
        string directory = CreateDirectory();

        try
        {
            var exporter = new FakeExporter();
            var synchronization = new FakeSynchronization();
            var interaction = new FakeRunInteraction
            {
                OptimizationResult = RuleOptimizationMode.Balanced,
                UploadResult = false
            };
            OutlookResieverCliApplication application = CreateApplication(
                exporter,
                synchronization,
                interaction);
            string rulesFile = Path.Combine(directory, "rules.json");

            int exitCode = await application.RunAsync(
                CommandLineOptions.Parse(["run", "--rules", rulesFile]),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, exporter.ExportCount);
            Assert.Equal(1, synchronization.PreviewCount);
            Assert.Equal(0, synchronization.DeployCount);
            Assert.NotNull(synchronization.LastPreviewRequest?.SourceDocument);
            Assert.Equal(
                RuleOptimizationMode.Balanced,
                synchronization.LastPreviewRequest?.OptimizationMode);
            Assert.False(interaction.LastExplicitOptimizationChoice);
            Assert.False(interaction.LastExplicitDeploy);
            Assert.True(File.Exists(rulesFile));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Run_DeployFlagUploadsWithoutUploadPromptResult()
    {
        string directory = CreateDirectory();

        try
        {
            var exporter = new FakeExporter();
            var synchronization = new FakeSynchronization();
            var interaction = new FakeRunInteraction
            {
                UploadResult = false
            };
            OutlookResieverCliApplication application = CreateApplication(
                exporter,
                synchronization,
                interaction);

            int exitCode = await application.RunAsync(
                CommandLineOptions.Parse(
                    [
                        "run",
                        "--rules", Path.Combine(directory, "rules.json"),
                        "--no-optimize",
                        "--deploy",
                        "--history-limit", "3"
                    ]),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, synchronization.DeployCount);
            Assert.True(interaction.LastExplicitDeploy);
            Assert.Equal(3, synchronization.LastDeployRequest?.HistoryLimit);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Rollback_DelegatesWithoutExportingOutlookRules()
    {
        var exporter = new FakeExporter();
        var synchronization = new FakeSynchronization();
        OutlookResieverCliApplication application = CreateApplication(
            exporter,
            synchronization,
            new FakeRunInteraction());

        int exitCode = await application.RunAsync(
            CommandLineOptions.Parse(["rollback", "--plan", "plan.json", "--force"]),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, exporter.ExportCount);
        Assert.Equal(1, synchronization.RollbackCount);
        Assert.True(synchronization.LastRollbackRequest?.Force);
        Assert.Equal("plan.json", synchronization.LastRollbackRequest?.PlanFile);
    }

    [Fact]
    public async Task Preview_ForwardsExplicitScriptName()
    {
        var exporter = new FakeExporter();
        var synchronization = new FakeSynchronization();
        OutlookResieverCliApplication application = CreateApplication(
            exporter,
            synchronization,
            new FakeRunInteraction());

        int exitCode = await application.RunAsync(
            CommandLineOptions.Parse(
                ["preview", "--script-name", "Open-Xchange"]),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal("Open-Xchange", synchronization.LastPreviewRequest?.TargetScriptName);
    }

    private static OutlookResieverCliApplication CreateApplication(
        FakeExporter exporter,
        FakeSynchronization synchronization,
        FakeRunInteraction interaction)
    {
        var serializer = new JsonRuleSerializer();
        return new OutlookResieverCliApplication(
            new OutlookExportApplication(exporter, serializer),
            new SieveRulerApplication(
                serializer,
                new RuleOptimizer(),
                new SieveGenerator()),
            synchronization,
            new FakeConfigurationProvider(),
            interaction);
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"OutlookResiever-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeExporter : IOutlookRuleExporter
    {
        public int ExportCount { get; private set; }

        public OutlookRuleExportResult Export()
        {
            ExportCount++;
            return new OutlookRuleExportResult
            {
                Rules =
                [
                    new RuleDefinition
                    {
                        Name = "Invoices",
                        TargetFolder = "INBOX/Billing",
                        Conditions =
                        [
                            new RuleCondition
                            {
                                Type = RuleConditionType.SubjectContains,
                                Values = ["invoice"]
                            }
                        ]
                    }
                ]
            };
        }
    }

    private sealed class FakeSynchronization : ISieveSynchronizationWorkflow
    {
        public int PreviewCount { get; private set; }

        public int DeployCount { get; private set; }

        public int RollbackCount { get; private set; }

        public PreviewSynchronizationRequest? LastPreviewRequest { get; private set; }

        public DeploySynchronizationRequest? LastDeployRequest { get; private set; }

        public RollbackSynchronizationRequest? LastRollbackRequest { get; private set; }

        public Task<PreviewSynchronizationResult> PreviewAsync(
            PreviewSynchronizationRequest request,
            CancellationToken cancellationToken)
        {
            PreviewCount++;
            LastPreviewRequest = request;
            return Task.FromResult(
                new PreviewSynchronizationResult
                {
                    Status = PreviewSynchronizationStatus.Prepared,
                    ManagedRuleCount = 1,
                    SuggestedScriptName = "srtx-test",
                    FilesWritten = true
                });
        }

        public Task<DeploySynchronizationResult> DeployAsync(
            DeploySynchronizationRequest request,
            CancellationToken cancellationToken)
        {
            DeployCount++;
            LastDeployRequest = request;
            return Task.FromResult(
                new DeploySynchronizationResult
                {
                    Status = DeploySynchronizationStatus.UploadedInactive,
                    ScriptName = "srtx-test"
                });
        }

        public Task<RollbackSynchronizationResult> RollbackAsync(
            RollbackSynchronizationRequest request,
            CancellationToken cancellationToken)
        {
            RollbackCount++;
            LastRollbackRequest = request;
            return Task.FromResult(
                new RollbackSynchronizationResult
                {
                    Status = RollbackSynchronizationStatus.ReactivatedSource,
                    TargetScriptName = "srtx-test",
                    RestoredScriptName = "source"
                });
        }

        public Task<HistoryListResult> ListHistoryAsync(
            HistoryListRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HistoryShowResult> ShowHistoryAsync(
            HistoryShowRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HistoryRestoreResult> RestoreHistoryAsync(
            HistoryRestoreRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HistoryDeleteResult> DeleteHistoryAsync(
            HistoryDeleteRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HistoryPruneResult> PruneHistoryAsync(
            HistoryPruneRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeConfigurationProvider : ISieveServerConfigurationProvider
    {
        public SieveServerConfiguration GetConfiguration() =>
            new(
                "localhost",
                SieveServerConfiguration.DefaultPort,
                "user",
                "password",
                SieveConnectionSecurity.StartTlsRequired);
    }

    private sealed class FakeRunInteraction : IOutlookRunInteraction
    {
        public RuleOptimizationMode? OptimizationResult { get; init; }

        public bool UploadResult { get; init; }

        public bool? LastExplicitOptimizationChoice { get; private set; }

        public bool? LastExplicitDeploy { get; private set; }

        public RuleOptimizationMode? ResolveOptimization(
            RuleOptimizationMode? explicitMode,
            bool explicitChoice)
        {
            LastExplicitOptimizationChoice = explicitChoice;
            return explicitChoice ? explicitMode : OptimizationResult;
        }

        public bool ConfirmUpload(bool explicitlyDeploy, string scriptName)
        {
            LastExplicitDeploy = explicitlyDeploy;
            return explicitlyDeploy || UploadResult;
        }
    }
}
