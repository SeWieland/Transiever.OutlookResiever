using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli.UnitTest;

[Collection("Environment")]
public sealed class ConfigurationProviderTests
{
    [Fact]
    public void Provider_PrefersOutlookVariablesOverSieveRulerFallback()
    {
        Set("SIEVERULER", "HOST", "fallback.test");
        Set("SIEVERULER", "USERNAME", "fallback");
        Set("SIEVERULER", "PASSWORD", "fallback-password");
        Set("OUTLOOKRESIEVER", "HOST", "outlook.test");
        Set("OUTLOOKRESIEVER", "USERNAME", "outlook");
        Set("OUTLOOKRESIEVER", "PASSWORD", "outlook-password");
        try
        {
            SieveServerConfiguration configuration =
                new EnvironmentSieveServerConfigurationProvider()
                    .GetConfiguration();

            Assert.Equal("outlook.test", configuration.Host);
            Assert.Equal("outlook", configuration.UserName);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void FolderMappingProvider_ReadsOutlookFolderOverrides()
    {
        Environment.SetEnvironmentVariable("OUTLOOKRESIEVER_FOLDER_JUNK", "Junk");
        try
        {
            OutlookFolderMappingOptions options =
                OutlookFolderMappingOptionsProvider.GetOptions();

            Assert.Equal("Junk", options.Junk);
            Assert.Equal("INBOX", options.Inbox);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OUTLOOKRESIEVER_FOLDER_JUNK", null);
        }
    }

    private static void Set(string prefix, string suffix, string value) =>
        Environment.SetEnvironmentVariable(
            $"{prefix}_SIEVE_{suffix}",
            value);

    private static void Clear()
    {
        foreach (string prefix in new[] { "OUTLOOKRESIEVER", "SIEVERULER" })
        {
            foreach (string suffix in new[] { "HOST", "USERNAME", "PASSWORD" })
            {
                Environment.SetEnvironmentVariable(
                    $"{prefix}_SIEVE_{suffix}",
                    null);
            }
        }
    }
}

[CollectionDefinition("Environment", DisableParallelization = true)]
public sealed class EnvironmentCollection;
