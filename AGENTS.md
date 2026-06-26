# AGENTS.md

## Project

`Transiever.OutlookResiever` is the Windows/classic-Outlook source adapter for
`Transiever.SieveRuler`. Its `olrx` CLI is a compatibility wrapper over the
adapter and `Transiever.SieveRuler`.

The repository name is intentionally spelled `OutlookResiever`.

## Layout and Boundary

```text
Transiever.OutlookResiever.slnx
docs/architecture.md
docs/outlook-export.md
src/
  Transiever.OutlookResiever/              Outlook COM adapter
  Transiever.OutlookResiever.Cli/          `olrx` compatibility CLI
  Transiever.OutlookResiever.UnitTest/
  Transiever.OutlookResiever.Cli.UnitTest/
```

Keep only Outlook COM access, Outlook mapping, folder normalization, and export
diagnostics in the library. `Transiever.SieveRuler` owns rule models, JSON,
optimization, Sieve processing, reconciliation, ManageSieve adaptation, and
deployment.

The adapter temporarily references the sibling `Transiever.SieveRuler`
project. The CLI references both adapter and `Transiever.SieveRuler`. Replace
these with versioned package references before publication.

## Outlook Constraints

Use late-bound COM:

```csharp
Type.GetTypeFromProgID("Outlook.Application")
Activator.CreateInstance(...)
dynamic
```

Do not add Office interop packages. Keep `olrx` Windows/x64 and `[STAThread]`.
Release COM objects best-effort and return per-rule diagnostics rather than
aborting the entire export.

Every export writes `Transiever.SieveRuler` schema v2 with `sourceId: "outlook"`.
Outlook export and folder normalization behavior are documented once in
`docs/outlook-export.md`.

## CLI Behavior

Keep `run`, `export`, `inspect`, `optimize`, `generate`, `preview`, `deploy`,
and `rollback`. `run` is the guided Outlook workflow: export, optional
optimization, server preview, upload confirmation, and deployment through
`Transiever.SieveRuler`.

Generic commands delegate to `Transiever.SieveRuler`.
Preview reads an existing `rules.json` and never exports Outlook rules implicitly.
SieveRuler deployment, rollback, and history policy remain documented in the
SieveRuler repository.

Accept `OUTLOOKRESIEVER_SIEVE_*` first and `SIEVERULER_SIEVE_*` as fallback.
Accept `OUTLOOKRESIEVER_FOLDER_*` for mailbox root overrides such as
`OUTLOOKRESIEVER_FOLDER_JUNK`.
Running without a command displays help without accessing Outlook, files, or
network.

## Commands

```bash
dotnet build Transiever.OutlookResiever.slnx
dotnet test Transiever.OutlookResiever.slnx
dotnet run --project src/Transiever.OutlookResiever.Cli -- --help
```

Update this file, both READMEs, architecture, and Outlook export docs when their
contracts change. Unit tests must not require Outlook or credentials.
