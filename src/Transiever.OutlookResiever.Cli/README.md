# `olrx`

`olrx` moves supported classic Outlook receive rules to server-side Sieve.
The main path is `olrx run`.
It exports from Outlook, optionally optimizes duplicate rules, previews the server candidate,
then deploys through ManageSieve after confirmation.

Repository overview lives in [../../README.md](../../README.md).
System boundaries live in [../../docs/architecture.md](../../docs/architecture.md).
Outlook export details live in [../../docs/outlook-export.md](../../docs/outlook-export.md).

## TL;DR

Configure ManageSieve:

```text
TRANSIEVER_SIEVE_HOST=sieve.example.com
TRANSIEVER_SIEVE_USERNAME=user@example.com
TRANSIEVER_SIEVE_PASSWORD=secret
```

Run the guided migration:

```bash
olrx run --optimize balanced
```

Deploy without the final prompt when you are comfortable with the preview:

```bash
olrx run --optimize balanced --deploy
```

Undo the latest `olrx` deployment from the newest server-side backup:

```bash
olrx rollback
```

`olrx run` writes no local files by default.
Use `--write-artifacts` only when you want local review files.

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

GitHub releases attach a self-contained `olrx` asset for `win-x64`.
No Linux or Windows x86 assets are produced because `olrx` targets classic Outlook COM.

## Commands

```bash
olrx run
olrx rollback
olrx export
```

`run` is the normal workflow.
It exports supported Outlook rules in memory, asks for optimization when running interactively,
previews the server-side candidate, and asks before deployment.
When deployment replaces the active server script, SieveRuler first writes a server-side `srtx-backup-*` copy.

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
`--write-artifacts` writes local review files during `run`.
Artifact path options such as `--rules`, `--candidate`, `--server-snapshot`, and `--plan` require `--write-artifacts` on `run`.

`rollback` restores the newest inactive SieveRuler backup on the server.
It creates a fresh backup of the current active script before restoring the selected backup.
Use `olrx rollback --dry-run` to validate which backup would be restored without changing the server.

`export` writes supported Outlook rules to `rules.json`.
Use `olrx export --rules <file>` to select a different destination.

Available optimization modes are `conservative`, `balanced`, and `aggressive`.
The short forms are `-o`, `-oo`, and `-ooo`.
Optimization here means reducing duplicate generated rules before preview and deployment.
Mode semantics are owned by SieveRuler and documented in the [SieveRuler CLI guide].

Generic inspection, Sieve generation, deployment-plan rollback, and history commands live in [`srtx`].

## Server Configuration

Configure ManageSieve through environment variables:

```text
TRANSIEVER_SIEVE_HOST=sieve.example.com
TRANSIEVER_SIEVE_PORT=4190
TRANSIEVER_SIEVE_USERNAME=user@example.com
TRANSIEVER_SIEVE_PASSWORD=secret
TRANSIEVER_SIEVE_SECURITY_MODE=StartTlsRequired
```

Use `--sieve-host`, `--sieve-port`, `--sieve-username`, `--sieve-password`,
and `--sieve-security-mode` to override those values for a targeted command.
The port and security mode are optional.
The default is port 4190 with required STARTTLS.
`ImplicitTls` is also supported.
Plaintext authentication is refused.
If the password variable is absent, an interactive terminal prompts without echoing it.

## Review Artifacts

`olrx run --write-artifacts` writes `rules.json`, `reconciled-rules.json`, `candidate-rules.json`,
`server-active.sieve`, `candidate.sieve`, and `deployment-plan.json`.

* `rules.json` is the Outlook export source document.
* `reconciled-rules.json` is the ownership review document.
* `candidate-rules.json` contains the managed rules actually rendered into `candidate.sieve`, including optimization when selected.
* `server-active.sieve` is the downloaded active server script snapshot.
* `candidate.sieve` is the script candidate.
* `deployment-plan.json` is an advanced SieveRuler artifact for low-level `srtx deploy` or plan-based rollback.

Artifacts are not required for the normal `olrx run` and `olrx rollback` workflow.

## Incremental Sync FAQ

**What happens when I run `olrx run` again?**

Outlook is authoritative for rules with `sourceId: "outlook"`.
Rules that were previously managed from Outlook but no longer exist in the new Outlook export
are removed from the managed server-side region.
Rules from other managed sources remain untouched.

**What happens to manual provider-side changes?**

Unsupported or user-authored Sieve content is preserved byte-for-byte where possible.
Compatible provider-side rules can be adopted into the managed region or preserved externally.
Interactive runs ask; redirected input preserves them.
Use `--adopt-compatible` or `--preserve-compatible` to make the choice explicit.

**What if a manual server rule duplicates an Outlook rule?**

When a compatible external server rule already represents the same behavior,
SieveRuler suppresses the generated duplicate and reports a diagnostic.

**How does rollback work without local files?**

Deployment keeps server-side `srtx-backup-*` scripts.
`olrx rollback` restores the newest inactive backup and creates a fresh backup of the current active script first.
The local `deployment-plan.json` artifact is only for advanced `srtx` workflows.

**Can `olrx` delete obsolete Outlook rules after migration?**

Not currently.
Outlook-side cleanup is intentionally not implemented until the COM behavior is verified and a safe opt-in design exists.

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

`olrx` exports enabled Outlook receive rules whose server-side meaning maps cleanly to Sieve.

Supported conditions and exceptions include:

* sender
* recipient
* subject
* body
* subject-or-body
* has-attachment

Supported actions include:

* move
* copy
* redirect
* mark-read
* delete-to-Trash
* stop processing

Only SieveRuler's strict compatible Sieve subset is imported semantically.
Unsupported and user-authored Sieve content is retained without rewriting.
Unsupported enabled Outlook shapes are reported as diagnostics.
Review target folder names, redirect recipients, mark-read behavior, optimization diagnostics, and server capabilities before deployment.

[`srtx`]: https://github.com/SeWieland/Transiever.SieveRuler
[SieveRuler CLI guide]: https://github.com/SeWieland/Transiever.SieveRuler
