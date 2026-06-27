# Transiever.OutlookResiever

Windows adapter and CLI for exporting supported classic Outlook rules into the cross-platform `Transiever.SieveRuler` JSON contract.

`olrx` provides `export` for local Outlook export and `run` for the guided Outlook-to-server workflow.
Generic rule inspection, Sieve generation, preview, deployment, rollback, and history commands live in `srtx`.

```text
olrx / Transiever.OutlookResiever
    -> Transiever.SieveRuler
        -> Transiever.ManageSieve
```

## Documentation Map

Start here, then follow the focused guides:

* [CLI guide](src/Transiever.OutlookResiever.Cli/README.md) for commands, environment variables, Outlook export, preview, and deployment.
* [adapter guide](src/Transiever.OutlookResiever/README.md) for the Outlook COM adapter boundary.
* [architecture](docs/architecture.md) for repository boundaries and dependency direction.
* [Outlook export](docs/outlook-export.md) for COM, rule mapping, folder normalization, and source document behavior.

## Repository Layout

```text
src/Transiever.OutlookResiever/              Outlook COM adapter
src/Transiever.OutlookResiever.Cli/          `olrx` workflow CLI
src/Transiever.OutlookResiever.UnitTest/     Adapter tests
src/Transiever.OutlookResiever.Cli.UnitTest/ CLI tests
```

## Feature Summary

* Late-bound classic Outlook COM rule export.
* `Transiever.SieveRuler` schema v1 output with `sourceId: "outlook"`.
* Folder display path normalization for IMAP/Sieve mailbox names.
* `olrx export` and the guided `olrx run` synchronization workflow.

Operational details live in the linked component guides instead of being repeated here.

## Development

```bash
dotnet build Transiever.OutlookResiever.slnx
dotnet test Transiever.OutlookResiever.slnx
dotnet run --project src/Transiever.OutlookResiever.Cli -- --help
```

## Publication Note

The current development build references the sibling `Transiever.SieveRuler` project.
Published builds must use a versioned package reference.
