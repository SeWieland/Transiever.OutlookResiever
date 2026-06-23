using Transiever.OutlookResiever.Services;

namespace Transiever.OutlookResiever.Cli;

public static class OutlookFolderMappingOptionsProvider
{
    public static OutlookFolderMappingOptions GetOptions() =>
        new()
        {
            Inbox = ReadFolderName("INBOX") ?? "INBOX",
            Drafts = ReadFolderName("DRAFTS") ?? "Drafts",
            Sent = ReadFolderName("SENT") ?? "Sent",
            Trash = ReadFolderName("TRASH") ?? "Trash",
            Junk = ReadFolderName("JUNK") ?? "Spam",
            Archive = ReadFolderName("ARCHIVE") ?? "Archive"
        };

    private static string? ReadFolderName(string suffix)
    {
        string? value = Environment.GetEnvironmentVariable(
            $"OUTLOOKRESIEVER_FOLDER_{suffix}");
        string? normalized = value?.Trim().Trim('/');
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }
}
