# Transiever.OutlookResiever

Windows adapter and CLI for exporting supported classic Outlook rules into the cross-platform `Transiever.SieveRuler` JSON contract.

`olrx` remains a compatibility frontend. It delegates rule optimization, Sieve generation, reconciliation,
ManageSieve deployment, rollback, and retained history policy to `Transiever.SieveRuler`.

```text
olrx / Transiever.OutlookResiever
    -> Transiever.SieveRuler
        -> Transiever.ManageSieve
```

## Documentation Map

Start here, then follow the focused guides:

* [CLI guide](src/Transiever.OutlookResiever.Cli/README.md) for commands, environment variables, Outlook export, preview, deployment, and rollback.
* [adapter guide](src/Transiever.OutlookResiever/README.md) for the Outlook COM adapter boundary.
* [architecture](docs/architecture.md) for repository boundaries and dependency direction.
* [Outlook export](docs/outlook-export.md) for COM, rule mapping, folder normalization, and source document behavior.

## Repository Layout

```text
src/Transiever.OutlookResiever/              Outlook COM adapter
src/Transiever.OutlookResiever.Cli/          `olrx` compatibility CLI
src/Transiever.OutlookResiever.UnitTest/     Adapter tests
src/Transiever.OutlookResiever.Cli.UnitTest/ CLI compatibility tests
```

## Feature Summary

* Late-bound classic Outlook COM rule export.
* `Transiever.SieveRuler` schema v2 output with `sourceId: "outlook"`.
* Folder display path normalization for IMAP/Sieve mailbox names.
* `olrx` compatibility commands for local conversion and server synchronization.

Operational details intentionally live in the linked component guides instead of being repeated here.

## Development

```bash
dotnet build Transiever.OutlookResiever.slnx
dotnet test Transiever.OutlookResiever.slnx
dotnet run --project src/Transiever.OutlookResiever.Cli -- --help
```

## Publication Note

The current development build references the sibling `Transiever.SieveRuler` project.
Published builds must use a versioned package reference.
