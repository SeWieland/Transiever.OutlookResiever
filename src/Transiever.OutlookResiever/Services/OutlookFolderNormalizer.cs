namespace Transiever.OutlookResiever.Services;

public sealed class OutlookFolderNormalizer : IFolderNormalizer
{
    private static readonly IReadOnlyDictionary<string, OutlookDefaultFolderRole> RootAliases =
        new Dictionary<string, OutlookDefaultFolderRole>(StringComparer.OrdinalIgnoreCase)
        {
            ["INBOX"] = OutlookDefaultFolderRole.Inbox,
            ["Posteingang"] = OutlookDefaultFolderRole.Inbox,
            ["Drafts"] = OutlookDefaultFolderRole.Drafts,
            ["Entwürfe"] = OutlookDefaultFolderRole.Drafts,
            ["Sent"] = OutlookDefaultFolderRole.Sent,
            ["Sent Items"] = OutlookDefaultFolderRole.Sent,
            ["Gesendet"] = OutlookDefaultFolderRole.Sent,
            ["Gesendete Objekte"] = OutlookDefaultFolderRole.Sent,
            ["Trash"] = OutlookDefaultFolderRole.Trash,
            ["Deleted Items"] = OutlookDefaultFolderRole.Trash,
            ["Papierkorb"] = OutlookDefaultFolderRole.Trash,
            ["Gelöschte Elemente"] = OutlookDefaultFolderRole.Trash,
            ["Spam"] = OutlookDefaultFolderRole.Junk,
            ["Junk"] = OutlookDefaultFolderRole.Junk,
            ["Junk Email"] = OutlookDefaultFolderRole.Junk,
            ["Junk-E-Mail"] = OutlookDefaultFolderRole.Junk,
            ["Archive"] = OutlookDefaultFolderRole.Archive,
            ["Archiv"] = OutlookDefaultFolderRole.Archive
        };

    private readonly OutlookFolderMappingOptions options;

    public OutlookFolderNormalizer()
        : this(new OutlookFolderMappingOptions())
    {
    }

    public OutlookFolderNormalizer(OutlookFolderMappingOptions options)
    {
        this.options = options;
    }

    public string Normalize(string outlookFolderPath) =>
        Normalize(new OutlookFolderPathContext { FolderPath = outlookFolderPath });

    public string Normalize(OutlookFolderPathContext context)
    {
        if (string.IsNullOrWhiteSpace(context.FolderPath))
            return "";

        string trimmedPath = context.FolderPath.Trim();
        string? byDefaultFolder = NormalizeByDefaultFolder(trimmedPath, context);
        if (byDefaultFolder is not null)
            return byDefaultFolder;

        List<string> parts = SplitOutlookPath(trimmedPath);

        if (parts.Count <= 1)
            return NormalizeRelativePath(trimmedPath);

        // Remove mailbox root.
        parts.RemoveAt(0);

        return NormalizeRelativeParts(parts);
    }

    private string? NormalizeByDefaultFolder(
        string folderPath,
        OutlookFolderPathContext context)
    {
        List<string> folderParts = SplitOutlookPath(folderPath);
        foreach ((OutlookDefaultFolderRole role, string defaultFolderPath) in
            context.DefaultFolderPaths
                .OrderByDescending(item => SplitOutlookPath(item.Value).Count))
        {
            List<string> defaultParts = SplitOutlookPath(defaultFolderPath);
            if (defaultParts.Count == 0 ||
                defaultParts.Count > folderParts.Count ||
                !StartsWith(folderParts, defaultParts))
            {
                continue;
            }

            return JoinMailboxPath(
                options.GetMailboxName(role),
                folderParts.Skip(defaultParts.Count));
        }

        return null;
    }

    private string NormalizeRelativePath(string folderPath) =>
        NormalizeRelativeParts(
            folderPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part)));

    private string NormalizeRelativeParts(IEnumerable<string> parts)
    {
        string[] cleanParts = parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim())
            .ToArray();
        if (cleanParts.Length == 0)
            return "";

        return RootAliases.TryGetValue(cleanParts[0], out OutlookDefaultFolderRole role)
            ? JoinMailboxPath(options.GetMailboxName(role), cleanParts.Skip(1))
            : string.Join("/", cleanParts);
    }

    private static string JoinMailboxPath(
        string root,
        IEnumerable<string> childParts)
    {
        string cleanRoot = root.Trim().Trim('/');
        string[] cleanChildren = childParts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim())
            .ToArray();
        return cleanChildren.Length == 0
            ? cleanRoot
            : $"{cleanRoot}/{string.Join("/", cleanChildren)}";
    }

    private static List<string> SplitOutlookPath(string path) =>
        path.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

    private static bool StartsWith(
        IReadOnlyList<string> folderParts,
        IReadOnlyList<string> defaultParts)
    {
        for (var index = 0; index < defaultParts.Count; index++)
        {
            if (!folderParts[index].Equals(
                defaultParts[index],
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
