using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Application;

public sealed record ExportRulesRequest(
    string RulesFile,
    bool DryRun = false);

public sealed record ExportRulesResult(
    RuleDocument Document,
    IReadOnlyCollection<OutlookRuleExportDiagnostic> Diagnostics,
    string RulesFile,
    bool FilesWritten);

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
