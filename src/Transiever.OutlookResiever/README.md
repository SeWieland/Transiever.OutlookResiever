# Transiever.OutlookResiever adapter

This Windows/x64 library contains only:

* Late-bound classic Outlook COM access.
* Stable Outlook receive-rule-to-`Transiever.SieveRuler` mapping.
* Outlook folder normalization.
* export diagnostics and the `OutlookExportApplication`.

Exports are `Transiever.SieveRuler` schema v1 documents with `sourceId: "outlook"`.
Unsupported enabled Outlook rule shapes are reported as diagnostics instead of being approximated.

Outlook access and folder normalization are documented in [../../docs/outlook-export.md](../../docs/outlook-export.md).
Repository boundaries are documented in [../../docs/architecture.md](../../docs/architecture.md).

The adapter must not implement JSON schemas, optimization, Sieve parsing or generation, reconciliation, ManageSieve, or deployment policy.
Those belong to SieveRuler.

Local umbrella development uses the sibling SieveRuler project when present.
Standalone published builds fall back to the versioned `Transiever.SieveRuler` NuGet package.
