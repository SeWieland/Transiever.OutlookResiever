# `olrx`

`olrx` exports supported rules from classic Outlook into
`Transiever.SieveRuler` JSON and provides compatibility commands that delegate
generic work to `Transiever.SieveRuler`.

## Requirements

* Windows x64
* Classic Outlook for Windows, installed and configured
* .NET 10 runtime for framework-dependent builds

Run `olrx` without arguments to display help. This does not access Outlook,
contact a server, or write files.

During development, substitute:

```bash
dotnet run --project src/Transiever.OutlookResiever.Cli --
```

for `olrx` in the examples below.

## Guided workflow

Run the normal Outlook-to-server workflow:

```bash
olrx run
```

`run` exports supported Outlook rules to `rules.json`, asks for optimization
when running interactively, previews the server-side candidate, writes review
artifacts, then asks before deployment. When the preview target is the current
active script, deployment creates a server-side backup and replaces that active
script in place. Otherwise it uploads and activates the target script.

Use explicit flags for unattended runs:

```bash
olrx run --no-optimize
olrx run --optimize balanced
olrx run --deploy
olrx run --activate
olrx run --history-limit 3
```

`--deploy` skips the deployment prompt. `--activate` is accepted as a
compatibility alias for `--deploy`; deploy activates by default. `--dry-run`
never writes output files or mutates the server.
Deployment prunes inactive SieveRuler-owned history by default, retaining the
oldest backup plus the newest 5 remaining history scripts. Use
`--history-limit <count>` to change that newest-history count or
`--no-prune-history` to disable deletion.

Available optimization modes are `conservative`, `balanced`, and `aggressive`.
The short forms are `-o`, `-oo`, and `-ooo`.

## Local conversion

Use individual commands when you only want local artifacts:

```bash
olrx export
olrx inspect
olrx optimize aggressive
olrx generate --optimize balanced
```

Default artifacts are `rules.json`, `rules.optimized.json`, and `rules.sieve`.
Use `--rules`, `--output`, and `--sieve` to select different paths. `--dry-run`
performs the operation without writing output files.

`optimize` only writes optimized JSON. Use `generate --optimize <mode>` or
`run --optimize <mode>` when the Sieve file or server candidate should use the
optimized result.

## Server synchronization

Configure ManageSieve through environment variables:

```text
OUTLOOKRESIEVER_SIEVE_HOST=sieve.example.com
OUTLOOKRESIEVER_SIEVE_PORT=4190
OUTLOOKRESIEVER_SIEVE_USERNAME=user@example.com
OUTLOOKRESIEVER_SIEVE_PASSWORD=secret
OUTLOOKRESIEVER_SIEVE_SECURITY_MODE=StartTlsRequired
```

The port and security mode are optional. The default is port 4190 with required
STARTTLS. `ImplicitTls` is also supported. Plaintext authentication is refused.
If the password variable is absent, an interactive terminal prompts without
echoing it.

The equivalent `SIEVERULER_SIEVE_*` variables are accepted as fallback.
`OUTLOOKRESIEVER_SIEVE_*` takes precedence for compatibility.

Outlook folder export uses mailbox.org/Open-Xchange defaults for localized
default folders. It first tries Outlook's default-folder identity and then maps
German/English display roots such as `Posteingang`/`Inbox` to `INBOX`,
`Entwürfe`/`Drafts` to `Drafts`, `Gesendete Objekte`/`Sent Items` to `Sent`,
`Papierkorb`/`Deleted Items` to `Trash`, `Junk-E-Mail`/`Junk` to `Spam`, and
`Archiv`/`Archive` to `Archive`. Override mailbox names without CLI flags:

```text
OUTLOOKRESIEVER_FOLDER_INBOX=INBOX
OUTLOOKRESIEVER_FOLDER_DRAFTS=Drafts
OUTLOOKRESIEVER_FOLDER_SENT=Sent
OUTLOOKRESIEVER_FOLDER_TRASH=Trash
OUTLOOKRESIEVER_FOLDER_JUNK=Spam
OUTLOOKRESIEVER_FOLDER_ARCHIVE=Archive
```

Create review artifacts from an existing rules JSON file without mutating the
server:

```bash
olrx preview --preserve-compatible
```

Preview reads `rules.json`; it does not export Outlook rules. It writes
`reconciled-rules.json`, `candidate-rules.json`, `server-active.sieve`,
`candidate.sieve`, and `deployment-plan.json`. `reconciled-rules.json` is the
ownership review document. `candidate-rules.json` contains the managed rules
actually rendered into `candidate.sieve`, including optimization when selected.
Interactive preview offers to adopt compatible external rules; redirected input
preserves them. Use `--adopt-compatible` or `--preserve-compatible` to make the
choice explicit. If the server has an active script, preview uses that active
script name as the deployment target by default; use `--script-name <name>` to
override it.

After reviewing the artifacts, deploy the exact candidate:

```bash
olrx deploy --plan deployment-plan.json
olrx deploy --plan deployment-plan.json --history-limit 3
olrx deploy --plan deployment-plan.json --no-prune-history
```

Deployment validates the plan, checks the candidate, and rechecks the active
script snapshot. If the target is active, it uploads a server-side
`srtx-backup-*` copy, replaces the active script in place, and retains the
backup. If the target is not active, it uploads an inactive script and asks
then activates it.
After success, deploy may delete only inactive SieveRuler-owned history scripts
named `srtx-YYYYMMDDHHMMSS-*` or `srtx-backup-YYYYMMDDHHMMSS-*`. It never
deletes the active script, target, source active script, current backup,
non-SieveRuler names, or the oldest backup. Cleanup failures are printed as
warnings.

Rollback uses the same deployment plan:

```bash
olrx rollback --plan deployment-plan.json
```

Version 3 plans restore the server-side backup into the target script. Legacy
v1/v2 plans reactivate the recorded source script, or disable Sieve processing
when the preview started with no active script. Rollback refuses to run if the
current active script no longer matches the deployed candidate; `--force`
bypasses only that current-active check, not backup or hash validation.

The tool never deletes the candidate, previous source script, current
server-side backup, or non-SieveRuler scripts, and it refuses to deploy from a
stale preview.

## Supported rule subset

The SieveRuler model supports sender, recipient, subject, body, and subject-or-body
contains conditions with `All` and `Any` combinations. Generated actions move
messages to a folder.

Only SieveRuler's strict compatible Sieve subset is imported semantically. Unsupported
and user-authored Sieve content is retained without rewriting. Review target
folder names, optimization diagnostics, and server capabilities before
activation.
