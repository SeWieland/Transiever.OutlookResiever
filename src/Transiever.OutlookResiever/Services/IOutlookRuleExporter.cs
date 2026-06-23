
using Transiever.SieveRuler.Models;

namespace Transiever.OutlookResiever.Services;

public interface IOutlookRuleExporter
{
    OutlookRuleExportResult Export();
}

public sealed record OutlookRuleExportResult
{
    public IReadOnlyCollection<RuleDefinition> Rules { get; init; } = [];

    public IReadOnlyCollection<OutlookRuleExportDiagnostic> Diagnostics { get; init; } = [];
}

public sealed record OutlookRuleExportDiagnostic(string RuleName, string Message);
