using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.UnitTest;

public sealed class OutlookRuleExporterTests
{
    [Fact]
    public void Export_MapsOut001()
    {
        OutlookRuleExportResult result = Export(OutlookSyntheticTestObjects.CreateOut001());

        RuleDefinition rule = Assert.Single(result.Rules);
        Assert.Empty(result.Diagnostics);
        Assert.Null(rule.Id);
        Assert.Equal("Project invoices", rule.Name);
        Assert.Equal(1, rule.OriginalOrder);
        Assert.Equal("INBOX/Projects", rule.TargetFolder);
        Assert.Collection(
            rule.Conditions,
            condition => Assert.Equal(RuleConditionType.SubjectContains, condition.Type),
            condition => Assert.Equal(RuleConditionType.SenderContains, condition.Type),
            condition => Assert.Equal(RuleConditionType.ReceiverContains, condition.Type),
            condition => Assert.Equal(RuleConditionType.HasAttachment, condition.Type));
        Assert.Equal(RuleConditionType.BodyContains, Assert.Single(rule.Exceptions).Type);
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
            action => Assert.Equal(RuleActionType.Stop, action.Type));
    }

    [Fact]
    public void Export_ExcludesUnsupportedOnlyCondition()
    {
        var rule = new FakeRule
        {
            Name = "Unsupported condition",
            Conditions = FakeConditions.Create(extra: [new FakeCondition(true, 15)]),
            Actions = FakeActions.Create(moveFolder: @"\\Mailbox\Inbox\Review")
        };

        OutlookRuleExportResult result = Export(rule);

        Assert.Empty(result.Rules);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.RuleName == rule.Name &&
            diagnostic.Message.Contains("condition", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("olConditionMessageHeader", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_ExcludesUnsupportedOnlyAction()
    {
        var rule = new FakeRule
        {
            Name = "Unsupported action",
            Conditions = FakeConditions.Create(subject: ["review"]),
            Actions = FakeActions.Create(extra: [new FakeAction(true, 4)])
        };

        OutlookRuleExportResult result = Export(rule);

        Assert.Empty(result.Rules);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.RuleName == rule.Name &&
            diagnostic.Message.Contains("action", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("olRuleActionDeletePermanently", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_ExcludesSendRule()
    {
        var rule = new FakeRule
        {
            Name = "Send",
            RuleType = 1
        };

        OutlookRuleExportResult result = Export(rule);

        Assert.Empty(result.Rules);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.RuleName == rule.Name &&
            diagnostic.Message.Contains("Send rules", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_ExcludesClientOnlyRule()
    {
        var rule = new FakeRule
        {
            Name = "Client only",
            Conditions = FakeConditions.Create(extra: [new FakeCondition(true, 27)]),
            Actions = FakeActions.Create(extra: [new FakeAction(true, 20)])
        };

        OutlookRuleExportResult result = Export(rule);

        Assert.Empty(result.Rules);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.RuleName == rule.Name &&
            diagnostic.Message.Contains("condition", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("olConditionLocalMachineOnly", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.RuleName == rule.Name &&
            diagnostic.Message.Contains("action", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("olRuleActionRunScript", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_MapsOut003SupportedSubsetAndDiagnostics()
    {
        OutlookRuleExportResult result = Export(OutlookSyntheticTestObjects.CreateOut003());

        RuleDefinition rule = Assert.Single(result.Rules);
        Assert.Null(rule.Id);
        Assert.Contains(rule.Conditions, condition =>
            condition.Type == RuleConditionType.SubjectContains &&
            condition.Values.SequenceEqual(["project"]));
        Assert.Contains(rule.Exceptions, condition =>
            condition.Type == RuleConditionType.BodyContains &&
            condition.Values.SequenceEqual(["internal"]));
        Assert.Contains(rule.Actions, action =>
            action.Type == RuleActionType.FileInto &&
            action.Values.SequenceEqual(["INBOX/Projects"]));
        Assert.Contains(rule.Actions, action => action.Type == RuleActionType.Stop);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message == "Unsupported Outlook condition 'olConditionMessageHeader' was not exported.");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message == "Unsupported Outlook exception 'olConditionLocalMachineOnly' was not exported.");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message == "Unsupported Outlook action 'olRuleActionDeletePermanently' was not exported.");
    }

    private static OutlookRuleExportResult Export(params FakeRule[] rules) =>
        Export(new FakeOutlook(rules));

    private static OutlookRuleExportResult Export(FakeOutlook outlook) =>
        new OutlookRuleExporter(new OutlookFolderNormalizer(), () => outlook).Export();
}
