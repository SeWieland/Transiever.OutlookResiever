# Transiever.OutlookResiever

Windows adapter that exports supported classic Outlook rules into the
cross-platform `Transiever.SieveRuler` JSON contract. The `olrx` CLI remains a
compatibility frontend and delegates optimization, Sieve generation,
reconciliation, ManageSieve deployment, and rollback to
`Transiever.SieveRuler`. Generated managed Sieve includes provider UI metadata,
and `olrx` exposes SieveRuler's default bounded inactive history pruning during
deployment.
Outlook folder display paths are normalized to IMAP/Sieve mailbox names during
export, with mailbox.org/Open-Xchange defaults for localized German and English
default folders such as `Posteingang` -> `INBOX`.

```text
olrx / Transiever.OutlookResiever
    -> Transiever.SieveRuler
        -> Transiever.ManageSieve
```

## Projects

```text
src/Transiever.OutlookResiever/              Outlook COM adapter
src/Transiever.OutlookResiever.Cli/          `olrx` compatibility CLI
src/Transiever.OutlookResiever.UnitTest/     Adapter tests
src/Transiever.OutlookResiever.Cli.UnitTest/ CLI compatibility tests
```

```bash
dotnet build Transiever.OutlookResiever.slnx
dotnet test Transiever.OutlookResiever.slnx
dotnet run --project src/Transiever.OutlookResiever.Cli -- --help
```

The current development build references the sibling
`Transiever.SieveRuler` project. Published builds must use a versioned package
reference.

See the [`olrx` guide](src/Transiever.OutlookResiever.Cli/README.md) and
[architecture](docs/architecture.md).
