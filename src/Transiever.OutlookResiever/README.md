# Transiever.OutlookResiever adapter

This Windows/x64 library contains only:

* late-bound classic Outlook COM access;
* Outlook rule-to-`Transiever.SieveRuler` mapping;
* Outlook folder normalization;
* export diagnostics and the `OutlookExportApplication`.

Exports are `Transiever.SieveRuler` schema v2 documents with `sourceId:
"outlook"`.

Outlook access and folder normalization are documented in [../../docs/outlook-export.md](../../docs/outlook-export.md).
Repository boundaries are documented in [../../docs/architecture.md](../../docs/architecture.md).

The adapter must not implement JSON schemas, optimization, Sieve parsing or generation, reconciliation, ManageSieve, or deployment policy.
Those belong to SieveRuler.
