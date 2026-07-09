# Transiever.OutlookResiever

Synchronize supported classic Outlook receive rules to **server-side Sieve**.

This way, mail is sorted **before** it reaches your phone, tablet, webmail, or any another desktop client and transforms the inbox into a unordered mess.

`olrx` is the Outlook-focused CLI.
Its main workflow exports your Outlook rules, optionally deduplicates them, previews the server-side Sieve candidate,
deploys it through ManageSieve, and keeps a server-side backup for rollback.

Generic rule inspection and low-level Sieve operations live in [`srtx`](https://github.com/SeWieland/Transiever.SieveRuler).

```text
olrx / Transiever.OutlookResiever
    -> Transiever.SieveRuler
        -> Transiever.ManageSieve
```

## TL;DR

Configure your ManageSieve server once:

```powershell
$env:TRANSIEVER_SIEVE_HOST = "sieve.example.com"
$env:TRANSIEVER_SIEVE_USERNAME = "user@example.com"
$env:TRANSIEVER_SIEVE_PASSWORD = "secret"
```

Run the Outlook-to-server workflow:

```bash
olrx run --optimize balanced
```

`olrx run` writes no local files by default.
It asks before deployment and creates a server-side `srtx-backup-*` copy before replacing the active script.

Undo the last deployment from the server-side backup:

```bash
olrx rollback
```

Use `--write-artifacts` only when you want local review files for debugging or manual inspection.

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
* Stable receive-rule mapping for server-side Sieve conditions, actions, and exceptions.
* Folder display path normalization for IMAP/Sieve mailbox names.
* Fileless `olrx run` synchronization with optional duplicate-rule reduction through [SieveRuler optimization].
* `olrx rollback` for restoring the newest server-side SieveRuler backup.
* `olrx export` for writing an Outlook rules JSON document when you need one.

Operational details live in the linked component guides instead of being repeated here.

## Development

```bash
dotnet build Transiever.OutlookResiever.slnx
dotnet test Transiever.OutlookResiever.slnx
dotnet run --project src/Transiever.OutlookResiever.Cli -- --help
```

## Publication Note

GitHub Actions produce releases.
Stable releases come from `main`.
Beta prereleases come from `dev` and may be unstable.

Releases attach a self-contained `olrx` asset for `win-x64`.
`olrx` is Windows x64 only because it targets classic Outlook COM.

[SieveRuler optimization]: https://github.com/SeWieland/Transiever.SieveRuler
