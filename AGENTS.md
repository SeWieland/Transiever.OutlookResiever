# AGENTS.md

## Project Boundary

`Transiever.OutlookResiever` is the Windows/classic-Outlook source adapter for `Transiever.SieveRuler`.
Its `olrx` CLI exports Outlook rules and runs the guided Outlook-to-server workflow.

The repository name is intentionally spelled `OutlookResiever`.
Keep only Outlook COM access, Outlook mapping, folder normalization, and export diagnostics in this repository.
`Transiever.SieveRuler` owns rule models, JSON, optimization, Sieve processing, reconciliation, ManageSieve adaptation, and deployment.

## Agent Index

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

The adapter temporarily references the sibling `Transiever.SieveRuler` project during local umbrella development.
Published standalone builds fall back to `Transiever.SieveRuler`nuget packages when the sibling checkout is absent.

## Canonical Docs

| Topic                                                                                 | Owner                                          |
| ------------------------------------------------------------------------------------- | ---------------------------------------------- |
| Outlook COM access, rule mapping, folder normalization, and source documents          | `docs/outlook-export.md`                       |
| Repository boundary and dependency direction                                          | `docs/architecture.md`                         |
| Adapter boundary                                                                      | `src/Transiever.OutlookResiever/README.md`     |
| CLI commands, options, environment variables, review artifacts, and operator workflow | `src/Transiever.OutlookResiever.Cli/README.md` |
| Public overview, docs map, feature summary, and development commands                  | `README.md`                                    |
| Repo boundary, validation, release constraints, and agent workflow                    | `AGENTS.md`                                    |

Do not update every document by default.
Update the canonical owner for the changed behavior.

## Validation

```bash
dotnet build Transiever.OutlookResiever.slnx
dotnet test Transiever.OutlookResiever.slnx
dotnet run --project src/Transiever.OutlookResiever.Cli -- --help
```

Unit tests must not require Outlook, credentials, Outlook COM, or a configured Outlook profile.

## Non-Negotiables

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

Keep only `run` and `export` in `olrx`.
Generic rule inspection, optimization, Sieve generation, preview, deployment, rollback, and history commands belong to `srtx`.

Accept `TRANSIEVER_SIEVE_*` for shared ManageSieve server configuration.
Accept `--sieve-*` CLI options as command-level overrides.
Accept `OUTLOOKRESIEVER_FOLDER_*` for mailbox root overrides such as `OUTLOOKRESIEVER_FOLDER_JUNK`.
Running without a command displays help without accessing Outlook, files, or network.

## When Docs Change

Update `AGENTS.md` only for agent workflow, repo boundary, validation, release constraints, or non-negotiable safety rules.
Update focused docs and READMEs through the ownership table above.
Keep documentation accurate, but do not duplicate the same contract across every Markdown file.

GitHub Actions are repository-local and run on Windows because `olrx` targets `net10.0-windows`.
Releases are manual, stable from `main`, and `beta` from `dev`.
CI and release validation must include package dependency mode with `SieveRulerProject` forced missing.
They attach a self-contained `olrx` asset for `win-x64` only.
