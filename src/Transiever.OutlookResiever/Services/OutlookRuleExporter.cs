using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.Services;

/// <summary>
/// Default Outlook exporter that reads supported rules from classic Outlook COM.
/// </summary>
public sealed class OutlookRuleExporter : IOutlookRuleExporter
{
    private const int OutlookDeletedItemsFolder = 3;
    private const int OutlookSentMailFolder = 5;
    private const int OutlookInboxFolder = 6;
    private const int OutlookDraftsFolder = 16;
    private const int OutlookJunkFolder = 23;
    private const int OutlookReceiveRule = 0;
    private const int OutlookRuleActionMarkRead = 19;

    private static readonly HashSet<int> SupportedConditionTypes =
    [
        1,  // olConditionFrom
        2,  // olConditionSubject
        12, // olConditionSentTo
        13, // olConditionBody
        14, // olConditionBodyOrSubject
        16, // olConditionRecipientAddress
        17, // olConditionSenderAddress
        20  // olConditionHasAttachment
    ];

    private static readonly HashSet<int> SupportedActionTypes =
    [
        1,  // olRuleActionMoveToFolder
        3,  // olRuleActionDelete
        5,  // olRuleActionCopyToFolder
        8,  // olRuleActionRedirect
        OutlookRuleActionMarkRead,
        21  // olRuleActionStop
    ];

    private static readonly IReadOnlyDictionary<int, string> ConditionNames =
        new Dictionary<int, string>
        {
            [3] = "olConditionAccount",
            [4] = "olConditionOnlyToMe",
            [5] = "olConditionTo",
            [6] = "olConditionImportance",
            [7] = "olConditionSensitivity",
            [8] = "olConditionFlaggedForAction",
            [9] = "olConditionCc",
            [10] = "olConditionToOrCc",
            [11] = "olConditionNotTo",
            [15] = "olConditionMessageHeader",
            [18] = "olConditionCategory",
            [19] = "olConditionOOF",
            [21] = "olConditionSizeRange",
            [22] = "olConditionDateRange",
            [23] = "olConditionFormName",
            [24] = "olConditionProperty",
            [25] = "olConditionSenderInAddressBook",
            [26] = "olConditionMeetingInviteOrUpdate",
            [27] = "olConditionLocalMachineOnly",
            [28] = "olConditionOtherMachine",
            [29] = "olConditionAnyCategory",
            [30] = "olConditionFromRssFeed",
            [31] = "olConditionFromAnyRSSFeed"
        };

    private static readonly IReadOnlyDictionary<int, string> ActionNames =
        new Dictionary<int, string>
        {
            [2] = "olRuleActionAssignToCategory",
            [4] = "olRuleActionDeletePermanently",
            [6] = "olRuleActionForward",
            [7] = "olRuleActionForwardAsAttachment",
            [9] = "olRuleActionServerReply",
            [10] = "olRuleActionTemplate",
            [11] = "olRuleActionFlagForActionInDays",
            [12] = "olRuleActionFlagColor",
            [13] = "olRuleActionFlagClear",
            [14] = "olRuleActionImportance",
            [15] = "olRuleActionSensitivity",
            [16] = "olRuleActionPrint",
            [17] = "olRuleActionPlaySound",
            [18] = "olRuleActionStartApplication",
            [20] = "olRuleActionRunScript",
            [22] = "olRuleActionCustomAction",
            [23] = "olRuleActionNewItemAlert",
            [24] = "olRuleActionDesktopAlert",
            [25] = "olRuleActionNotifyRead",
            [26] = "olRuleActionNotifyDelivery",
            [27] = "olRuleActionCcMessage",
            [28] = "olRuleActionDefer",
            [30] = "olRuleActionClearCategories",
            [41] = "olRuleActionMarkAsTask"
        };

    private readonly IFolderNormalizer folderNormalizer;
    private readonly Func<object?> outlookFactory;

    public OutlookRuleExporter(IFolderNormalizer folderNormalizer)
        : this(folderNormalizer, CreateOutlookApplication)
    {
    }

