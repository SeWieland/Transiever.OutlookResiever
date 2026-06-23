
using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.Services;

public sealed class OutlookRuleExporter : IOutlookRuleExporter
{
    private const int OutlookDeletedItemsFolder = 3;
    private const int OutlookSentMailFolder = 5;
    private const int OutlookInboxFolder = 6;
    private const int OutlookDraftsFolder = 16;
    private const int OutlookJunkFolder = 23;

    private readonly IFolderNormalizer folderNormalizer;

    public OutlookRuleExporter(IFolderNormalizer folderNormalizer)
    {
        this.folderNormalizer = folderNormalizer;
    }

    public OutlookRuleExportResult Export()
    {
        var result = new List<RuleDefinition>();
        var diagnostics = new List<OutlookRuleExportDiagnostic>();

        object? outlook = null;
        object? session = null;
        object? store = null;
        object? rules = null;

        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application")
                ?? throw new InvalidOperationException("Outlook.Application COM ProgID was not found. Is classic Outlook installed?");

            outlook = Activator.CreateInstance(outlookType)
                ?? throw new InvalidOperationException("Could not create Outlook.Application.");

            dynamic app = outlook;

            session = app.Session;
            dynamic dynSession = session;

            store = dynSession.DefaultStore;
            dynamic dynStore = store;
            IReadOnlyDictionary<OutlookDefaultFolderRole, string> defaultFolderPaths =
                ReadDefaultFolderPaths(dynStore);

            rules = dynStore.GetRules();

            foreach (var ruleObj in OutlookCom.AsEnumerable(rules))
            {
                object? rule = ruleObj;

                try
                {
                    dynamic dynRule = rule!;

                    string ruleName = OutlookCom.SafeString(() => dynRule.Name);

                    if (!OutlookCom.SafeBool(() => dynRule.Enabled))
                        continue;

                    object? actions = dynRule.Actions;
                    dynamic dynActions = actions!;

                    object? moveToFolderAction = dynActions.MoveToFolder;
                    dynamic dynMove = moveToFolderAction!;

                    if (!OutlookCom.SafeBool(() => dynMove.Enabled))
                        continue;

                    object? folder = dynMove.Folder;

                    if (folder is null)
                        continue;

                    dynamic dynFolder = folder;

                    var definition = new RuleDefinition
                    {
                        Name = ruleName,
                        ConditionMode = RuleConditionMode.All,
                        TargetFolder = folderNormalizer.Normalize(
                            new OutlookFolderPathContext
                            {
                                FolderPath = OutlookCom.SafeString(
                                    () => dynFolder.FolderPath),
                                DefaultFolderPaths = defaultFolderPaths
                            })
                    };

                    ReadSubjectConditions(dynRule, definition);
                    ReadSenderConditions(dynRule, definition);
                    ReadReceiverConditions(dynRule, definition);
                    ReadContentConditions(dynRule, definition);

                    if (definition.Conditions.Count > 0)
                        result.Add(definition);
                }
                catch (Exception ex)
                {
                    var name = OutlookCom.TryGetRuleName(rule);
                    diagnostics.Add(new OutlookRuleExportDiagnostic(name, ex.Message));
                }
                finally
                {
                    OutlookCom.Release(rule);
                }
            }
        }
        finally
        {
            OutlookCom.Release(rules);
            OutlookCom.Release(store);
            OutlookCom.Release(session);
            OutlookCom.Release(outlook);
        }

