using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.UnitTest;

public sealed class OutlookRuleExporterTests
{
    [Fact]
    public void Export_MapsSupportedReceiveRuleShapes()
    {
        var rule = new FakeRule
        {
            Name = "Project invoices",
            Conditions = FakeConditions.Create(
                subject: ["invoice"],
                senderAddress: ["billing@example.com"],
                recipientAddress: ["team@example.com"],
                hasAttachment: true),
            Exceptions = FakeConditions.Create(body: ["internal"]),
            Actions = FakeActions.Create(
                moveFolder: @"\\Mailbox\Inbox\Projects",
                copyFolder: @"\\Mailbox\Archive\Projects",
                redirectRecipients: [new FakeRecipient("archive@example.com")],
                markRead: true,
                stop: true)
        };
        var exporter = new OutlookRuleExporter(
            new OutlookFolderNormalizer(),
            () => new FakeOutlook([rule]));

        OutlookRuleExportResult result = exporter.Export();

        RuleDefinition exported = Assert.Single(result.Rules);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("Project invoices", exported.Name);
        Assert.Equal("INBOX/Projects", exported.TargetFolder);
        Assert.Collection(
            exported.Conditions,
            condition => Assert.Equal(RuleConditionType.SubjectContains, condition.Type),
            condition => Assert.Equal(RuleConditionType.SenderContains, condition.Type),
            condition => Assert.Equal(RuleConditionType.ReceiverContains, condition.Type),
            condition => Assert.Equal(RuleConditionType.HasAttachment, condition.Type));
        Assert.Equal(RuleConditionType.BodyContains, Assert.Single(exported.Exceptions).Type);
        Assert.Collection(
            exported.Actions,
            action =>
            {
                Assert.Equal(RuleActionType.SetFlags, action.Type);
                Assert.Equal(["\\Seen"], action.GetValues());
            },
            action =>
            {
                Assert.Equal(RuleActionType.FileInto, action.Type);
                Assert.Equal(["INBOX/Projects"], action.GetValues());
            },
            action =>
            {
                Assert.Equal(RuleActionType.CopyInto, action.Type);
                Assert.Equal(["Archive/Projects"], action.GetValues());
            },
            action =>
            {
                Assert.Equal(RuleActionType.Redirect, action.Type);
                Assert.Equal(["archive@example.com"], action.GetValues());
            },
            action => Assert.Equal(RuleActionType.Stop, action.Type));
    }

