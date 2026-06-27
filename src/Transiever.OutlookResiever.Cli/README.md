# `olrx`

`olrx` exports supported rules from classic Outlook into `Transiever.SieveRuler` JSON.
It can also run the guided Outlook-to-server workflow.

Repository overview lives in [../../README.md](../../README.md).
System boundaries live in [../../docs/architecture.md](../../docs/architecture.md).
Outlook export details live in [../../docs/outlook-export.md](../../docs/outlook-export.md).

## Requirements

* Windows x64
* Classic Outlook for Windows, installed and configured
* .NET 10 runtime for framework-dependent builds

Run `olrx` without arguments to display help.
This does not access Outlook, contact a server, or write files.

During development, substitute:

```bash
dotnet run --project src/Transiever.OutlookResiever.Cli --
```

for `olrx` in the examples below.

## Commands

```bash
olrx export
olrx run
```

`export` writes supported Outlook rules to `rules.json`.
Use `--rules <file>` to select a different destination.

`run` exports supported Outlook rules.
It asks for optimization when running interactively.
It previews the server-side candidate, writes review artifacts, and asks before deployment.

Use explicit flags for unattended runs:

```bash
olrx run --no-optimize
olrx run --optimize balanced
olrx run --deploy
olrx run --history-limit 3
olrx run --no-prune-history
```

`--deploy` skips the deployment prompt.
`--dry-run` never writes output files or mutates the server.

Available optimization modes are `conservative`, `balanced`, and `aggressive`.
The short forms are `-o`, `-oo`, and `-ooo`.

Generic inspection, optimization, Sieve generation, preview, deployment, rollback, and history commands live in `srtx`.

## Server Configuration

Configure ManageSieve through environment variables:

```text
OUTLOOKRESIEVER_SIEVE_HOST=sieve.example.com
OUTLOOKRESIEVER_SIEVE_PORT=4190
OUTLOOKRESIEVER_SIEVE_USERNAME=user@example.com
OUTLOOKRESIEVER_SIEVE_PASSWORD=secret
OUTLOOKRESIEVER_SIEVE_SECURITY_MODE=StartTlsRequired
```

The port and security mode are optional.
The default is port 4190 with required STARTTLS.
`ImplicitTls` is also supported.
Plaintext authentication is refused.
If the password variable is absent, an interactive terminal prompts without echoing it.

The equivalent `SIEVERULER_SIEVE_*` variables are accepted as fallback.
`OUTLOOKRESIEVER_SIEVE_*` takes precedence.

## Review Artifacts

`run` writes `reconciled-rules.json`, `candidate-rules.json`, `server-active.sieve`, `candidate.sieve`, and `deployment-plan.json`.

* `reconciled-rules.json` is the ownership review document.
* `candidate-rules.json` contains the managed rules actually rendered into `candidate.sieve`, including optimization when selected.

Interactive preview offers to adopt compatible external rules.
Redirected input preserves them.
Use `--adopt-compatible` or `--preserve-compatible` to make the choice explicit.
If the server has an active script, preview uses that active script name as the deployment target by default.
Use `--script-name <name>` to override it.

When deployment proceeds, SieveRuler validates the exact previewed candidate and rechecks the active script snapshot.
If the target is active, it uploads a server-side `srtx-backup-*` copy, replaces the active script in place, and retains the backup.
If the target is not active, it uploads and activates that target.

Deployment prunes inactive SieveRuler-owned history by default, retaining the oldest backup plus the newest 5 remaining history scripts.
Use `--history-limit <count>` to change that newest-history count.
Use `--no-prune-history` to disable deletion.

## Folder Mapping

Outlook folder export uses mailbox.org/Open-Xchange defaults for localized default folders.
It first tries Outlook's default-folder identity.
It then maps German and English display roots:

* `Posteingang` and `Inbox` map to `INBOX`.
* `Entwürfe` and `Drafts` map to `Drafts`.
* `Gesendete Objekte` and `Sent Items` map to `Sent`.
* `Papierkorb` and `Deleted Items` map to `Trash`.
* `Junk-E-Mail` and `Junk` map to `Spam`.
* `Archiv` and `Archive` map to `Archive`.

Override mailbox names without CLI flags:

```text
OUTLOOKRESIEVER_FOLDER_INBOX=INBOX
OUTLOOKRESIEVER_FOLDER_DRAFTS=Drafts
OUTLOOKRESIEVER_FOLDER_SENT=Sent
OUTLOOKRESIEVER_FOLDER_TRASH=Trash
OUTLOOKRESIEVER_FOLDER_JUNK=Spam
OUTLOOKRESIEVER_FOLDER_ARCHIVE=Archive
```

## Supported Rule Subset

The SieveRuler model supports sender, recipient, subject, body, and subject-or-body contains conditions with `All` and `Any` combinations.
Generated actions move messages to a folder.

Only SieveRuler's strict compatible Sieve subset is imported semantically.
Unsupported and user-authored Sieve content is retained without rewriting.
Review target folder names, optimization diagnostics, and server capabilities before deployment.
