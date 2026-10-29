using Transiever.Common.Accounts;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli;

public interface ISieveServerConfigurationProvider
{
    SieveServerConfiguration GetConfiguration(CommandLineOptions options);
}

public sealed class EnvironmentSieveServerConfigurationProvider
    : ISieveServerConfigurationProvider
{
    private readonly IAccountProfileStore? _profileStore;
    private readonly ICredentialStore? _credentialStore;
    private readonly Func<bool> _isInputRedirected;

    public EnvironmentSieveServerConfigurationProvider(
        IAccountProfileStore? profileStore = null,
        ICredentialStore? credentialStore = null,
        Func<bool>? isInputRedirected = null)
    {
        _profileStore = profileStore;
        _credentialStore = credentialStore;
        _isInputRedirected = isInputRedirected ?? (() => Console.IsInputRedirected);
    }

    public SieveServerConfiguration GetConfiguration(CommandLineOptions options)
    {
        AccountProfile? profile = GetSelectedProfile(options);
        string host = options.SieveHost ?? ReadNonEmpty("HOST") ?? profile?.Host
            ?? Required("HOST");
        string userName = options.SieveUserName ?? ReadNonEmpty("USERNAME") ?? profile?.Username
            ?? Required("USERNAME");
        int port = options.SievePort
            ?? (int.TryParse(Read("PORT"), out int configuredPort)
                ? configuredPort
                : profile?.Port ?? SieveServerConfiguration.DefaultPort);
        SieveConnectionSecurity security = ResolveSecurity(options.SieveSecurity, profile);
        string password = ResolvePassword(options, profile);
        return new SieveServerConfiguration(host, port, userName, password, security);
    }

    private static SieveConnectionSecurity ResolveSecurity(
        SieveConnectionSecurity? commandLineSecurity,
        AccountProfile? profile)
    {
        if (commandLineSecurity is { } explicitSecurity)
        {
            return explicitSecurity;
        }

        string? configuredSecurity = Read("SECURITY_MODE");
        if (configuredSecurity?.Equals("PlainText", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException(
                "Transiever.OutlookResiever does not send credentials over a plaintext ManageSieve connection.");
        }

        if (Enum.TryParse(configuredSecurity, ignoreCase: true, out SieveConnectionSecurity environmentSecurity)
            && Enum.IsDefined(environmentSecurity))
        {
            return environmentSecurity;
        }

        if (profile?.Security == TransportSecurity.PlainText)
        {
            throw new InvalidOperationException(
                "Transiever.OutlookResiever does not send credentials over a plaintext ManageSieve connection.");
        }

        return profile?.Security == TransportSecurity.ImplicitTls
            ? SieveConnectionSecurity.ImplicitTls
            : SieveConnectionSecurity.StartTlsRequired;
    }

    private string ResolvePassword(CommandLineOptions options, AccountProfile? profile)
    {
        if (options.SievePasswordStdin)
        {
            return ReadStandardInputPassword(_isInputRedirected());
        }

        string? password = Read("PASSWORD");
        if (password is null && profile is not null)
        {
            CredentialResult credential = (_credentialStore ?? CredentialStores.CreateDefault())
                .Get(profile.Reference, CredentialKind.Password);
            if (credential.Status == CredentialStatus.Available)
            {
                password = credential.Secret;
            }
        }

        return password ?? ReadPassword(_isInputRedirected());
    }

    private AccountProfile? GetSelectedProfile(CommandLineOptions options)
    {
        string? selectedAccount = options.Account ?? ReadAccountEnvironment();
        if (selectedAccount is null)
        {
            return null;
        }

        AccountReference reference = AccountReference.Parse(selectedAccount);
        AccountProfile? profile = (_profileStore ?? new JsonAccountProfileStore()).Get(reference)
            ?? throw new InvalidOperationException($"No saved account exists for {reference}.");

        if (profile.PreferredAuthentication is { Length: > 0 } authentication
            && !authentication.Equals("PLAIN", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected account's preferred authentication is not supported by OutlookResiever.");
        }

        return profile;
    }

    private static string? ReadAccountEnvironment()
    {
        string? value = Environment.GetEnvironmentVariable("TRANSIEVER_ACCOUNT");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string Required(string suffix) =>
        Read(suffix) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Environment variable TRANSIEVER_SIEVE_{suffix} is required.");

    private static string? ReadNonEmpty(string suffix)
    {
        string? value = Read(suffix);
        return value is { Length: 0 }
            ? throw new InvalidOperationException($"Environment variable TRANSIEVER_SIEVE_{suffix} must not be empty.")
            : value;
    }

    private static string? Read(string suffix) =>
        Environment.GetEnvironmentVariable($"TRANSIEVER_SIEVE_{suffix}");

    internal static string ReadStandardInputPassword(bool inputIsRedirected)
    {
        if (!inputIsRedirected)
        {
            throw new InvalidOperationException(
                "--sieve-password-stdin requires redirected standard input.");
        }

        return Console.ReadLine() is { Length: > 0 } password
            ? password
            : throw new InvalidOperationException("Standard input did not contain a Sieve password.");
    }

    private static string ReadPassword(bool inputIsRedirected)
    {
        if (inputIsRedirected)
        {
            throw new InvalidOperationException(
                "TRANSIEVER_SIEVE_PASSWORD is required when input is redirected and no usable stored password is available.");
        }

        Console.Write("ManageSieve password: ");
        var password = new System.Text.StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }

        Console.WriteLine();
        return password.ToString();
    }
}
