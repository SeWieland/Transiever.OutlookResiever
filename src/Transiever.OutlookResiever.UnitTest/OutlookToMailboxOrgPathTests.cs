using System.Security.Cryptography;
using System.Text;
using Transiever.ManageSieve;
using Transiever.OutlookResiever.Application;
using Transiever.OutlookResiever.Cli;
using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Application;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.UnitTest;

public sealed class OutlookToMailboxOrgPathTests
{
    private const string Path001CandidateSha256 =
        "39CD2EAC030FA0D675B0BC2832EDCB5E8EE95B6E1252F6105735C7035D735873";

    [Fact]
    public async Task Path001_RunAndRollbackPreserveTheHistoricalContract()
    {
        PathHarness path = CreatePathHarness();
        byte[] seededContent = ReadFixture(
            "PathV1",
            "PATH-001.active.sieve.base64",
            base64: true);

        int runExitCode = await path.Application.RunAsync(
            CommandLineOptions.Parse(["run", "--no-optimize", "--deploy"]),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, runExitCode);
        await AssertExportBoundaryAsync(path);
        DeploymentPlan plan = await AssertCandidateBoundaryAsync(path, seededContent);
        int deploymentOperationCount = path.Server.Operations.Count;

        path.Server.RegisterCandidate(seededContent);
        int rollbackExitCode = await path.Application.RunAsync(
            CommandLineOptions.Parse(["rollback"]),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, rollbackExitCode);
        await AssertRollbackBoundaryAsync(
            path,
            plan,
            seededContent,
            deploymentOperationCount);
    }

    private static PathHarness CreatePathHarness()
    {
        byte[] activeContent = ReadFixture("PathV1", "PATH-001.active.sieve.base64", base64: true);
        var server = new PathSieveServerConnection("Open-Xchange", activeContent);
        server.RegisterCandidate(CreateExpectedCandidate(activeContent));
        var serverFactory = new PathSieveServerConnectionFactory(server);
        var serializer = new JsonRuleSerializer();
        var importer = new SieveImporter();
        var realWorkflow = new SieveSynchronizationWorkflow(
            serializer,
            importer,
            new RuleReconciler(new RuleOptimizer()),
            new SieveScriptComposer(importer, new SieveGenerator()),
            serverFactory,
            new FixedSynchronizationInteraction(adopt: false));
        var exporter = new RecordingOutlookRuleExporter(
            new OutlookRuleExporter(
                new OutlookFolderNormalizer(),
                OutlookSyntheticTestObjects.CreateOut001));
        var synchronization = new RecordingSynchronizationWorkflow(realWorkflow);
        var application = new OutlookResieverCliApplication(
            new OutlookExportApplication(exporter, serializer),
            synchronization,
            new FixedSieveServerConfigurationProvider(TestConfiguration()),
            new ConsoleSynchronizationInteraction());
        return new PathHarness(application, exporter, synchronization, server);
    }

    private static byte[] CreateExpectedCandidate(byte[] activeContent)
    {
        var serializer = new JsonRuleSerializer();
        using var source = new MemoryStream(ReadFixture("OutlookV1", "OUT-001.rules.json"));
        RuleDocument document = serializer.LoadDocumentAsync(source).GetAwaiter().GetResult();
        var importer = new SieveImporter();
        SieveImportResult imported = importer.Import(activeContent);
        RuleReconciliationResult reconciliation = new RuleReconciler(new RuleOptimizer())
            .Reconcile(document.SourceId, document.Rules, imported, false, null);
        SieveCompositionResult composition = new SieveScriptComposer(
            importer,
            new SieveGenerator()).Compose(imported, reconciliation);
        Assert.False(composition.IsBlocked);
        return composition.Content;
    }