        return new OutlookRuleExportResult
        {
            Rules = result,
            Diagnostics = diagnostics
        };
    }

    private static IReadOnlyDictionary<OutlookDefaultFolderRole, string>
        ReadDefaultFolderPaths(dynamic store)
    {
        var paths = new Dictionary<OutlookDefaultFolderRole, string>();
        foreach ((OutlookDefaultFolderRole role, int folderType) in
            new (OutlookDefaultFolderRole Role, int FolderType)[]
            {
                (OutlookDefaultFolderRole.Inbox, OutlookInboxFolder),
                (OutlookDefaultFolderRole.Drafts, OutlookDraftsFolder),
                (OutlookDefaultFolderRole.Sent, OutlookSentMailFolder),
                (OutlookDefaultFolderRole.Trash, OutlookDeletedItemsFolder),
                (OutlookDefaultFolderRole.Junk, OutlookJunkFolder)
            })
        {
            object? folder = null;
            try
            {
                folder = store.GetDefaultFolder(folderType);
                dynamic dynFolder = folder!;
                string folderPath = OutlookCom.SafeString(() => dynFolder.FolderPath);
                if (!string.IsNullOrWhiteSpace(folderPath))
                    paths[role] = folderPath;
            }
            catch
            {
                // Some stores do not expose every default folder role.
            }
            finally
            {
                OutlookCom.Release(folder);
            }
        }

        return paths;
    }

    private static void ReadSubjectConditions(
        dynamic rule,
        RuleDefinition definition)
    {
        ReadTextCondition(
            () => rule.Conditions.Subject,
            RuleConditionType.SubjectContains,
            definition);
    }

    private static void ReadSenderConditions(
        dynamic rule,
        RuleDefinition definition)
    {
        ReadRecipientCondition(
            () => rule.Conditions.From,
            RuleConditionType.SenderContains,
            definition);
    }

    private static void ReadReceiverConditions(
        dynamic rule,
        RuleDefinition definition)
    {
        ReadRecipientCondition(
            () => rule.Conditions.SentTo,
            RuleConditionType.ReceiverContains,
            definition);

        ReadTextCondition(
            () => rule.Conditions.RecipientAddress,
            RuleConditionType.ReceiverContains,
            definition);
    }

    private static void ReadContentConditions(
        dynamic rule,
        RuleDefinition definition)
    {
        ReadTextCondition(
            () => rule.Conditions.Body,
            RuleConditionType.BodyContains,
            definition);

        ReadTextCondition(
            () => rule.Conditions.BodyOrSubject,
            RuleConditionType.SubjectOrBodyContains,
            definition);
    }

    private static void ReadTextCondition(
        Func<object?> conditionGetter,
        RuleConditionType type,
        RuleDefinition definition)
    {
        object? condition = null;

        try
        {
            condition = conditionGetter();
            dynamic textCondition = condition!;

            if (!OutlookCom.SafeBool(() => textCondition.Enabled))
                return;

            var values = ReadTextValues(textCondition.Text);

            AddCondition(definition, type, values);
        }
        catch
        {
            // Unsupported or unavailable condition shape.
        }
        finally
        {
            OutlookCom.Release(condition);
        }
    }

    private static void ReadRecipientCondition(
        Func<object?> conditionGetter,
        RuleConditionType type,
        RuleDefinition definition)
    {
        object? condition = null;
        object? recipients = null;

        try
        {
            condition = conditionGetter();
            dynamic recipientCondition = condition!;

            if (!OutlookCom.SafeBool(() => recipientCondition.Enabled))
                return;

            recipients = recipientCondition.Recipients;

            var values = OutlookCom.AsEnumerable(recipients)
                .Select(ReadRecipientValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            AddCondition(definition, type, values);
        }
        catch
        {
            // Unsupported or unavailable condition shape.
        }
        finally
        {
            OutlookCom.Release(recipients);
            OutlookCom.Release(condition);
        }
    }

    private static IReadOnlyCollection<string> ReadTextValues(object? textValues)
    {
        return OutlookCom.AsEnumerable(textValues)
            .Select(value => value?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ReadRecipientValue(object? recipient)
    {
        try
        {
            dynamic dynRecipient = recipient!;

            return FirstNonEmpty(
                OutlookCom.SafeString(() => dynRecipient.SmtpAddress),
                OutlookCom.SafeString(() => dynRecipient.Address),
                OutlookCom.SafeString(() => dynRecipient.Name)).Trim();
        }
        finally
        {
            OutlookCom.Release(recipient);
        }
    }

    private static void AddCondition(
        RuleDefinition definition,
        RuleConditionType type,
        IEnumerable<string> values)
    {
        var cleanValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanValues.Count == 0)
            return;

        definition.Conditions.Add(new RuleCondition
        {
            Type = type,
            Values = cleanValues
        });
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
    }

}
