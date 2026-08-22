using Transiever.OutlookResiever.Application;
using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.UnitTest;

public sealed class OutlookExportApplicationTests
{
    [Fact]
    public Task Export_OUT001_MatchesCanonicalGolden() =>
        AssertGoldenAsync(
            "OUT-001",
            OutlookSyntheticTestObjects.CreateOut001,
            (result, loaded) =>
            {
                Assert.Empty(result.Diagnostics);
                Assert.Empty(loaded.Diagnostics);
                RuleDefinition rule = Assert.Single(loaded.Rules);
                Assert.Equal("Project invoices", rule.Name);
                Assert.Equal(1, rule.OriginalOrder);
                Assert.Null(rule.Id);
                Assert.Equal("INBOX/Projects", rule.TargetFolder);
                Assert.Equal(RuleConditionMode.All, rule.ConditionMode);
                Assert.Collection(
                    rule.Conditions,
                    condition =>
                    {
                        Assert.Equal(RuleConditionType.SubjectContains, condition.Type);
                        Assert.Equal(["invoice"], condition.Values);
                    },
                    condition =>
                    {
                        Assert.Equal(RuleConditionType.SenderContains, condition.Type);
                        Assert.Equal(["billing@example.test"], condition.Values);
                    },
                    condition =>
                    {
                        Assert.Equal(RuleConditionType.ReceiverContains, condition.Type);
                        Assert.Equal(["team@example.test"], condition.Values);
                    },
                    condition =>
                    {
                        Assert.Equal(RuleConditionType.HasAttachment, condition.Type);
                        Assert.Empty(condition.Values);
                    });
                RuleCondition exception = Assert.Single(rule.Exceptions);
                Assert.Equal(RuleConditionType.BodyContains, exception.Type);
                Assert.Equal(["internal"], exception.Values);
                Assert.Collection(
                    rule.Actions,
                    action =>
                    {
                        Assert.Equal(RuleActionType.SetFlags, action.Type);
                        Assert.Equal(["\\Seen"], action.Values);
                    },
                    action =>
                    {
                        Assert.Equal(RuleActionType.FileInto, action.Type);
                        Assert.Equal(["INBOX/Projects"], action.Values);
                    },
                    action =>
                    {
                        Assert.Equal(RuleActionType.CopyInto, action.Type);
                        Assert.Equal(["Archive/Projects"], action.Values);
                    },
                    action =>
                    {
                        Assert.Equal(RuleActionType.Redirect, action.Type);
                        Assert.Equal(["archive@example.test"], action.Values);
                    },
                    action =>
                    {
                        Assert.Equal(RuleActionType.Stop, action.Type);
                        Assert.Empty(action.Values);
                    });
                Assert.Equal(RuleOwnership.Managed, rule.Ownership);
                Assert.Empty(rule.RequiredCapabilities);
            });

    [Fact]
    public Task Export_OUT003_MatchesCanonicalGolden() =>
        AssertGoldenAsync(
            "OUT-003",
            OutlookSyntheticTestObjects.CreateOut003,
            (result, loaded) =>
            {
                Assert.Collection(
                    result.Diagnostics,
                    diagnostic =>
                    {
                        Assert.Equal("Legacy mixed rule", diagnostic.RuleName);
                        Assert.Equal(
                            "Unsupported Outlook condition 'olConditionMessageHeader' was not exported.",
                            diagnostic.Message);
                    },
                    diagnostic =>
                    {
                        Assert.Equal("Legacy mixed rule", diagnostic.RuleName);
                        Assert.Equal(
                            "Unsupported Outlook exception 'olConditionLocalMachineOnly' was not exported.",
                            diagnostic.Message);
                    },
                    diagnostic =>
                    {
                        Assert.Equal("Legacy mixed rule", diagnostic.RuleName);
                        Assert.Equal(
                            "Unsupported Outlook action 'olRuleActionDeletePermanently' was not exported.",
                            diagnostic.Message);
                    });
                Assert.Empty(loaded.Diagnostics);
                RuleDefinition rule = Assert.Single(loaded.Rules);
                Assert.Equal("Legacy mixed rule", rule.Name);
                Assert.Equal(0, rule.OriginalOrder);
                Assert.Null(rule.Id);
                Assert.Equal("INBOX/Projects", rule.TargetFolder);
                Assert.Equal(RuleConditionMode.All, rule.ConditionMode);
                RuleCondition condition = Assert.Single(rule.Conditions);
                Assert.Equal(RuleConditionType.SubjectContains, condition.Type);
                Assert.Equal(["project"], condition.Values);
                RuleCondition exception = Assert.Single(rule.Exceptions);
                Assert.Equal(RuleConditionType.BodyContains, exception.Type);
                Assert.Equal(["internal"], exception.Values);
                Assert.Collection(
                    rule.Actions,
                    action =>
                    {
                        Assert.Equal(RuleActionType.FileInto, action.Type);
                        Assert.Equal(["INBOX/Projects"], action.Values);
                    },
                    action =>
                    {
                        Assert.Equal(RuleActionType.Stop, action.Type);
                        Assert.Empty(action.Values);
                    });
                Assert.Equal(RuleOwnership.Managed, rule.Ownership);
                Assert.Empty(rule.RequiredCapabilities);
            });

    private static async Task AssertGoldenAsync(
        string scenarioId,
        Func<FakeOutlook> outlookFactory,
        Action<ExportRulesResult, RuleDocument> assertScenario)
    {
        string actualFile = Path.Combine(
            Path.GetTempPath(),
            $"OutlookResiever-{scenarioId}-{Guid.NewGuid():N}.rules.json");
        string fixtureFile = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "OutlookV1",
            $"{scenarioId}.rules.json");
        bool fixtureExists = File.Exists(fixtureFile);
        var serializer = new JsonRuleSerializer();
        var application = new OutlookExportApplication(
            new OutlookRuleExporter(
                new OutlookFolderNormalizer(),
                () => outlookFactory()),
            serializer);

        try
        {
            ExportRulesResult result = await application.ExportAsync(
                new ExportRulesRequest(actualFile),
                TestContext.Current.CancellationToken);
            byte[] actual = await File.ReadAllBytesAsync(
                actualFile,
                TestContext.Current.CancellationToken);

            if (!fixtureExists)
            {
                Assert.Fail($"Missing {scenarioId} golden. Inspect product output at {actualFile}.");
            }

            byte[] expected = await File.ReadAllBytesAsync(
                fixtureFile,
                TestContext.Current.CancellationToken);

            Assert.Equal(expected, actual);
            Assert.DoesNotContain((byte)0x0D, actual);
            Assert.False(actual.AsSpan().StartsWith("\uFEFF"u8));
            Assert.Equal((byte)0x0A, actual[^1]);
            Assert.NotEqual((byte)0x0A, actual[^2]);

            await using var stream = new MemoryStream(actual);
            RuleDocument loaded = await serializer.LoadDocumentAsync(
                stream,
                TestContext.Current.CancellationToken);

            Assert.Equal(RuleDocument.SchemaId, loaded.Schema);
            Assert.Equal(RuleDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal("outlook", loaded.SourceId);
            assertScenario(result, loaded);
        }
        finally
        {
            if (fixtureExists)
            {
                File.Delete(actualFile);
            }
        }
    }
}
