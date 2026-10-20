using Transiever.OutlookResiever.Services;
using Transiever.Common.Accounts;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli.UnitTest;

[Collection("Environment")]
public sealed class ConfigurationProviderTests
{
    [Fact]
    public void StandardInputPassword_RejectsTerminalWithoutReading()
    {
        TextReader originalInput = Console.In;
        using var input = new StringReader("terminal-password\n");
        Console.SetIn(input);
        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EnvironmentSieveServerConfigurationProvider.ReadStandardInputPassword(inputIsRedirected: false));

            Assert.Contains("redirected standard input", exception.Message);
            Assert.Equal("terminal-password", input.ReadLine());
        }
        finally
        {
            Console.SetIn(originalInput);
        }
    }

    [Fact]
    public void Provider_ReadsTransieverEnvironment()
    {
        Clear();
        Set("HOST", "sieve.test");
        Set("USERNAME", "user");
        Set("PASSWORD", "password");
        TextReader originalInput = Console.In;
        Console.SetIn(new StringReader("stdin-sentinel\n"));
        try
        {
            SieveServerConfiguration configuration =
                new EnvironmentSieveServerConfigurationProvider()
                    .GetConfiguration(CommandLineOptions.Parse(["run"]));

            Assert.Equal("sieve.test", configuration.Host);
            Assert.Equal("user", configuration.UserName);
            Assert.Equal("password", configuration.Password);
            Assert.Equal("stdin-sentinel", Console.In.ReadLine());
        }
        finally
        {
            Console.SetIn(originalInput);
            Clear();
        }
    }

    [Fact]
    public void Provider_PrefersCommandLineOptionsOverEnvironment()
    {
        Clear();
        Set("HOST", "env.test");
        Set("USERNAME", "env-user");
        Set("PASSWORD", "env-password");
        try
        {
            CommandLineOptions options = CommandLineOptions.Parse(
                [
                    "run",
                    "--sieve-host", "cli.test",
                    "--sieve-port", "4191",
                    "--sieve-username", "cli-user",
                    "--sieve-security-mode", "ImplicitTls"
                ]);

            SieveServerConfiguration configuration =
                new EnvironmentSieveServerConfigurationProvider()
                    .GetConfiguration(options);

            Assert.Equal("cli.test", configuration.Host);
            Assert.Equal(4191, configuration.Port);
            Assert.Equal("cli-user", configuration.UserName);
            Assert.Equal("env-password", configuration.Password);
            Assert.Equal(SieveConnectionSecurity.ImplicitTls, configuration.Security);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_UsesExplicitStandardInputPasswordBeforeEnvironment()
    {
        Clear();
        Set("HOST", "sieve.test");
        Set("USERNAME", "user");
        Set("PASSWORD", "environment-password");
        TextReader originalInput = Console.In;
        Console.SetIn(new StringReader("stdin-password\nunused\n"));
        try
        {
            SieveServerConfiguration configuration = new EnvironmentSieveServerConfigurationProvider()
                .GetConfiguration(CommandLineOptions.Parse(["run", "--sieve-password-stdin"]));

            Assert.Equal("stdin-password", configuration.Password);
            Assert.Equal("unused", Console.In.ReadLine());
        }
        finally
        {
            Console.SetIn(originalInput);
            Clear();
        }
    }

    [Fact]
    public void Provider_UsesExplicitAccountBeforeEnvironmentAccount()
    {
        Clear();
        AccountReference explicitReference = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        AccountReference environmentReference = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        SetAccount(environmentReference.ToString());
        var profileStore = new FakeProfileStore(CreateProfile(explicitReference, "explicit.test", "explicit-user"));
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "stored-password"));

        try
        {
            SieveServerConfiguration configuration = new EnvironmentSieveServerConfigurationProvider(
                profileStore,
                credentialStore,
                () => true)
                .GetConfiguration(CommandLineOptions.Parse(["run", "--account", explicitReference.ToString()]));

            Assert.Equal("explicit.test", configuration.Host);
            Assert.Equal("explicit-user", configuration.UserName);
            Assert.Equal("stored-password", configuration.Password);
            Assert.Equal(explicitReference, profileStore.LastReference);
            Assert.Equal(explicitReference, credentialStore.LastReference);
            Assert.Equal(CredentialKind.Password, credentialStore.LastKind);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_UsesEnvironmentAccountWhenNoExplicitSelectionExists()
    {
        Clear();
        AccountReference reference = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
        SetAccount(reference.ToString());
        var profileStore = new FakeProfileStore(CreateProfile(reference, "selected.test", "selected-user"));
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "stored-password"));

        try
        {
            SieveServerConfiguration configuration = new EnvironmentSieveServerConfigurationProvider(
                profileStore,
                credentialStore,
                () => true)
                .GetConfiguration(CommandLineOptions.Parse(["run"]));

            Assert.Equal("selected.test", configuration.Host);
            Assert.Equal("selected-user", configuration.UserName);
            Assert.Equal("stored-password", configuration.Password);
            Assert.Equal(reference, profileStore.LastReference);
            Assert.Equal(reference, credentialStore.LastReference);
            Assert.Equal(CredentialKind.Password, credentialStore.LastKind);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_PrefersEnvironmentValuesAndPasswordOverSelectedAccount()
    {
        Clear();
        AccountReference reference = new(Guid.Parse("00000000-0000-0000-0000-000000000004"));
        SetAccount(reference.ToString());
        Set("HOST", "environment.test");
        Set("PORT", "4191");
        Set("USERNAME", "environment-user");
        Set("PASSWORD", "environment-password");
        Set("SECURITY_MODE", "ImplicitTls");
        var profileStore = new FakeProfileStore(CreateProfile(reference, "profile.test", "profile-user"));
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "stored-password"));

        try
        {
            SieveServerConfiguration configuration = new EnvironmentSieveServerConfigurationProvider(
                profileStore,
                credentialStore,
                () => true)
                .GetConfiguration(CommandLineOptions.Parse(["run"]));

            Assert.Equal("environment.test", configuration.Host);
            Assert.Equal(4191, configuration.Port);
            Assert.Equal("environment-user", configuration.UserName);
            Assert.Equal("environment-password", configuration.Password);
            Assert.Equal(SieveConnectionSecurity.ImplicitTls, configuration.Security);
            Assert.Equal(0, credentialStore.GetCallCount);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_UsesProfileDefaultsAndCommandLineOverrides()
    {
        Clear();
        AccountReference reference = new(Guid.Parse("00000000-0000-0000-0000-000000000007"));
        var profile = CreateProfile(reference, "profile.test", "profile-user") with
        {
            Port = 4191,
            Security = TransportSecurity.ImplicitTls
        };
        var profileStore = new FakeProfileStore(profile);
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "profile-password"));
        try
        {
            SieveServerConfiguration configuration = new EnvironmentSieveServerConfigurationProvider(
                profileStore,
                credentialStore,
                () => true)
                .GetConfiguration(CommandLineOptions.Parse([
                    "run", "--account", reference.ToString(),
                    "--sieve-host", "cli.test", "--sieve-port", "4192",
                    "--sieve-username", "cli-user", "--sieve-security-mode", "StartTlsRequired"
                ]));

            Assert.Equal("cli.test", configuration.Host);
            Assert.Equal(4192, configuration.Port);
            Assert.Equal("cli-user", configuration.UserName);
            Assert.Equal(SieveConnectionSecurity.StartTlsRequired, configuration.Security);
            Assert.Equal("profile-password", configuration.Password);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_UsesSelectedProfileEndpointDefaults()
    {
        Clear();
        AccountReference reference = new(Guid.Parse("00000000-0000-0000-0000-000000000008"));
        var profile = CreateProfile(reference, "profile.test", "profile-user") with
        {
            Port = 4191,
            Security = TransportSecurity.ImplicitTls
        };
        var profileStore = new FakeProfileStore(profile);
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "profile-password"));
        try
        {
            SieveServerConfiguration configuration = new EnvironmentSieveServerConfigurationProvider(
                profileStore,
                credentialStore,
                () => true)
                .GetConfiguration(CommandLineOptions.Parse(["run", "--account", reference.ToString()]));

            Assert.Equal("profile.test", configuration.Host);
            Assert.Equal(4191, configuration.Port);
            Assert.Equal("profile-user", configuration.UserName);
            Assert.Equal(SieveConnectionSecurity.ImplicitTls, configuration.Security);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_RejectsPlaintextProfileBeforeFetchingCredential()
    {
        Clear();
        AccountReference reference = new(Guid.Parse("00000000-0000-0000-0000-000000000009"));
        var profile = CreateProfile(reference, "profile.test", "profile-user") with
        {
            Security = TransportSecurity.PlainText
        };
        var profileStore = new FakeProfileStore(profile);
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "secret-sentinel"));
        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EnvironmentSieveServerConfigurationProvider(profileStore, credentialStore, () => true)
                    .GetConfiguration(CommandLineOptions.Parse(["run", "--account", reference.ToString()])));

            Assert.Contains("plaintext", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, credentialStore.GetCallCount);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_UsesEnvironmentConfigurationWithoutReadingAccountStoresWhenUnselected()
    {
        Clear();
        SetAccount(" ");
        Set("HOST", "sieve.test");
        Set("USERNAME", "user");
        Set("PASSWORD", "environment-password");
        var profileStore = new FakeProfileStore();
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "stored-password"));

        try
        {
            SieveServerConfiguration configuration = new EnvironmentSieveServerConfigurationProvider(
                profileStore,
                credentialStore,
                () => true)
                .GetConfiguration(CommandLineOptions.Parse(["run"]));

            Assert.Equal("sieve.test", configuration.Host);
            Assert.Equal("environment-password", configuration.Password);
            Assert.Equal(0, profileStore.GetCallCount);
            Assert.Equal(0, credentialStore.GetCallCount);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_RejectsInvalidExplicitAccountWithoutFallingBackToEnvironment()
    {
        Clear();
        AccountReference environmentReference = new(Guid.Parse("00000000-0000-0000-0000-000000000010"));
        SetAccount(environmentReference.ToString());
        var profileStore = new FakeProfileStore(CreateProfile(environmentReference, "environment.test", "env-user"));
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "secret-sentinel"));
        try
        {
            Assert.Throws<ArgumentException>(
                () => new EnvironmentSieveServerConfigurationProvider(profileStore, credentialStore, () => true)
                    .GetConfiguration(CommandLineOptions.Parse(["run", "--account", "invalid-account"])));

            Assert.Equal(0, profileStore.GetCallCount);
            Assert.Equal(0, credentialStore.GetCallCount);
        }
        finally
        {
            Clear();
        }
    }

    [Theory]
    [InlineData(CredentialStatus.Missing)]
    [InlineData(CredentialStatus.Expired)]
    [InlineData(CredentialStatus.Rejected)]
    [InlineData(CredentialStatus.Unavailable)]
    public void Provider_DoesNotUseUnavailableOrStaleStoredPasswordHeadlessly(CredentialStatus status)
    {
        Clear();
        AccountReference reference = new(Guid.Parse("00000000-0000-0000-0000-000000000005"));
        var profileStore = new FakeProfileStore(CreateProfile(reference, "selected.test", "selected-user"));
        var credentialStore = new FakeCredentialStore(new CredentialResult(status, "stale-secret-sentinel"));

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EnvironmentSieveServerConfigurationProvider(profileStore, credentialStore, () => true)
                    .GetConfiguration(CommandLineOptions.Parse(["run", "--account", reference.ToString()])));

            Assert.Contains("input is redirected", exception.Message);
            Assert.DoesNotContain("stale-secret-sentinel", exception.Message);
            Assert.Equal(1, credentialStore.GetCallCount);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Provider_RejectsUnsupportedAuthenticationBeforeFetchingCredential()
    {
        Clear();
        AccountReference reference = new(Guid.Parse("00000000-0000-0000-0000-000000000006"));
        var profile = CreateProfile(reference, "selected.test", "selected-user") with
        {
            PreferredAuthentication = "OAUTHBEARER"
        };
        var profileStore = new FakeProfileStore(profile);
        var credentialStore = new FakeCredentialStore(new CredentialResult(CredentialStatus.Available, "secret-sentinel"));

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EnvironmentSieveServerConfigurationProvider(profileStore, credentialStore, () => true)
                    .GetConfiguration(CommandLineOptions.Parse(["run", "--account", reference.ToString()])));

            Assert.Contains("authentication", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-sentinel", exception.Message);
            Assert.Equal(0, credentialStore.GetCallCount);
        }
        finally
        {
            Clear();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Provider_RejectsMissingExplicitStandardInputPasswordEvenWithEnvironmentPassword(bool emptyLine)
    {
        Clear();
        Set("HOST", "sieve.test");
        Set("USERNAME", "user");
        Set("PASSWORD", "environment-password");
        TextReader originalInput = Console.In;
        Console.SetIn(new StringReader(emptyLine ? "\n" : ""));
        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new EnvironmentSieveServerConfigurationProvider()
                .GetConfiguration(CommandLineOptions.Parse(["run", "--sieve-password-stdin"])));

            Assert.Equal("Standard input did not contain a Sieve password.", exception.Message);
        }
        finally
        {
            Console.SetIn(originalInput);
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

    private static void Set(string suffix, string value) =>
        Environment.SetEnvironmentVariable(
            $"TRANSIEVER_SIEVE_{suffix}",
            value);

    private static void SetAccount(string value) =>
        Environment.SetEnvironmentVariable("TRANSIEVER_ACCOUNT", value);

    private static AccountProfile CreateProfile(
        AccountReference reference,
        string host,
        string username) => new()
        {
            Reference = reference,
            DisplayName = "test account",
            Provider = "sieve",
            AccountId = username,
            Host = host,
            Username = username,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch
        };

    private sealed class FakeProfileStore(params AccountProfile[] profiles) : IAccountProfileStore
    {
        private readonly Dictionary<AccountReference, AccountProfile> _profiles =
            profiles.ToDictionary(profile => profile.Reference);

        public int GetCallCount { get; private set; }
        public AccountReference? LastReference { get; private set; }

        public AccountProfile Create(AccountProfile profile) => throw new NotSupportedException();

        public AccountProfile? Get(AccountReference reference)
        {
            GetCallCount++;
            LastReference = reference;
            return _profiles.GetValueOrDefault(reference);
        }

        public IReadOnlyList<AccountProfile> List() => throw new NotSupportedException();

        public AccountProfile Update(AccountProfile profile) => throw new NotSupportedException();

        public void Delete(AccountReference reference) => throw new NotSupportedException();
    }

    private sealed class FakeCredentialStore(CredentialResult result) : ICredentialStore
    {
        public int GetCallCount { get; private set; }
        public AccountReference? LastReference { get; private set; }
        public CredentialKind? LastKind { get; private set; }

        public CredentialResult Get(AccountReference reference, CredentialKind kind)
        {
            GetCallCount++;
            LastReference = reference;
            LastKind = kind;
            return result;
        }

        public CredentialStatus Set(AccountReference reference, CredentialKind kind, string secret, DateTimeOffset? expiresAt = null) =>
            throw new NotSupportedException();

        public CredentialStatus Remove(AccountReference reference, CredentialKind kind) =>
            throw new NotSupportedException();

        public CredentialStatus GetStatus(AccountReference reference, CredentialKind kind) =>
            throw new NotSupportedException();

        public CredentialStatus MarkOutcome(AccountReference reference, CredentialKind kind, CredentialStatus status) =>
            throw new NotSupportedException();
    }

    private static void Clear()
    {
        foreach (string suffix in new[]
        {
            "HOST",
            "USERNAME",
            "PASSWORD",
            "PORT",
            "SECURITY_MODE"
        })
        {
            Environment.SetEnvironmentVariable(
                $"TRANSIEVER_SIEVE_{suffix}",
                null);
        }

        Environment.SetEnvironmentVariable("TRANSIEVER_ACCOUNT", null);
    }
}

[CollectionDefinition("Environment", DisableParallelization = true)]
public sealed class EnvironmentCollection;
