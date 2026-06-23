using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli;

public interface ISieveServerConfigurationProvider
{
    SieveServerConfiguration GetConfiguration();
}

public sealed class EnvironmentSieveServerConfigurationProvider
    : ISieveServerConfigurationProvider
{
    public SieveServerConfiguration GetConfiguration()
    {
        string host = Required("HOST");
        string userName = Required("USERNAME");
        string password = Read("PASSWORD") ?? ReadPassword();
        int port = int.TryParse(Read("PORT"), out int configuredPort)
            ? configuredPort
            : SieveServerConfiguration.DefaultPort;
        string? configuredSecurity = Read("SECURITY_MODE");
        if (configuredSecurity?.Equals(
            "PlainText",
            StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException(
                "Transiever.OutlookResiever does not send credentials over a plaintext ManageSieve connection.");
        }

        SieveConnectionSecurity security = Enum.TryParse(
            configuredSecurity,
            ignoreCase: true,
            out SieveConnectionSecurity configuredMode)
            ? configuredMode
            : SieveConnectionSecurity.StartTlsRequired;
        return new SieveServerConfiguration(
            host,
            port,
            userName,
            password,
            security);
    }

    private static string Required(string suffix) =>
        Read(suffix) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Environment variable OUTLOOKRESIEVER_SIEVE_{suffix} or SIEVERULER_SIEVE_{suffix} is required.");

    private static string? Read(string suffix) =>
        Environment.GetEnvironmentVariable($"OUTLOOKRESIEVER_SIEVE_{suffix}")
        ?? Environment.GetEnvironmentVariable($"SIEVERULER_SIEVE_{suffix}");

    private static string ReadPassword()
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException(
                "OUTLOOKRESIEVER_SIEVE_PASSWORD or SIEVERULER_SIEVE_PASSWORD is required when input is redirected.");
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
