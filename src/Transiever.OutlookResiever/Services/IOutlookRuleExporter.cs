
using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.Services;

/// <summary>
/// Exports supported Outlook rules into the shared Transiever rule model.
/// </summary>
public interface IOutlookRuleExporter
{
    OutlookRuleExportResult Export();
}

/// <summary>
/// Result of exporting rules from Outlook.
/// </summary>
public sealed record OutlookRuleExportResult
{
    public IReadOnlyCollection<RuleDefinition> Rules { get; init; } = [];

    public IReadOnlyCollection<OutlookRuleExportDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>
/// Describes a rule that could not be exported cleanly.
/// </summary>
public sealed record OutlookRuleExportDiagnostic(string RuleName, string Message);
