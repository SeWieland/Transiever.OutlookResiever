using Transiever.SieveRuler.Models;

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
        Assert.Equal("rules.sieve", options.SieveFile);
        Assert.Equal("candidate-rules.json", options.CandidateRulesFile);
        Assert.Null(options.OptimizationMode);
        Assert.False(options.OptimizationChoiceSpecified);
        Assert.False(options.Deploy);
    }

    [Fact]
    public void Parse_AllowsOptimizationDuringGeneration()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            ["generate", "--optimize", "--output", "prepared.json"]);

        Assert.Equal(OutlookResieverCommand.Generate, options.Command);
        Assert.Equal(RuleOptimizationMode.Conservative, options.OptimizationMode);
        Assert.True(options.OptimizationChoiceSpecified);
        Assert.Equal("prepared.json", options.OutputFile);
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
            ["generate", "--optimize", "balanced"]);

        Assert.Equal(RuleOptimizationMode.Balanced, options.OptimizationMode);
        Assert.True(options.OptimizationChoiceSpecified);
    }

    [Fact]
    public void Parse_OptimizeCommandDefaultsToConservative()
    {
        CommandLineOptions options = CommandLineOptions.Parse(["optimize"]);

        Assert.Equal(RuleOptimizationMode.Conservative, options.OptimizationMode);
        Assert.True(options.OptimizationChoiceSpecified);
    }

    [Fact]
    public void Parse_OptimizeCommandAcceptsPositionalMode()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            ["optimize", "balanced"]);

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
    public void Parse_ReadsPreviewOwnershipAndArtifactOptions()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            [
                "preview",
                "--adopt-compatible",
                "--candidate", "candidate.sieve",
                "--candidate-rules", "candidate-rules.json",
                "--server-snapshot", "server.sieve",
                "--script-name", "Open-Xchange",
                "--plan", "plan.json"
            ]);

        Assert.Equal(OutlookResieverCommand.Preview, options.Command);
        Assert.True(options.AdoptCompatible);
        Assert.Equal("candidate.sieve", options.CandidateFile);
        Assert.Equal("candidate-rules.json", options.CandidateRulesFile);
        Assert.Equal("server.sieve", options.ServerSnapshotFile);
        Assert.Equal("Open-Xchange", options.ScriptName);
        Assert.Equal("plan.json", options.PlanFile);
    }

    [Fact]
    public void Parse_RunActivationImpliesDeploy()
    {
        CommandLineOptions options =
            CommandLineOptions.Parse(["run", "--activate"]);

        Assert.Equal(OutlookResieverCommand.Run, options.Command);
        Assert.True(options.Deploy);
        Assert.True(options.Activate);
    }

    [Fact]
    public void Parse_DeployActivationIsExplicit()
    {
        CommandLineOptions options =
            CommandLineOptions.Parse(["deploy", "--plan", "plan.json", "--activate"]);

        Assert.Equal(OutlookResieverCommand.Deploy, options.Command);
        Assert.True(options.Deploy);
        Assert.True(options.Activate);
    }

    [Fact]
    public void Parse_DeployReadsHistoryOptions()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            ["deploy", "--history-limit", "3", "--no-prune-history"]);

        Assert.Equal(OutlookResieverCommand.Deploy, options.Command);
        Assert.Equal(3, options.HistoryLimit);
        Assert.False(options.PruneHistory);
    }

    [Fact]
    public void Parse_RunReadsHistoryLimit()
    {
        CommandLineOptions options = CommandLineOptions.Parse(
            ["run", "--history-limit", "3"]);

        Assert.Equal(OutlookResieverCommand.Run, options.Command);
        Assert.Equal(3, options.HistoryLimit);
        Assert.True(options.PruneHistory);
    }

    [Fact]
    public void Parse_RollbackReadsPlanAndForce()
    {
        CommandLineOptions options =
            CommandLineOptions.Parse(["rollback", "--plan", "plan.json", "--force"]);

        Assert.Equal(OutlookResieverCommand.Rollback, options.Command);
        Assert.Equal("plan.json", options.PlanFile);
        Assert.True(options.Force);
    }
}
