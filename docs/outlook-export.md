# Transiever.OutlookResiever Outlook Export

This document is the canonical description of classic Outlook access, export mapping, and folder normalization.

The system boundary lives in [architecture](architecture.md).
Command-facing behavior lives in the [OutlookResiever CLI guide](../src/Transiever.OutlookResiever.Cli/README.md).

## COM Access

The adapter uses late-bound classic Outlook COM access:

```csharp
Type.GetTypeFromProgID("Outlook.Application")
Activator.CreateInstance(...)
dynamic
```

Note for agents:

Do not add Office interop packages.
Keep `olrx` Windows/x64 and `[STAThread]`.
Release COM objects best-effort and return per-rule diagnostics instead of aborting the entire export.

## Source Documents

Every export writes a `Transiever.SieveRuler` schema v1 document with `sourceId: "outlook"`.

The `run` workflow exports Outlook rules in memory by default.
It writes `rules.json` only when `--write-artifacts` is selected.
Server reconciliation never overwrites the source document with server state.

## Rule Mapping

Only enabled receive rules are exported.
Send rules are reported as diagnostics.

Supported Outlook conditions and exceptions map to SieveRuler conditions:

* `olConditionFrom` maps sender recipients to `SenderContains`.
* `olConditionSenderAddress` maps sender text to `SenderContains`.
* `olConditionSentTo` maps recipient entries to `ReceiverContains`.
* `olConditionRecipientAddress` maps recipient text to `ReceiverContains`.
* `olConditionSubject` maps text to `SubjectContains`.
* `olConditionBody` maps text to `BodyContains`.
* `olConditionBodyOrSubject` maps text to `SubjectOrBodyContains`.
* `olConditionHasAttachment` maps to `HasAttachment`.

Outlook exceptions are exported into the SieveRuler `exceptions` collection.
Generated Sieve treats them as blocking tests.

Supported Outlook actions map to explicit SieveRuler actions:

* `olRuleActionMoveToFolder` maps to `FileInto` and also fills the simple `targetFolder` shortcut.
* `olRuleActionCopyToFolder` maps to `CopyInto`.
* `olRuleActionDelete` maps to `FileInto` for the Trash default folder.
* `olRuleActionRedirect` maps recipients to `Redirect`.
* `olRuleActionMarkRead` maps to `SetFlags` with `\Seen`.
* `olRuleActionStop` maps to `Stop`.

Permanent delete is intentionally not mapped.
Client-only and unsafe Outlook behavior is reported as diagnostics instead of approximated in Sieve.
This includes scripts, applications, sounds, alerts, printing, categories, importance, sensitivity, RSS, address-book membership, local-machine rules, unsupported send-rule actions, and wizard-only conditions.

## Legacy Partial Export

Current schema-v1 export is collection-level best effort.
One failed or unsupported-only rule does not hide later rules.
A mixed rule that still has at least one supported condition and action currently emits that supported subset and reports every omitted enabled condition, exception, or action as a rule-scoped diagnostic.
This legacy partial output is not an exact conversion and is not used as the downstream baseline artifact; future portability plans classify semantic loss and require acknowledgement before apply.
The v1 regression identity is the exported name plus `originalOrder`, not a synthesized rule ID.

## Folder Normalization

Folder paths are normalized during Outlook export.
The adapter prefers Outlook default-folder identity from the rule store,
then falls back to mailbox.org/Open-Xchange German and English default-folder aliases.

Examples:

```text
Posteingang/Drucker -> INBOX/Drucker
Inbox/Printers -> INBOX/Printers
Entwürfe -> Drafts
Gesendete Objekte -> Sent
Papierkorb -> Trash
Junk-E-Mail -> Spam
Archiv -> Archive
```

Provider-specific mailbox root overrides are read from `OUTLOOKRESIEVER_FOLDER_*` environment variables in the CLI composition root.
Folder mapping customization is environment-variable based, not CLI-flag based.
