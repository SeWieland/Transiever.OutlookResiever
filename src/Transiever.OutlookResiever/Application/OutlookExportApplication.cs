using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Application;

/// <summary>
/// Requests export of the supported classic Outlook rules to a rules document.
/// </summary>
public sealed record ExportRulesRequest(
    string RulesFile,
    bool DryRun = false);

/// <summary>
/// Result of exporting Outlook rules.
/// </summary>
public sealed record ExportRulesResult(
    RuleDocument Document,
    IReadOnlyCollection<OutlookRuleExportDiagnostic> Diagnostics,
    string RulesFile,
    bool FilesWritten);

/// <summary>
/// Application service that converts Outlook rules into Transiever rules documents.
/// </summary>
public sealed class OutlookExportApplication(
    IOutlookRuleExporter exporter,
    IRuleSerializer serializer)
{
    public async Task<ExportRulesResult> ExportAsync(
        ExportRulesRequest request,
        CancellationToken cancellationToken = default)
    {
        OutlookRuleExportResult export = exporter.Export();
        var document = new RuleDocument
        {
            SourceId = "outlook",
            Rules = export.Rules.ToList()
        };

        if (!request.DryRun)
        {
            await serializer.SaveDocumentAsync(
                document,
                request.RulesFile,
                cancellationToken);
        }

        return new ExportRulesResult(
            document,
            export.Diagnostics,
            request.RulesFile,
            !request.DryRun);
    }
}
