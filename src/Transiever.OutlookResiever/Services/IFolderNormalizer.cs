namespace Transiever.OutlookResiever.Services;

/// <summary>
/// Normalizes Outlook folder names to the target mailbox naming scheme.
/// </summary>
public interface IFolderNormalizer
{
    string Normalize(string outlookFolderPath);

    string Normalize(OutlookFolderPathContext context);
}
