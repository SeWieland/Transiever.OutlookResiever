# Transiever.OutlookResiever Outlook Export

This document is the canonical description of classic Outlook access, export mapping, and folder normalization.

The system boundary lives in [architecture](architecture.md).
Command-facing behavior lives in [../src/Transiever.OutlookResiever.Cli/README.md](../src/Transiever.OutlookResiever.Cli/README.md).

## COM Access

The adapter uses late-bound classic Outlook COM access:

```csharp
Type.GetTypeFromProgID("Outlook.Application")
Activator.CreateInstance(...)
dynamic
```

*Note for Agents*:

Do not add Office interop packages.
Keep `olrx` Windows/x64 and `[STAThread]`.
Release COM objects best-effort and return per-rule diagnostics rather than aborting the entire export.

## Source Documents

Every export writes a `Transiever.SieveRuler` schema v1 document with `sourceId: "outlook"`.

The `run` workflow exports Outlook rules to `rules.json`.
Server reconciliation never overwrites the source document with server state.

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

Provider-specific mailbox root overrides are read from `OUTLOOKRESIEVER_FOLDER_*`
environment variables in the CLI composition root.
Folder mapping customization is environment-variable based, not CLI-flag based.
