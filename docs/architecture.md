# Transiever.OutlookResiever Architecture

This document is the canonical description of the OutlookResiever system boundary and dependency direction.

## Boundary

```text
Classic Outlook COM
    -> Transiever.OutlookResiever adapter
    -> Transiever.SieveRuler RuleDocument (`sourceId: outlook`)
    -> Transiever.SieveRuler engine
    -> Transiever.ManageSieve
```

`Transiever.OutlookResiever` owns discovery and stable receive-rule mapping for supported classic Outlook rules.
`Transiever.SieveRuler` owns these concerns:

* The JSON contract.
* Optimization.
* Sieve processing.
* Reconciliation and preservation.
* Review artifacts.
* Deployment policy.
`Transiever.ManageSieve` owns ManageSieve protocol execution.

## Focused Docs

Use the focused docs instead of restating the same policy in multiple places:

* [outlook-export](outlook-export.md) for COM access, rule mapping, folder normalization, and source document behavior.
* [CLI guide](../src/Transiever.OutlookResiever.Cli/README.md) for command-facing behavior and operator configuration.
* SieveRuler's synchronization policy for preview, deployment, rollback, and retained-history semantics.