    private static async Task AssertExportBoundaryAsync(PathHarness path)
    {
        Assert.Equal(1, path.Exporter.ExportCount);
        OutlookRuleExportResult export = Assert.IsType<OutlookRuleExportResult>(
            path.Exporter.LastResult);
        Assert.Empty(export.Diagnostics);
        RuleDefinition rule = Assert.Single(export.Rules);

        Assert.Equal("Project invoices", rule.Name);
        Assert.Equal(1, rule.OriginalOrder);
        Assert.Null(rule.Id);
        Assert.Equal("INBOX/Projects", rule.TargetFolder);
        Assert.Equal(RuleConditionMode.All, rule.ConditionMode);
        Assert.Collection(
            rule.Conditions,
            condition => AssertCondition(condition, RuleConditionType.SubjectContains, "invoice"),
            condition => AssertCondition(condition, RuleConditionType.SenderContains, "billing@example.test"),
            condition => AssertCondition(condition, RuleConditionType.ReceiverContains, "team@example.test"),
            condition => AssertCondition(condition, RuleConditionType.HasAttachment));
        RuleCondition exception = Assert.Single(rule.Exceptions);
        AssertCondition(exception, RuleConditionType.BodyContains, "internal");
        Assert.Collection(
            rule.Actions,
            action => AssertAction(action, RuleActionType.SetFlags, "\\Seen"),
            action => AssertAction(action, RuleActionType.FileInto, "INBOX/Projects"),
            action => AssertAction(action, RuleActionType.CopyInto, "Archive/Projects"),
            action => AssertAction(action, RuleActionType.Redirect, "archive@example.test"),
            action => AssertAction(action, RuleActionType.Stop));
        Assert.Equal(RuleOwnership.Managed, rule.Ownership);
        Assert.Empty(rule.RequiredCapabilities);

        PreviewSynchronizationRequest previewRequest =
            Assert.IsType<PreviewSynchronizationRequest>(
                path.Synchronization.LastPreviewRequest);
        RuleDocument document = Assert.IsType<RuleDocument>(previewRequest.SourceDocument);
        await using var stream = new MemoryStream();
        await new JsonRuleSerializer().SaveDocumentAsync(
            document,
            stream,
            TestContext.Current.CancellationToken);
        byte[] actual = stream.ToArray();
        byte[] expected = ReadFixture("OutlookV1", "OUT-001.rules.json");

        Assert.Equal(expected, actual);
        Assert.False(actual.AsSpan().StartsWith("\uFEFF"u8));
        Assert.DoesNotContain((byte)0x0D, actual);
        Assert.Equal((byte)0x0A, actual[^1]);
        Assert.NotEqual((byte)0x0A, actual[^2]);
    }

    private static async Task<DeploymentPlan> AssertCandidateBoundaryAsync(
        PathHarness path,
        byte[] seededContent)
    {
        Assert.Equal(1, path.Synchronization.PreviewCount);
        Assert.Equal(1, path.Synchronization.DeployCount);
        PreviewSynchronizationResult preview =
            Assert.IsType<PreviewSynchronizationResult>(
                path.Synchronization.LastPreviewResult);
        DeploySynchronizationResult deploy =
            Assert.IsType<DeploySynchronizationResult>(
                path.Synchronization.LastDeployResult);
        Assert.Equal(PreviewSynchronizationStatus.Prepared, preview.Status);
        Assert.Equal(DeploySynchronizationStatus.ReplacedActive, deploy.Status);
        DeploymentPlan plan = Assert.IsType<DeploymentPlan>(preview.Plan);
        byte[] candidate = Convert.FromBase64String(plan.CandidateContentBase64);
        string actualSha256 = Convert.ToHexString(SHA256.HashData(candidate));

        Assert.Equal(plan.CandidateContentSha256, actualSha256);
        Assert.Equal(Path001CandidateSha256, actualSha256);
        AssertCandidateSemantics(candidate, seededContent);

        string targetName = Assert.IsType<string>(preview.TargetScriptName);
        string backupName = Assert.IsType<string>(deploy.BackupScriptName);
        string seededSha256 = Convert.ToHexString(SHA256.HashData(seededContent));
        Assert.True(preview.ReplacesActiveScript);
        Assert.Equal("Open-Xchange", targetName);
        Assert.Equal("Open-Xchange", plan.SourceActiveScriptName);
        Assert.Equal(seededSha256, plan.SourceContentSha256);
        Assert.Equal(targetName, plan.TargetScriptName);
        Assert.Equal(backupName, plan.BackupScriptName);
        Assert.Equal(seededSha256, plan.BackupContentSha256);
        Assert.Equal(targetName, deploy.ScriptName);
        Assert.Equal("Open-Xchange", deploy.PreviousActiveScriptName);

        RemoteSieveState state = await path.Server.ReadStateAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(targetName, state.ActiveScriptName);
        Assert.Equal(candidate, state.ActiveContent);
        Assert.Equal(
            [
                new ManageSieveScriptInfo(targetName, true),
                new ManageSieveScriptInfo(backupName, false)
            ],
            state.Scripts.OrderBy(script => script.Name, StringComparer.Ordinal));
        Assert.Equal(
            seededContent,
            await path.Server.GetScriptAsync(
                backupName,
                TestContext.Current.CancellationToken));
        Assert.Contains(
            new PathServerOperation(PathServerOperationKind.PutScript, backupName),
            path.Server.Operations);
        Assert.Contains(
            new PathServerOperation(PathServerOperationKind.PutScript, targetName),
            path.Server.Operations);

        return plan;
    }