    [Fact]
    public void Export_ReportsUnsupportedEnabledShapes()
    {
        var rule = new FakeRule
        {
            Name = "Unsupported",
            Conditions = FakeConditions.Create(
                extra: [new FakeCondition(true, 15)]),
            Actions = FakeActions.Create(
                extra: [new FakeAction(true, 4)])
        };
        var sendRule = new FakeRule
        {
            Name = "Send",
            RuleType = 1
        };
        var exporter = new OutlookRuleExporter(
            new OutlookFolderNormalizer(),
            () => new FakeOutlook([rule, sendRule]));

        OutlookRuleExportResult result = exporter.Export();

        Assert.Empty(result.Rules);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.RuleName == "Unsupported" &&
                diagnostic.Message.Contains("MessageHeader", StringComparison.Ordinal));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.RuleName == "Unsupported" &&
                diagnostic.Message.Contains("DeletePermanently", StringComparison.Ordinal));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.RuleName == "Send" &&
                diagnostic.Message.Contains("Send rules", StringComparison.Ordinal));
    }

    public sealed class FakeOutlook(IReadOnlyList<FakeRule> rules)
    {
        public FakeSession Session { get; } = new(rules);
    }

    public sealed class FakeSession(IReadOnlyList<FakeRule> rules)
    {
        public FakeStore DefaultStore { get; } = new(rules);
    }

    public sealed class FakeStore(IReadOnlyList<FakeRule> rules)
    {
        public FakeCollection GetRules() => new([.. rules]);

        public FakeFolder GetDefaultFolder(int folderType) =>
            folderType switch
            {
                3 => new FakeFolder(@"\\Mailbox\Deleted Items"),
                5 => new FakeFolder(@"\\Mailbox\Sent Items"),
                6 => new FakeFolder(@"\\Mailbox\Inbox"),
                16 => new FakeFolder(@"\\Mailbox\Drafts"),
                23 => new FakeFolder(@"\\Mailbox\Junk Email"),
                _ => new FakeFolder("")
            };
    }

    public sealed class FakeRule
    {
        public string Name { get; init; } = "";

        public bool Enabled { get; init; } = true;

        public int RuleType { get; init; }

        public FakeConditions Conditions { get; init; } = FakeConditions.Create();

        public FakeConditions Exceptions { get; init; } = FakeConditions.Create();

        public FakeActions Actions { get; init; } = FakeActions.Create();
    }

    public sealed class FakeConditions
    {
        private readonly object[] items;

        private FakeConditions(
            FakeCondition subject,
            FakeCondition body,
            FakeCondition bodyOrSubject,
            FakeCondition from,
            FakeCondition senderAddress,
            FakeCondition sentTo,
            FakeCondition recipientAddress,
            FakeCondition hasAttachment,
            object[] extra)
        {
            Subject = subject;
            Body = body;
            BodyOrSubject = bodyOrSubject;
            From = from;
            SenderAddress = senderAddress;
            SentTo = sentTo;
            RecipientAddress = recipientAddress;
            HasAttachment = hasAttachment;
            items =
            [
                subject,
                body,
                bodyOrSubject,
                from,
                senderAddress,
                sentTo,
                recipientAddress,
                hasAttachment,
                .. extra
            ];
        }

        public FakeCondition Subject { get; }

        public FakeCondition Body { get; }

        public FakeCondition BodyOrSubject { get; }

        public FakeCondition From { get; }

        public FakeCondition SenderAddress { get; }

        public FakeCondition SentTo { get; }

        public FakeCondition RecipientAddress { get; }

        public FakeCondition HasAttachment { get; }

        public int Count => items.Length;

        public object Item(int index) => items[index - 1];

        public static FakeConditions Create(
            string[]? subject = null,
            string[]? body = null,
            string[]? bodyOrSubject = null,
            FakeRecipient[]? from = null,
            string[]? senderAddress = null,
            FakeRecipient[]? sentTo = null,
            string[]? recipientAddress = null,
            bool hasAttachment = false,
            object[]? extra = null) =>
            new(
                new FakeCondition(subject is not null, 2, text: subject),
                new FakeCondition(body is not null, 13, text: body),
                new FakeCondition(bodyOrSubject is not null, 14, text: bodyOrSubject),
                new FakeCondition(from is not null, 1, recipients: from),
                new FakeCondition(senderAddress is not null, 17, address: senderAddress),
                new FakeCondition(sentTo is not null, 12, recipients: sentTo),
                new FakeCondition(recipientAddress is not null, 16, address: recipientAddress),
                new FakeCondition(hasAttachment, 20),
                extra ?? []);
    }

    public sealed class FakeActions
    {
        private readonly object[] items;

        private FakeActions(
            FakeAction moveToFolder,
            FakeAction copyToFolder,
            FakeAction delete,
            FakeAction redirect,
            FakeAction stop,
            object[] extra)
        {
            MoveToFolder = moveToFolder;
            CopyToFolder = copyToFolder;
            Delete = delete;
            Redirect = redirect;
            Stop = stop;
            items = [moveToFolder, copyToFolder, delete, redirect, stop, .. extra];
        }

        public FakeAction MoveToFolder { get; }

        public FakeAction CopyToFolder { get; }

        public FakeAction Delete { get; }

        public FakeAction Redirect { get; }

        public FakeAction Stop { get; }

        public int Count => items.Length;

        public object Item(int index) => items[index - 1];

        public static FakeActions Create(
            string? moveFolder = null,
            string? copyFolder = null,
            bool delete = false,
            FakeRecipient[]? redirectRecipients = null,
            bool markRead = false,
            bool stop = false,
            object[]? extra = null)
        {
            object[] extras = markRead
                ? [new FakeAction(true, 19), .. (extra ?? [])]
                : extra ?? [];
            return new FakeActions(
                new FakeAction(moveFolder is not null, 1, folder: moveFolder),
                new FakeAction(copyFolder is not null, 5, folder: copyFolder),
                new FakeAction(delete, 3),
                new FakeAction(redirectRecipients is not null, 8, recipients: redirectRecipients),
                new FakeAction(stop, 21),
                extras);
        }
    }

    public sealed class FakeCondition(
        bool enabled,
        int conditionType,
        string[]? text = null,
        string[]? address = null,
        FakeRecipient[]? recipients = null)
    {
        public bool Enabled { get; } = enabled;

        public int ConditionType { get; } = conditionType;

        public string[] Text { get; } = text ?? [];

        public string[] Address { get; } = address ?? [];

        public FakeCollection Recipients { get; } = new(recipients ?? []);
    }

    public sealed class FakeAction(
        bool enabled,
        int actionType,
        string? folder = null,
        FakeRecipient[]? recipients = null)
    {
        public bool Enabled { get; } = enabled;

        public int ActionType { get; } = actionType;

        public FakeFolder? Folder { get; } = folder is null ? null : new FakeFolder(folder);

        public FakeCollection Recipients { get; } = new(recipients ?? []);
    }

    public sealed class FakeRecipient(string address)
    {
        public string SmtpAddress { get; } = address;

        public string Address { get; } = address;

        public string Name { get; } = address;
    }

    public sealed class FakeFolder(string path)
    {
        public string FolderPath { get; } = path;
    }

    public sealed class FakeCollection(IReadOnlyList<object?> items)
    {
        public int Count => items.Count;

        public object? Item(int index) => items[index - 1];
    }
}
