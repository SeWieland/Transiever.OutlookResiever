namespace Transiever.OutlookResiever.Cli.UnitTest;

public sealed class ProgramWorkflowTests
{
    [Fact]
    public async Task Main_WithoutArguments_ReturnsHelpWithoutExternalAccess()
    {
        Assert.Equal(0, await Program.Main([]));
    }

    [Fact]
    public async Task Main_GenericSieveRulerCommand_ReturnsUsageError()
    {
        Assert.Equal(1, await Program.Main(["generate"]));
    }
}
