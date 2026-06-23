namespace Transiever.OutlookResiever.Services;

public sealed record OutlookFolderMappingOptions
{
    public string Inbox { get; init; } = "INBOX";

    public string Drafts { get; init; } = "Drafts";

    public string Sent { get; init; } = "Sent";

    public string Trash { get; init; } = "Trash";

    public string Junk { get; init; } = "Spam";

    public string Archive { get; init; } = "Archive";

    public string GetMailboxName(OutlookDefaultFolderRole role) =>
        role switch
        {
            OutlookDefaultFolderRole.Inbox => Inbox,
            OutlookDefaultFolderRole.Drafts => Drafts,
            OutlookDefaultFolderRole.Sent => Sent,
            OutlookDefaultFolderRole.Trash => Trash,
            OutlookDefaultFolderRole.Junk => Junk,
            OutlookDefaultFolderRole.Archive => Archive,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
}
