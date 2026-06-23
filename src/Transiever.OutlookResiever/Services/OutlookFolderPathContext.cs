namespace Transiever.OutlookResiever.Services;

public sealed record OutlookFolderPathContext
{
    public required string FolderPath { get; init; }

    public IReadOnlyDictionary<OutlookDefaultFolderRole, string> DefaultFolderPaths { get; init; } =
        new Dictionary<OutlookDefaultFolderRole, string>();
}
