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
            });

    [Fact]
    public Task Export_OUT003_MatchesCanonicalGolden() =>
        AssertGoldenAsync(
            "OUT-003",
            OutlookSyntheticTestObjects.CreateOut003,
            (result, loaded) =>
            {
                Assert.Equal(3, result.Diagnostics.Count);
                Assert.Empty(loaded.Diagnostics);
                RuleDefinition rule = Assert.Single(loaded.Rules);
                Assert.Equal("Legacy mixed rule", rule.Name);
                Assert.Equal(0, rule.OriginalOrder);
                Assert.Null(rule.Id);
            });

    private static async Task AssertGoldenAsync(
        string scenarioId,
        Func<FakeOutlook> outlookFactory,
        Action<ExportRulesResult, RuleDocument> assertScenario)
    {
        string actualFile = Path.Combine(
            Path.GetTempPath(),
            $"OutlookResiever-{scenarioId}-{Guid.NewGuid():N}.rules.json");
        var serializer = new JsonRuleSerializer();
        var application = new OutlookExportApplication(
            new OutlookRuleExporter(
                new OutlookFolderNormalizer(),
                () => outlookFactory()),
            serializer);

        ExportRulesResult result = await application.ExportAsync(
            new ExportRulesRequest(actualFile),
            TestContext.Current.CancellationToken);
        byte[] actual = await File.ReadAllBytesAsync(
            actualFile,
            TestContext.Current.CancellationToken);
        string fixtureFile = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "OutlookV1",
            $"{scenarioId}.rules.json");

        if (!File.Exists(fixtureFile))
        {
            Assert.Fail($"Missing {scenarioId} golden. Inspect product output at {actualFile}.");
        }

        try
        {
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
            File.Delete(actualFile);
        }
    }
}
