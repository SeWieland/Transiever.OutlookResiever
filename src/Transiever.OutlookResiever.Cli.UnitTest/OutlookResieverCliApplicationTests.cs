using Transiever.OutlookResiever.Application;
using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Application;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli.UnitTest;

public sealed class OutlookResieverCliApplicationTests
{
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

            int exitCode = await application.RunAsync(
                CommandLineOptions.Parse(["run"]),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, exporter.ExportCount);
            Assert.Equal(1, synchronization.PreviewCount);
            Assert.Equal(0, synchronization.DeployCount);
            Assert.NotNull(synchronization.LastPreviewRequest?.SourceDocument);
            Assert.False(synchronization.LastPreviewRequest?.WriteArtifacts);
            Assert.Equal(
                RuleOptimizationMode.Balanced,
                synchronization.LastPreviewRequest?.OptimizationMode);
            Assert.False(interaction.LastExplicitOptimizationChoice);
            Assert.False(interaction.LastExplicitDeploy);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WriteArtifactsWritesExportRulesFile()
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
                CommandLineOptions.Parse(
                    ["run", "--write-artifacts", "--rules", rulesFile]),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(rulesFile));
            Assert.True(synchronization.LastPreviewRequest?.WriteArtifacts);
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
                        "--no-optimize",
                        "--deploy",
                        "--history-limit", "3"
                    ]),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, synchronization.DeployCount);
            Assert.True(interaction.LastExplicitDeploy);
            Assert.Equal(3, synchronization.LastDeployRequest?.HistoryLimit);
            Assert.NotNull(synchronization.LastDeployRequest?.Plan);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Rollback_RestoresLatestServerBackup()
    {
        var exporter = new FakeExporter();
        var synchronization = new FakeSynchronization();
        var interaction = new FakeRunInteraction();
        OutlookResieverCliApplication application = CreateApplication(
            exporter,
            synchronization,
            interaction);

        int exitCode = await application.RunAsync(
            CommandLineOptions.Parse(["rollback"]),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, synchronization.RestoreHistoryCount);
        Assert.Equal("latest", synchronization.LastHistoryRestoreRequest?.ScriptName);
    }

    private static OutlookResieverCliApplication CreateApplication(
        FakeExporter exporter,
        FakeSynchronization synchronization,
        FakeRunInteraction interaction)
    {
        var serializer = new JsonRuleSerializer();
        return new OutlookResieverCliApplication(
            new OutlookExportApplication(exporter, serializer),
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

        public int RestoreHistoryCount { get; private set; }

        public PreviewSynchronizationRequest? LastPreviewRequest { get; private set; }

        public DeploySynchronizationRequest? LastDeployRequest { get; private set; }

        public RollbackSynchronizationRequest? LastRollbackRequest { get; private set; }

        public HistoryRestoreRequest? LastHistoryRestoreRequest { get; private set; }

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
                    TargetScriptName = "srtx-test",
                    FilesWritten = request.WriteArtifacts,
                    Plan = new DeploymentPlan
                    {
                        SourceActiveScriptName = "",
                        SourceContentSha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(
                                Array.Empty<byte>())),
                        CandidateContentBase64 = Convert.ToBase64String("keep;\r\n"u8.ToArray()),
                        CandidateContentSha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(
                                "keep;\r\n"u8.ToArray())),
                        TargetScriptName = "srtx-test"
                    }
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
            CancellationToken cancellationToken)
        {
            RestoreHistoryCount++;
            LastHistoryRestoreRequest = request;
            return Task.FromResult(
                new HistoryRestoreResult
                {
                    Status = HistoryRestoreStatus.RestoredScript,
                    SourceScriptName = "srtx-backup-20240101000000-original",
                    TargetScriptName = "Open-Xchange",
                    BackupScriptName = "srtx-backup-20240102000000-current"
                });
        }

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
        public SieveServerConfiguration GetConfiguration(CommandLineOptions options) =>
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
