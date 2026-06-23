# Transiever.OutlookResiever Architecture

## Boundary

```text
Classic Outlook COM
    -> Transiever.OutlookResiever adapter
    -> Transiever.SieveRuler RuleDocument (`sourceId: outlook`)
    -> Transiever.SieveRuler engine
    -> Transiever.ManageSieve
```

`Transiever.OutlookResiever` owns discovery and mapping of supported classic
Outlook rules. `Transiever.SieveRuler` owns the JSON contract, optimization,
Sieve processing, reconciliation, preservation, review artifacts, and
deployment policy. `Transiever.ManageSieve` owns ManageSieve protocol
execution.

Folder paths are normalized during Outlook export. The adapter prefers Outlook
default-folder identity from the rule store, then falls back to mailbox.org /
Open-Xchange German and English default-folder aliases, so localized display
paths such as `Posteingang/Drucker` become Sieve mailbox paths such as
`INBOX/Drucker`. Provider-specific mailbox root overrides are read from
`OUTLOOKRESIEVER_FOLDER_*` environment variables in the CLI composition root.

`olrx` remains a Windows/x64 convenience wrapper. Its `run` command is the
guided Outlook workflow: export Outlook rules, choose optimization when
interactive, preview server reconciliation, ask before deployment, and then use
SieveRuler's deployment prompts. When preview targets the current active script,
SieveRuler creates a server-side backup and replaces that active script in
place by default; non-active targets are activated by default. Its `export`
command is local; `inspect`, `optimize`, `generate`, `preview`, `deploy`, and
`rollback` delegate to
`Transiever.SieveRuler`. `run` and `deploy` expose SieveRuler's history
retention controls; by default inactive SieveRuler-owned history is pruned
after successful deployment while the oldest backup and newest 5 history
scripts are retained.

Preview reads an existing source document from `rules.json` and writes combined
state to `reconciled-rules.json`; it never exports Outlook rules implicitly and
never overwrites the source document with server state. `candidate-rules.json`
contains the managed rules actually rendered into `candidate.sieve`.
`deployment-plan.json` is a SieveRuler plan; new plans preserve the active
script name by default and include rollback metadata for server-side backups.

The CLI accepts existing `OUTLOOKRESIEVER_SIEVE_*` configuration and falls back
to `SIEVERULER_SIEVE_*`. Outlook-specific variables take precedence.

The CLI entry point remains STA. The adapter continues to use late-bound COM and
must not add Office interop package dependencies.
