namespace Transiever.OutlookResiever.Cli.UnitTest;

public sealed class ProgramWorkflowTests
{
    [Fact]
    public async Task Main_WithoutArguments_ReturnsHelpWithoutExternalAccess()
    {
        Assert.Equal(0, await Program.Main([]));
    }

    [Fact]
    public async Task Generate_WithOptimization_WritesReviewableIntermediateAndSieveFiles()
    {
        string directory = CreateDirectory();
        string rulesFile = Path.Combine(directory, "rules.json");
        string optimizedFile = Path.Combine(directory, "rules.optimized.json");
        string sieveFile = Path.Combine(directory, "rules.sieve");

        try
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            await File.WriteAllTextAsync(
                rulesFile,
                """
                [
                  {
                    "name": "First",
                    "targetFolder": "INBOX/Development",
                    "conditionMode": "All",
                    "conditions": [
                      {
                        "type": "SenderContains",
                        "values": [ "first@example.com" ]
                      }
                    ]
                  },
                  {
                    "name": "Second",
                    "targetFolder": "INBOX/Development",
                    "conditionMode": "All",
                    "conditions": [
                      {
                        "type": "SenderContains",
                        "values": [ "second@example.com" ]
                      }
                    ]
                  }
                ]
                """,
                cancellationToken);

            int exitCode = await Program.Main(
                [
                    "generate",
                    "--rules", rulesFile,
                    "--output", optimizedFile,
                    "--sieve", sieveFile,
                    "--optimize"
                ]);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(optimizedFile));
            Assert.True(File.Exists(sieveFile));

            string optimizedJson = await File.ReadAllTextAsync(
                optimizedFile,
                cancellationToken);
            string sieve = await File.ReadAllTextAsync(
                sieveFile,
                cancellationToken);

            Assert.Contains("first@example.com", optimizedJson);
            Assert.Contains("second@example.com", optimizedJson);
            Assert.Contains(
                "[\"first@example.com\", \"second@example.com\"]",
                sieve);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Generate_WithAggressiveOptimization_UsesCollapsedParentDomain()
    {
        string directory = CreateDirectory();
        string rulesFile = Path.Combine(directory, "rules.json");
        string optimizedFile = Path.Combine(directory, "rules.optimized.json");
        string sieveFile = Path.Combine(directory, "rules.sieve");

        try
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            await File.WriteAllTextAsync(
                rulesFile,
                """
                [
                  {
                    "name": "info@xyz.de",
                    "targetFolder": "Posteingang/XYZ",
                    "conditionMode": "All",
                    "conditions": [
                      {
                        "type": "SenderContains",
                        "values": [ "info@xyz.de" ]
                      }
                    ]
                  },
                  {
                    "name": "XYZ",
                    "targetFolder": "Posteingang/XYZ",
                    "conditionMode": "All",
                    "conditions": [
                      {
                        "type": "SenderContains",
                        "values": [ "noreply@info.xyz.de" ]
                      }
                    ]
                  }
                ]
                """,
                cancellationToken);

            int exitCode = await Program.Main(
                [
                    "generate",
                    "--rules", rulesFile,
                    "--output", optimizedFile,
                    "--sieve", sieveFile,
                    "--optimize", "aggressive"
                ]);

            Assert.Equal(0, exitCode);

            string optimizedJson = await File.ReadAllTextAsync(
                optimizedFile,
                cancellationToken);
            string sieve = await File.ReadAllTextAsync(
                sieveFile,
                cancellationToken);

            Assert.Contains("\"xyz.de\"", optimizedJson);
            Assert.DoesNotContain("info.xyz.de", optimizedJson);
            Assert.Contains("address :contains \"from\" \"xyz.de\"", sieve);
            Assert.DoesNotContain("info@xyz.de", sieve);
            Assert.DoesNotContain("info.xyz.de", sieve);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"OutlookResiever-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
