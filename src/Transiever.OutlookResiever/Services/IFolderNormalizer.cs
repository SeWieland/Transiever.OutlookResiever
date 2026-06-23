namespace Transiever.OutlookResiever.Services;

public interface IFolderNormalizer
{
    string Normalize(string outlookFolderPath);

    string Normalize(OutlookFolderPathContext context);
}
