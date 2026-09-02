using System.Security.Cryptography;
using Transiever.ManageSieve;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.UnitTest;

internal enum PathServerOperationKind
{
    CheckScript,
    HaveSpace,
    PutScript,
    Activate,
    DeleteScript
}

internal sealed record PathServerOperation(
    PathServerOperationKind Kind,
    string? ScriptName = null);

internal sealed class PathSieveServerConnectionFactory(PathSieveServerConnection connection)
    : ISieveServerConnectionFactory
{
    public Task<ISieveServerConnection> ConnectAsync(
        SieveServerConfiguration configuration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ISieveServerConnection>(connection);
    }
}

internal sealed class PathSieveServerConnection : ISieveServerConnection
{
    private readonly Dictionary<string, byte[]> scripts;
    private readonly List<PathServerOperation> operations = [];
    private byte[]? registeredCandidate;
    private string registeredCandidateScriptName = "path-candidate";

    public PathSieveServerConnection(string activeScriptName, byte[] activeContent)
    {
        ActiveScriptName = activeScriptName;
        scripts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [activeScriptName] = activeContent.ToArray()
        };
    }

    public PathSieveServerConnection(
        string activeScriptName,
        byte[] activeContent,
        string candidateScriptName,
        byte[] candidateContent)
        : this(activeScriptName, activeContent)
    {
        RegisterCandidate(candidateScriptName, candidateContent);
    }

    public string ActiveScriptName { get; private set; }

    public IReadOnlyList<PathServerOperation> Operations => operations;

    public void RegisterCandidate(string scriptName, ReadOnlyMemory<byte> content)
    {
        registeredCandidateScriptName = scriptName;
        registeredCandidate = content.ToArray();
    }

    public void RegisterCandidate(ReadOnlyMemory<byte> content) =>
        RegisterCandidate(registeredCandidateScriptName, content);

    public Task CheckScriptAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Record(PathServerOperationKind.CheckScript);
        if (registeredCandidate is null || !content.Span.SequenceEqual(registeredCandidate))
            throw new InvalidOperationException("The Sieve candidate was not registered with the PATH server.");
        return Task.CompletedTask;
    }

    public Task<bool> HaveSpaceAsync(
        string scriptName,
        long contentLength,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Record(PathServerOperationKind.HaveSpace, scriptName);
        return Task.FromResult(true);
    }

    public Task<byte[]> GetScriptAsync(string scriptName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(scripts[scriptName].ToArray());
    }

    public Task PutScriptAsync(
        string scriptName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Record(PathServerOperationKind.PutScript, scriptName);
        scripts[scriptName] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task ActivateAsync(string? scriptName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Record(PathServerOperationKind.Activate, scriptName);
        if (scriptName is null || !scripts.ContainsKey(scriptName))
            throw new KeyNotFoundException($"Sieve script '{scriptName}' was not found.");
        ActiveScriptName = scriptName;
        return Task.CompletedTask;
    }

    public Task DeleteScriptAsync(string scriptName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Record(PathServerOperationKind.DeleteScript, scriptName);
        scripts.Remove(scriptName);
        if (string.Equals(ActiveScriptName, scriptName, StringComparison.Ordinal))
            ActiveScriptName = string.Empty;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<RemoteSieveState> ReadStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] activeContent = scripts.TryGetValue(ActiveScriptName, out byte[]? content)
            ? content.ToArray()
            : [];
        return Task.FromResult(new RemoteSieveState
        {
            ActiveScriptName = ActiveScriptName,
            ActiveContent = activeContent,
            ActiveContentSha256 = Convert.ToHexString(SHA256.HashData(activeContent)),
            Scripts = scripts
                .Select(pair => new ManageSieveScriptInfo(pair.Key, pair.Key == ActiveScriptName))
                .ToArray(),
            Capabilities = CreateCapabilities()
        });
    }

    private void Record(PathServerOperationKind kind, string? scriptName = null) =>
        operations.Add(new(kind, scriptName));

    private static ManageSieveCapabilities CreateCapabilities() => new()
    {
        Implementation = "Transiever PATH test server",
        ProtocolVersion = "1.0",
        SieveExtensions = new HashSet<string>(["fileinto", "imap4flags"], StringComparer.OrdinalIgnoreCase)
    };
}
