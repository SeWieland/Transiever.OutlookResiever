# Transiever.OutlookResiever Architecture

This document is the canonical description of the OutlookResiever system boundary and dependency direction.
The root [README](../README.md) is the repo entry point.
The CLI guide lives in [../src/Transiever.OutlookResiever.Cli/README.md](../src/Transiever.OutlookResiever.Cli/README.md).
Outlook export details live in [outlook-export](outlook-export.md).

## Boundary

```text
Classic Outlook COM
    -> Transiever.OutlookResiever adapter
    -> Transiever.SieveRuler RuleDocument (`sourceId: outlook`)
    -> Transiever.SieveRuler engine
    -> Transiever.ManageSieve
```

`Transiever.OutlookResiever` owns discovery and mapping of supported classic Outlook rules.
`Transiever.SieveRuler` owns these concerns:

* The JSON contract.
* Optimization.
* Sieve processing.
* Reconciliation and preservation.
* Review artifacts.
* Deployment policy.
`Transiever.ManageSieve` owns ManageSieve protocol execution.

`olrx` is a Windows/x64 workflow CLI.
Its `run` command is the guided Outlook workflow:
It exports Outlook rules, chooses optimization when interactive, previews server reconciliation, and asks before deployment.

Its `export` command is local.
Use `srtx` for generic inspection, optimization, Sieve generation, preview, deployment, rollback, and history commands.

The CLI accepts `OUTLOOKRESIEVER_SIEVE_*` configuration and falls back to `SIEVERULER_SIEVE_*`.
Outlook-specific variables take precedence.

## Canonical References

Use the focused docs instead of restating the same policy in multiple places:

* [outlook-export](outlook-export.md) for COM access, folder normalization, and source document behavior.
* [CLI guide](../src/Transiever.OutlookResiever.Cli/README.md) for command-facing behavior and operator configuration.
* SieveRuler's synchronization policy for preview, deployment, rollback, and retained-history semantics.
