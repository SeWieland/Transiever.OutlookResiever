using Transiever.OutlookResiever.Application;
using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.UnitTest;

public sealed class OutlookExportApplicationTests
{
    [Fact]
    public async Task Export_WritesSieveRulerDocumentWithOutlookSource()
    {
        string file = Path.Combine(
            Path.GetTempPath(),
            $"OutlookResiever-{Guid.NewGuid():N}.json");
        try
        {
            var application = new OutlookExportApplication(
                new FakeExporter(),
                new JsonRuleSerializer());

            ExportRulesResult result = await application.ExportAsync(
                new ExportRulesRequest(file),
                TestContext.Current.CancellationToken);
            RuleDocument saved = await new JsonRuleSerializer().LoadDocumentAsync(
                file,
                TestContext.Current.CancellationToken);

            Assert.Equal("outlook", result.Document.SourceId);
            Assert.Equal("outlook", saved.SourceId);
            Assert.Equal("Invoices", Assert.Single(saved.Rules).Name);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private sealed class FakeExporter : IOutlookRuleExporter
    {
        public OutlookRuleExportResult Export() =>
            new()
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