    private static void AssertCandidateSemantics(byte[] candidate, byte[] seededContent)
    {
        SieveImportResult imported = new SieveImporter().Import(candidate);
        string text = Encoding.UTF8.GetString(candidate);

        Assert.False(imported.ManagedRegionConflict);
        Assert.Empty(imported.Diagnostics);
        Assert.Equal(
            ["body", "copy", "fileinto", "imap4flags", "mime"],
            imported.DeclaredCapabilities.Order(StringComparer.Ordinal).ToArray());
        Assert.StartsWith(
            "require [\"body\", \"copy\", \"fileinto\", \"imap4flags\", \"mime\"];\r\n",
            text,
            StringComparison.Ordinal);
        Assert.Collection(
            imported.ManagedSourceRules,
            rule =>
            {
                Assert.Equal("B8188BA86AF95144", rule.Id);
                Assert.Equal("Retained Other Source", rule.Name);
                Assert.Equal("other", rule.SourceId);
                Assert.Equal("INBOX/Other", rule.TargetFolder);
                Assert.Equal(RuleConditionMode.All, rule.ConditionMode);
                RuleCondition condition = Assert.Single(rule.Conditions);
                AssertCondition(condition, RuleConditionType.SubjectContains, "other");
                Assert.Empty(rule.Exceptions);
                Assert.Collection(
                    rule.Actions,
                    action => AssertAction(action, RuleActionType.FileInto, "INBOX/Other"),
                    action => AssertAction(action, RuleActionType.Stop));
                Assert.Equal(RuleOwnership.Managed, rule.Ownership);
                Assert.Empty(rule.RequiredCapabilities);
            },
            rule =>
            {
                Assert.Equal("BD0E7FE709C46A66", rule.Id);
                Assert.Equal("Project invoices", rule.Name);
                Assert.Equal("outlook", rule.SourceId);
                Assert.Equal("INBOX/Projects", rule.TargetFolder);
                Assert.Equal(RuleConditionMode.All, rule.ConditionMode);
                Assert.Collection(
                    rule.Conditions,
                    condition => AssertCondition(condition, RuleConditionType.SubjectContains, "invoice"),
                    condition => AssertCondition(condition, RuleConditionType.SenderContains, "billing@example.test"),
                    condition => AssertCondition(condition, RuleConditionType.ReceiverContains, "team@example.test"),
                    condition => AssertCondition(condition, RuleConditionType.HasAttachment));
                RuleCondition exception = Assert.Single(rule.Exceptions);
                AssertCondition(exception, RuleConditionType.BodyContains, "internal");
                Assert.Collection(
                    rule.Actions,
                    action => AssertAction(action, RuleActionType.SetFlags, "\\Seen"),
                    action => AssertAction(action, RuleActionType.FileInto, "INBOX/Projects"),
                    action => AssertAction(action, RuleActionType.CopyInto, "Archive/Projects"),
                    action => AssertAction(action, RuleActionType.Redirect, "archive@example.test"),
                    action => AssertAction(action, RuleActionType.Stop));
                Assert.Equal(1, rule.OriginalOrder);
                Assert.Equal(RuleOwnership.Managed, rule.Ownership);
                Assert.Empty(rule.RequiredCapabilities);
            });
        Assert.Contains(
            "## Flag: |UniqueId:89294856|Rulename: Retained Other Source\n" +
            "if header :contains \"Subject\" \"other\"",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "## Flag: |UniqueId:140970435|Rulename: Project invoices\n" +
            "if allof",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Obsolete Outlook", text, StringComparison.Ordinal);
        Assert.DoesNotContain("INBOX/Obsolete", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UniqueId:310001", text, StringComparison.Ordinal);
        AssertOpaqueSlicesPreserved(seededContent, candidate);
    }

    private static void AssertOpaqueSlicesPreserved(byte[] seededContent, byte[] candidate)
    {
        byte[] opaqueSlice = Encoding.UTF8.GetBytes(
            "# PATH-001 active input\r\n" +
            "# Provider comment retained\r\n" +
            "# opaque UTF-8 provider span: café☃\r\n");
        int seededOffset = seededContent.AsSpan().IndexOf(opaqueSlice);
        int candidateOffset = candidate.AsSpan().IndexOf(opaqueSlice);

        Assert.True(seededOffset >= 0);
        Assert.True(candidateOffset >= 0);
        Assert.Equal(
            seededContent[seededOffset..][..opaqueSlice.Length],
            candidate[candidateOffset..][..opaqueSlice.Length]);
    }

    private static async Task AssertRollbackBoundaryAsync(
        PathHarness path,
        DeploymentPlan plan,
        byte[] seededContent,
        int deploymentOperationCount)
    {
        Assert.Equal(1, path.Synchronization.RestoreHistoryCount);
        HistoryRestoreRequest request = Assert.IsType<HistoryRestoreRequest>(
            path.Synchronization.LastHistoryRestoreRequest);
        HistoryRestoreResult result = Assert.IsType<HistoryRestoreResult>(
            path.Synchronization.LastHistoryRestoreResult);
        string deploymentBackupName = Assert.IsType<string>(plan.BackupScriptName);
        string restoreBackupName = Assert.IsType<string>(result.BackupScriptName);
        byte[] candidate = Convert.FromBase64String(plan.CandidateContentBase64);

        Assert.Equal("latest", request.ScriptName);
        Assert.Equal(HistoryRestoreStatus.RestoredScript, result.Status);
        Assert.Equal(deploymentBackupName, result.SourceScriptName);
        Assert.Equal("Open-Xchange", result.TargetScriptName);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(seededContent)),
            result.RestoredContentSha256);

        RemoteSieveState state = await path.Server.ReadStateAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal("Open-Xchange", state.ActiveScriptName);
        Assert.Equal(seededContent, state.ActiveContent);
        Assert.Equal(
            new[]
            {
                new ManageSieveScriptInfo("Open-Xchange", true),
                new ManageSieveScriptInfo(deploymentBackupName, false),
                new ManageSieveScriptInfo(restoreBackupName, false)
            }.OrderBy(script => script.Name, StringComparer.Ordinal),
            state.Scripts.OrderBy(script => script.Name, StringComparer.Ordinal));
        Assert.Equal(
            seededContent,
            await path.Server.GetScriptAsync(
                deploymentBackupName,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            candidate,
            await path.Server.GetScriptAsync(
                restoreBackupName,
                TestContext.Current.CancellationToken));

        IReadOnlyList<PathServerOperation> rollbackOperations = path.Server.Operations
            .Skip(deploymentOperationCount)
            .ToArray();
        Assert.Contains(
            new PathServerOperation(PathServerOperationKind.PutScript, restoreBackupName),
            rollbackOperations);
        Assert.Contains(
            new PathServerOperation(PathServerOperationKind.PutScript, "Open-Xchange"),
            rollbackOperations);
    }

    private static void AssertCondition(
        RuleCondition condition,
        RuleConditionType type,
        params string[] values)
    {
        Assert.Equal(type, condition.Type);
        Assert.Equal(values, condition.Values);
    }

    private static void AssertAction(
        RuleAction action,
        RuleActionType type,
        params string[] values)
    {
        Assert.Equal(type, action.Type);
        Assert.Equal(values, action.Values);
    }

    private static byte[] ReadFixture(string directory, string file, bool base64 = false)
    {
        byte[] content = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", directory, file));
        return base64
            ? Convert.FromBase64String(System.Text.Encoding.UTF8.GetString(content))
            : content;
    }

    private static SieveServerConfiguration TestConfiguration() =>
        new(
            "localhost",
            SieveServerConfiguration.DefaultPort,
            "user",
            "password",
            SieveConnectionSecurity.StartTlsRequired);

    private sealed record PathHarness(
        OutlookResieverCliApplication Application,
        RecordingOutlookRuleExporter Exporter,
        RecordingSynchronizationWorkflow Synchronization,
        PathSieveServerConnection Server);

    [Fact]
    public async Task PathServer_CopiesConstructorRegistrationAndPutInputs()
    {
        byte[] activeInput = [0x10, 0x0D, 0x0A];
        var server = new PathSieveServerConnection("active", activeInput);
        ISieveServerConnection connection = server;

        activeInput[0] = 0xFF;
        Assert.Equal(
            new byte[] { 0x10, 0x0D, 0x0A },
            await connection.GetScriptAsync("active", TestContext.Current.CancellationToken));

        byte[] candidateInput = [0x20, 0x0D, 0x0A];
        server.RegisterCandidate("candidate", candidateInput);
        candidateInput[0] = 0xFF;
        await connection.CheckScriptAsync(
            new byte[] { 0x20, 0x0D, 0x0A },
            TestContext.Current.CancellationToken);

        byte[] putInput = [0x30, 0x0D, 0x0A];
        await connection.PutScriptAsync(
            "candidate",
            putInput,
            TestContext.Current.CancellationToken);
        putInput[0] = 0xFF;
        Assert.Equal(
            new byte[] { 0x30, 0x0D, 0x0A },
            await connection.GetScriptAsync("candidate", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PathServer_PreservesExactBytesAndReportsTypedMutations()
    {
        const string activeName = "path-active";
        const string candidateName = "path-candidate";
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "PathV1",
            "PATH-001.active.sieve.base64");
        byte[] seed = Convert.FromBase64String(await File.ReadAllTextAsync(
            fixturePath,
            TestContext.Current.CancellationToken));
        Assert.True(seed.AsSpan().IndexOf(new byte[] { 0xC3, 0xA9, 0xE2, 0x98, 0x83 }) >= 0);
        Assert.False(new SieveImporter().Import(seed).ManagedRegionConflict);
        byte[] candidate = [0x72, 0xC3, 0xA9, 0x0D, 0x0A, 0x00];
        var server = new PathSieveServerConnection(activeName, seed, candidateName, candidate);
        ISieveServerConnection connection = server;

        byte[] readSeed = await connection.GetScriptAsync(activeName, TestContext.Current.CancellationToken);
        readSeed[0] ^= 0xFF;
        Assert.Equal(seed, await connection.GetScriptAsync(activeName, TestContext.Current.CancellationToken));
        RemoteSieveState initialState = await connection.ReadStateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(activeName, initialState.ActiveScriptName);
        Assert.Equal(seed, initialState.ActiveContent);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(seed)), initialState.ActiveContentSha256);

        await connection.CheckScriptAsync(candidate, TestContext.Current.CancellationToken);
        Assert.True(await connection.HaveSpaceAsync(candidateName, candidate.Length, TestContext.Current.CancellationToken));
        await connection.PutScriptAsync(candidateName, candidate, TestContext.Current.CancellationToken);

        byte[] readCandidate = await connection.GetScriptAsync(candidateName, TestContext.Current.CancellationToken);
        readCandidate[^1] ^= 0xFF;
        Assert.Equal(candidate, await connection.GetScriptAsync(candidateName, TestContext.Current.CancellationToken));

        await connection.ActivateAsync(candidateName, TestContext.Current.CancellationToken);
        RemoteSieveState state = await connection.ReadStateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(candidateName, state.ActiveScriptName);
        Assert.Equal(candidate, state.ActiveContent);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(candidate)), state.ActiveContentSha256);
        state.ActiveContent[0] ^= 0xFF;
        Assert.Equal(candidate, await connection.GetScriptAsync(candidateName, TestContext.Current.CancellationToken));
        Assert.Equal(
            ["fileinto", "imap4flags"],
            state.Capabilities.SieveExtensions.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(
            [
                new ManageSieveScriptInfo(activeName, false),
                new ManageSieveScriptInfo(candidateName, true)
            ],
            state.Scripts);

        await connection.DeleteScriptAsync(activeName, TestContext.Current.CancellationToken);
        Assert.Equal(candidate, await connection.GetScriptAsync(candidateName, TestContext.Current.CancellationToken));
        Assert.Equal(
            [
                PathServerOperationKind.CheckScript,
                PathServerOperationKind.HaveSpace,
                PathServerOperationKind.PutScript,
                PathServerOperationKind.Activate,
                PathServerOperationKind.DeleteScript
            ],
            server.Operations.Select(operation => operation.Kind));
    }
}