    public OutlookRuleExporter(
        IFolderNormalizer folderNormalizer,
        Func<object?> outlookFactory)
    {
        this.folderNormalizer = folderNormalizer;
        this.outlookFactory = outlookFactory;
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
            outlook = outlookFactory()
                ?? throw new InvalidOperationException("Could not create Outlook.Application.");
            dynamic app = outlook;

            session = app.Session;
            dynamic dynSession = session;

            store = dynSession.DefaultStore;
            dynamic dynStore = store;
            IReadOnlyDictionary<OutlookDefaultFolderRole, string> defaultFolderPaths =
                ReadDefaultFolderPaths(dynStore);

            rules = dynStore.GetRules();
            int order = 0;

            foreach (var ruleObj in OutlookCom.AsEnumerable(rules))
            {
                object? rule = ruleObj;

                try
                {
                    dynamic dynRule = rule!;

                    string ruleName = OutlookCom.SafeString(() => dynRule.Name);

                    if (!OutlookCom.SafeBool(() => dynRule.Enabled))
                        continue;

                    if (OutlookCom.SafeInt(() => dynRule.RuleType, OutlookReceiveRule) !=
                        OutlookReceiveRule)
                    {
                        diagnostics.Add(Diagnostic(ruleName, "Send rules are not exported."));
                        continue;
                    }

                    var definition = new RuleDefinition
                    {
                        Name = ruleName,
                        ConditionMode = RuleConditionMode.All,
                        OriginalOrder = order
                    };

                    ReadConditions(
                        () => dynRule.Conditions,
                        definition.Conditions,
                        diagnostics,
                        ruleName,
                        "condition");
                    ReadConditions(
                        () => dynRule.Exceptions,
                        definition.Exceptions,
                        diagnostics,
                        ruleName,
                        "exception");
                    ReadActions(
                        () => dynRule.Actions,
                        definition,
                        defaultFolderPaths,
                        diagnostics,
                        ruleName);

                    if (definition.Conditions.Count == 0)
                    {
                        diagnostics.Add(
                            Diagnostic(
                                ruleName,
                                "No supported receive-rule conditions were exported."));
                    }

                    if (definition.Actions.Count == 0)
                    {
                        diagnostics.Add(
                            Diagnostic(
                                ruleName,
                                "No supported server-side actions were exported."));
                    }

                    if (definition.Conditions.Count > 0 &&
                        definition.Actions.Count > 0)
                    {
                        result.Add(definition);
                    }
                }
                catch (Exception ex)
                {
                    var name = OutlookCom.TryGetRuleName(rule);
                    diagnostics.Add(new OutlookRuleExportDiagnostic(name, ex.Message));
                }
                finally
                {
                    OutlookCom.Release(rule);
                    order++;
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

    private static object CreateOutlookApplication()
    {
        var outlookType = Type.GetTypeFromProgID("Outlook.Application")
            ?? throw new InvalidOperationException("Outlook.Application COM ProgID was not found. Is classic Outlook installed?");

        return Activator.CreateInstance(outlookType)
            ?? throw new InvalidOperationException("Could not create Outlook.Application.");
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

    private static void ReadConditions(
        Func<object?> conditionCollectionGetter,
        List<RuleCondition> target,
        List<OutlookRuleExportDiagnostic> diagnostics,
        string ruleName,
        string diagnosticKind)
    {
        object? conditions = null;
        try
        {
            conditions = conditionCollectionGetter();
            dynamic dynConditions = conditions!;

            ReadTextCondition(
                () => dynConditions.Subject,
                RuleConditionType.SubjectContains,
                target);
            ReadTextCondition(
                () => dynConditions.Body,
                RuleConditionType.BodyContains,
                target);
            ReadTextCondition(
                () => dynConditions.BodyOrSubject,
                RuleConditionType.SubjectOrBodyContains,
                target);
            ReadRecipientCondition(
                () => dynConditions.From,
                RuleConditionType.SenderContains,
                target);
            ReadAddressCondition(
                () => dynConditions.SenderAddress,
                RuleConditionType.SenderContains,
                target);
            ReadRecipientCondition(
                () => dynConditions.SentTo,
                RuleConditionType.ReceiverContains,
                target);
            ReadAddressCondition(
                () => dynConditions.RecipientAddress,
                RuleConditionType.ReceiverContains,
                target);
            ReadFlagCondition(
                () => dynConditions.HasAttachment,
                RuleConditionType.HasAttachment,
                target);

            AddUnsupportedConditionDiagnostics(
                conditions,
                diagnostics,
                ruleName,
                diagnosticKind);
        }
        catch
        {
            // Unsupported or unavailable condition collection shape.
        }
        finally
        {
            OutlookCom.Release(conditions);
        }
    }

    private void ReadActions(
        Func<object?> actionCollectionGetter,
        RuleDefinition definition,
        IReadOnlyDictionary<OutlookDefaultFolderRole, string> defaultFolderPaths,
        List<OutlookRuleExportDiagnostic> diagnostics,
        string ruleName)
    {
        object? actions = null;
        try
        {
            actions = actionCollectionGetter();
            dynamic dynActions = actions!;

            if (ReadEnabledActionTypes(actions).Contains(OutlookRuleActionMarkRead))
            {
                AddAction(
                    definition,
                    RuleActionType.SetFlags,
                    ["\\Seen"]);
            }

            ReadFolderAction(
                () => dynActions.MoveToFolder,
                RuleActionType.FileInto,
                definition,
                defaultFolderPaths);
            ReadFolderAction(
                () => dynActions.CopyToFolder,
                RuleActionType.CopyInto,
                definition,
                defaultFolderPaths);
            ReadDeleteAction(
                () => dynActions.Delete,
                definition,
                defaultFolderPaths);
            ReadRedirectAction(
                () => dynActions.Redirect,
                definition);
            ReadStopAction(
                () => dynActions.Stop,
                definition);

            AddUnsupportedActionDiagnostics(actions, diagnostics, ruleName);
        }
        catch
        {
            // Unsupported or unavailable action collection shape.
        }
        finally
        {
            OutlookCom.Release(actions);
        }
    }

    private static void ReadTextCondition(
        Func<object?> conditionGetter,
        RuleConditionType type,
        List<RuleCondition> target)
    {
        object? condition = null;

        try
        {
            condition = conditionGetter();
            dynamic textCondition = condition!;

            if (!OutlookCom.SafeBool(() => textCondition.Enabled))
                return;

            AddCondition(target, type, ReadTextValues(textCondition.Text));
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

    private static void ReadAddressCondition(
        Func<object?> conditionGetter,
        RuleConditionType type,
        List<RuleCondition> target)
    {
        object? condition = null;

        try
        {
            condition = conditionGetter();
            dynamic addressCondition = condition!;

            if (!OutlookCom.SafeBool(() => addressCondition.Enabled))
                return;

            AddCondition(target, type, ReadTextValues(addressCondition.Address));
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
        List<RuleCondition> target)
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

            AddCondition(target, type, values);
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

    private static void ReadFlagCondition(
        Func<object?> conditionGetter,
        RuleConditionType type,
        List<RuleCondition> target)
    {
        object? condition = null;

        try
        {
            condition = conditionGetter();
            dynamic dynCondition = condition!;

            if (!OutlookCom.SafeBool(() => dynCondition.Enabled))
                return;

            target.Add(new RuleCondition { Type = type });
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

    private void ReadFolderAction(
        Func<object?> actionGetter,
        RuleActionType type,
        RuleDefinition definition,
        IReadOnlyDictionary<OutlookDefaultFolderRole, string> defaultFolderPaths)
    {
        object? action = null;
        object? folder = null;

        try
        {
            action = actionGetter();
            dynamic dynAction = action!;

            if (!OutlookCom.SafeBool(() => dynAction.Enabled))
                return;

            folder = dynAction.Folder;
            if (folder is null)
                return;

            dynamic dynFolder = folder;
            string targetFolder = NormalizeFolder(
                OutlookCom.SafeString(() => dynFolder.FolderPath),
                defaultFolderPaths);
            if (string.IsNullOrWhiteSpace(targetFolder))
                return;

            AddAction(definition, type, [targetFolder]);
            if (type == RuleActionType.FileInto &&
                string.IsNullOrWhiteSpace(definition.TargetFolder))
            {
                definition.TargetFolder = targetFolder;
            }
        }
        catch
        {
            // Unsupported or unavailable action shape.
        }
        finally
        {
            OutlookCom.Release(folder);
            OutlookCom.Release(action);
        }
    }

    private void ReadDeleteAction(
        Func<object?> actionGetter,
        RuleDefinition definition,
        IReadOnlyDictionary<OutlookDefaultFolderRole, string> defaultFolderPaths)
    {
        object? action = null;

        try
        {
            action = actionGetter();
            dynamic dynAction = action!;

            if (!OutlookCom.SafeBool(() => dynAction.Enabled))
                return;

            string targetFolder = defaultFolderPaths.TryGetValue(
                OutlookDefaultFolderRole.Trash,
                out string? trashPath)
                ? NormalizeFolder(trashPath, defaultFolderPaths)
                : folderNormalizer.Normalize("Trash");
            if (string.IsNullOrWhiteSpace(targetFolder))
                return;

            AddAction(definition, RuleActionType.FileInto, [targetFolder]);
            if (string.IsNullOrWhiteSpace(definition.TargetFolder))
                definition.TargetFolder = targetFolder;
        }
        catch
        {
            // Unsupported or unavailable action shape.
        }
        finally
        {
            OutlookCom.Release(action);
        }
    }

    private static void ReadRedirectAction(
        Func<object?> actionGetter,
        RuleDefinition definition)
    {
        object? action = null;
        object? recipients = null;

        try
        {
            action = actionGetter();
            dynamic dynAction = action!;

            if (!OutlookCom.SafeBool(() => dynAction.Enabled))
                return;

            recipients = dynAction.Recipients;
            string[] values = OutlookCom.AsEnumerable(recipients)
                .Select(ReadRecipientValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            AddAction(definition, RuleActionType.Redirect, values);
        }
        catch
        {
            // Unsupported or unavailable action shape.
        }
        finally
        {
            OutlookCom.Release(recipients);
            OutlookCom.Release(action);
        }
    }

    private static void ReadStopAction(
        Func<object?> actionGetter,
        RuleDefinition definition)
    {
        object? action = null;

        try
        {
            action = actionGetter();
            dynamic dynAction = action!;

            if (OutlookCom.SafeBool(() => dynAction.Enabled))
                AddAction(definition, RuleActionType.Stop, []);
        }
        catch
        {
            // Unsupported or unavailable action shape.
        }
        finally
        {
            OutlookCom.Release(action);
        }
    }

    private static IReadOnlyCollection<int> ReadEnabledActionTypes(object? actions) =>
        OutlookCom.AsEnumerable(actions)
            .Select(ReadEnabledActionType)
            .Where(type => type >= 0)
            .ToHashSet();

    private static int ReadEnabledActionType(object? action)
    {
        try
        {
            dynamic dynAction = action!;
            if (!OutlookCom.SafeBool(() => dynAction.Enabled))
                return -1;

            return OutlookCom.SafeInt(() => dynAction.ActionType, -1);
        }
        catch
        {
            return -1;
        }
    }

    private static void AddUnsupportedConditionDiagnostics(
        object? conditions,
        List<OutlookRuleExportDiagnostic> diagnostics,
        string ruleName,
        string diagnosticKind)
    {
        foreach (int conditionType in OutlookCom.AsEnumerable(conditions)
            .Select(ReadEnabledConditionType)
            .Where(type => type >= 0 && !SupportedConditionTypes.Contains(type))
            .Distinct()
            .Order())
        {
            diagnostics.Add(
                Diagnostic(
                    ruleName,
                    $"Unsupported Outlook {diagnosticKind} '{DisplayCondition(conditionType)}' was not exported."));
        }
    }

    private static int ReadEnabledConditionType(object? condition)
    {
        try
        {
            dynamic dynCondition = condition!;
            if (!OutlookCom.SafeBool(() => dynCondition.Enabled))
                return -1;

            return OutlookCom.SafeInt(() => dynCondition.ConditionType, -1);
        }
        catch
        {
            return -1;
        }
    }

    private static void AddUnsupportedActionDiagnostics(
        object? actions,
        List<OutlookRuleExportDiagnostic> diagnostics,
        string ruleName)
    {
        foreach (int actionType in OutlookCom.AsEnumerable(actions)
            .Select(ReadEnabledActionType)
            .Where(type => type >= 0 && !SupportedActionTypes.Contains(type))
            .Distinct()
            .Order())
        {
            diagnostics.Add(
                Diagnostic(
                    ruleName,
                    $"Unsupported Outlook action '{DisplayAction(actionType)}' was not exported."));
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

    private static void AddAction(
        RuleDefinition definition,
        RuleActionType type,
        IEnumerable<string> values)
    {
        var cleanValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanValues.Count == 0 && type is not RuleActionType.Stop)
        {
            return;
        }

        definition.Actions.Add(new RuleAction
        {
            Type = type,
            Values = cleanValues
        });
    }

    private static void AddCondition(
        List<RuleCondition> target,
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

        target.Add(new RuleCondition
        {
            Type = type,
            Values = cleanValues
        });
    }

    private string NormalizeFolder(
        string folderPath,
        IReadOnlyDictionary<OutlookDefaultFolderRole, string> defaultFolderPaths) =>
        folderNormalizer.Normalize(
            new OutlookFolderPathContext
            {
                FolderPath = folderPath,
                DefaultFolderPaths = defaultFolderPaths
            });

    private static string DisplayCondition(int conditionType) =>
        ConditionNames.TryGetValue(conditionType, out string? name)
            ? name
            : $"ConditionType {conditionType}";

    private static string DisplayAction(int actionType) =>
        ActionNames.TryGetValue(actionType, out string? name)
            ? name
            : $"ActionType {actionType}";

    private static OutlookRuleExportDiagnostic Diagnostic(string ruleName, string message) =>
        new(ruleName, message);

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
}
