using Transiever.OutlookResiever.Services;

namespace Transiever.OutlookResiever.UnitTest;

public sealed class OutlookFolderNormalizerTests
{
    [Theory]
    [InlineData(@"\\Mailbox\Posteingang\Drucker", "INBOX/Drucker")]
    [InlineData(@"\\Mailbox\Inbox\Drucker", "INBOX/Drucker")]
    [InlineData(@"\\Mailbox\Entwürfe\Privat", "Drafts/Privat")]
    [InlineData(@"\\Mailbox\Gesendete Objekte\Privat", "Sent/Privat")]
    [InlineData(@"\\Mailbox\Papierkorb\Privat", "Trash/Privat")]
    [InlineData(@"\\Mailbox\Junk-E-Mail\Privat", "Spam/Privat")]
    [InlineData(@"\\Mailbox\Archiv\Privat", "Archive/Privat")]
    public void Normalize_MapsGermanAndEnglishDefaultRoots(
        string outlookPath,
        string expected)
    {
        var normalizer = new OutlookFolderNormalizer();

        Assert.Equal(expected, normalizer.Normalize(outlookPath));
    }

    [Fact]
    public void Normalize_UsesDefaultFolderPathBeforeAliasFallback()
    {
        var normalizer = new OutlookFolderNormalizer(
            new OutlookFolderMappingOptions
            {
                Inbox = "CUSTOM-INBOX"
            });

        string result = normalizer.Normalize(
            new OutlookFolderPathContext
            {
                FolderPath = @"\\Mailbox\Posteingang\Drucker",
                DefaultFolderPaths =
                    new Dictionary<OutlookDefaultFolderRole, string>
                    {
                        [OutlookDefaultFolderRole.Inbox] =
                            @"\\Mailbox\Posteingang"
                    }
            });

        Assert.Equal("CUSTOM-INBOX/Drucker", result);
    }

    [Fact]
    public void Normalize_PreservesUnknownRootAfterMailboxRootRemoval()
    {
        var normalizer = new OutlookFolderNormalizer();

        Assert.Equal(
            "Kunden/Drucker",
            normalizer.Normalize(@"\\Mailbox\Kunden\Drucker"));
    }

    [Fact]
    public void Normalize_AppliesConfiguredRoleNames()
    {
        var normalizer = new OutlookFolderNormalizer(
            new OutlookFolderMappingOptions
            {
                Junk = "Junk"
            });

        Assert.Equal(
            "Junk/Anbieter",
            normalizer.Normalize(@"\\Mailbox\Spam\Anbieter"));
    }

    [Fact]
    public void Normalize_MapsRelativeLocalizedPath()
    {
        var normalizer = new OutlookFolderNormalizer();

        Assert.Equal("INBOX/Drucker", normalizer.Normalize("Posteingang/Drucker"));
    }
}
