using Transiever.SieveRuler.Models;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli.UnitTest;

public sealed class CommandLineOptionsTests
{
    [Fact]
    public void Parse_WithoutArguments_ShowsHelp()
    {
        CommandLineOptions options = CommandLineOptions.Parse([]);

        Assert.True(options.ShowHelp);
    }

    [Fact]
    public void Parse_RunCommandUsesWorkflowDefaults()
    {
        CommandLineOptions options = CommandLineOptions.Parse(["run"]);

        Assert.Equal(OutlookResieverCommand.Run, options.Command);
        Assert.Equal("rules.json", options.RulesFile);
        Assert.Equal("candidate-rules.json", options.CandidateRulesFile);
        Assert.Null(options.OptimizationMode);
        Assert.False(options.OptimizationChoiceSpecified);
        Assert.False(options.Deploy);
    }

    [Theory]
    [InlineData("--optimize-conservative", RuleOptimizationMode.Conservative)]
    [InlineData("--optimize-balanced", RuleOptimizationMode.Balanced)]
    [InlineData("--optimize-aggressive", RuleOptimizationMode.Aggressive)]
    [InlineData("-o", RuleOptimizationMode.Conservative)]
    [InlineData("-oo", RuleOptimizationMode.Balanced)]
    [InlineData("-ooo", RuleOptimizationMode.Aggressive)]
    [InlineData("-ooooo", RuleOptimizationMode.Aggressive)]
    public void Parse_SelectsOptimizationMode(
        string option,
        RuleOptimizationMode expectedMode)
    {
        CommandLineOptions options = CommandLineOptions.Parse(["run", option]);

        Assert.Equal(expectedMode, options.OptimizationMode);
        Assert.True(options.OptimizationChoiceSpecified);
    }

    [Fact]
    public void Parse_ReadsOptimizationModeAfterOptimizeOption()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            ["run", "--optimize", "balanced"]);

        Assert.Equal(RuleOptimizationMode.Balanced, options.OptimizationMode);
        Assert.True(options.OptimizationChoiceSpecified);
    }

    [Fact]
    public void Parse_RejectsUnknownCommands()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["unknown"]));

        Assert.Contains("Unknown command", exception.Message);
    }

    [Fact]
    public void Parse_RejectsAllCommand()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["all"]));

        Assert.Contains("Unknown command", exception.Message);
    }

    [Fact]
    public void Parse_NoOptimizeSkipsRunOptimizationPrompt()
    {
        CommandLineOptions options = CommandLineOptions.Parse(["run", "--no-optimize"]);

        Assert.Null(options.OptimizationMode);
        Assert.True(options.OptimizationChoiceSpecified);
    }

    [Fact]
    public void Parse_RunReadsPreviewOwnershipAndArtifactOptions()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            [
                "run",
                "--adopt-compatible",
                "--candidate", "candidate.sieve",
                "--candidate-rules", "candidate-rules.json",
                "--server-snapshot", "server.sieve",
                "--script-name", "Open-Xchange",
                "--plan", "plan.json"
            ]);

        Assert.Equal(OutlookResieverCommand.Run, options.Command);
        Assert.True(options.AdoptCompatible);
        Assert.Equal("candidate.sieve", options.CandidateFile);
        Assert.Equal("candidate-rules.json", options.CandidateRulesFile);
        Assert.Equal("server.sieve", options.ServerSnapshotFile);
        Assert.Equal("Open-Xchange", options.ScriptName);
        Assert.Equal("plan.json", options.PlanFile);
    }

    [Fact]
    public void Parse_RejectsActivateAlias()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["run", "--activate"]));

        Assert.Contains("Unknown option", exception.Message);
    }

    [Theory]
    [InlineData("--sieve")]
    [InlineData("--output")]
    [InlineData("--force")]
    public void Parse_RejectsRemovedGenericOptions(string option)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["run", option, "value"]));

        Assert.Contains("Unknown option", exception.Message);
    }

    [Fact]
    public void Parse_RunReadsHistoryOptions()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            ["run", "--history-limit", "3", "--no-prune-history"]);

        Assert.Equal(OutlookResieverCommand.Run, options.Command);
        Assert.Equal(3, options.HistoryLimit);
        Assert.False(options.PruneHistory);
    }

    [Fact]
    public void Parse_ReadsSieveConnectionOptions()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            [
                "run",
                "--sieve-host", "sieve.test",
                "--sieve-port", "4191",
                "--sieve-username", "user",
                "--sieve-password", "password",
                "--sieve-security-mode", "ImplicitTls"
            ]);

        Assert.Equal("sieve.test", options.SieveHost);
        Assert.Equal(4191, options.SievePort);
        Assert.Equal("user", options.SieveUserName);
        Assert.Equal("password", options.SievePassword);
        Assert.Equal(SieveConnectionSecurity.ImplicitTls, options.SieveSecurity);
    }

    [Fact]
    public void Parse_RejectsInvalidSieveSecurityMode()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["run", "--sieve-security-mode", "plain"]));

        Assert.Contains("Unknown Sieve security mode", exception.Message);
    }

    [Theory]
    [InlineData("inspect")]
    [InlineData("optimize")]
    [InlineData("generate")]
    [InlineData("preview")]
    [InlineData("deploy")]
    [InlineData("rollback")]
    public void Parse_RejectsGenericSieveRulerCommands(string command)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse([command]));

        Assert.Contains("Unknown command", exception.Message);
    }
}
