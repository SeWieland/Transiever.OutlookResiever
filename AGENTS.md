# AGENTS.md

## Project

`Transiever.OutlookResiever` is the Windows/classic-Outlook source adapter for `Transiever.SieveRuler`.
Its `olrx` CLI exports Outlook rules and runs the guided Outlook-to-server workflow.

The repository name is intentionally spelled `OutlookResiever`.

## Layout and Boundary

```text
Transiever.OutlookResiever.slnx
docs/architecture.md
docs/outlook-export.md
src/
  Transiever.OutlookResiever/              Outlook COM adapter
  Transiever.OutlookResiever.Cli/          `olrx` workflow CLI
  Transiever.OutlookResiever.UnitTest/
  Transiever.OutlookResiever.Cli.UnitTest/
```

Keep only Outlook COM access, Outlook mapping, folder normalization, and export diagnostics in the library.
`Transiever.SieveRuler` owns rule models, JSON, optimization, Sieve processing, reconciliation, ManageSieve adaptation, and deployment.

The adapter temporarily references the sibling `Transiever.SieveRuler` project.
The CLI references both the adapter and `Transiever.SieveRuler`.
Replace these with versioned package references before publication.

## Outlook Constraints

Use late-bound COM:

```csharp
Type.GetTypeFromProgID("Outlook.Application")
Activator.CreateInstance(...)
dynamic
```

Do not add Office interop packages.
Keep `olrx` Windows/x64 and `[STAThread]`.
Release COM objects best-effort and return per-rule diagnostics instead of aborting the entire export.

Every export writes `Transiever.SieveRuler` schema v1 with `sourceId: "outlook"`.
Map only stable receive-rule semantics into SieveRuler rules.
Unsupported enabled Outlook rule conditions and actions must become export diagnostics with official Outlook enum names when known.
Outlook export and folder normalization behavior are documented once in `docs/outlook-export.md`.

## CLI Behavior

Keep only `run` and `export` in `olrx`.
`run` is the guided Outlook workflow.
It exports, optionally optimizes, previews the server, confirms upload, and deploys through `Transiever.SieveRuler`.
Generic rule inspection, optimization, Sieve generation, preview, deployment, rollback, and history commands belong to `srtx`.
The `srtx` command lives in the SieveRuler repository.

Accept `TRANSIEVER_SIEVE_*` for shared ManageSieve server configuration.
Accept `--sieve-*` CLI options as command-level overrides.
Accept `OUTLOOKRESIEVER_FOLDER_*` for mailbox root overrides such as `OUTLOOKRESIEVER_FOLDER_JUNK`.
Running without a command displays help without accessing Outlook, files, or network.

## Commands

```bash
dotnet build Transiever.OutlookResiever.slnx
dotnet test Transiever.OutlookResiever.slnx
dotnet run --project src/Transiever.OutlookResiever.Cli -- --help
```

Update this file, both READMEs, architecture, and Outlook export docs when their contracts change.
Unit tests must not require Outlook or credentials.
