namespace Transiever.OutlookResiever.Services;

/// <summary>
/// Input context for normalizing an Outlook folder path.
/// </summary>
public sealed record OutlookFolderPathContext
{
    public required string FolderPath { get; init; }

    public IReadOnlyDictionary<OutlookDefaultFolderRole, string> DefaultFolderPaths { get; init; } =
        new Dictionary<OutlookDefaultFolderRole, string>();
}
