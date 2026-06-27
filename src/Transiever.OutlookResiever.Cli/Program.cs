using Transiever.OutlookResiever.Application;
using Transiever.OutlookResiever.Services;
using Transiever.SieveRuler.Application;
using Transiever.SieveRuler.Services;

namespace Transiever.OutlookResiever.Cli;

public static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            ConsolePresentation.PrintHelp();
            return 1;
        }

        if (options.ShowHelp)
        {
            ConsolePresentation.PrintHelp();
            return 0;
        }

        IRuleSerializer serializer = new JsonRuleSerializer();
        IRuleOptimizer optimizer = new RuleOptimizer();
        ISieveGenerator generator = new SieveGenerator();
        ISieveImporter importer = new SieveImporter();
        IRuleReconciler reconciler = new RuleReconciler(optimizer);
        ISieveScriptComposer composer = new SieveScriptComposer(importer, generator);
        var interaction = new ConsoleSynchronizationInteraction();
        ISynchronizationInteraction synchronizationInteraction = interaction;
        ISieveSynchronizationWorkflow synchronization =
            new SieveSynchronizationWorkflow(
                serializer,
                importer,
                reconciler,
                composer,
                new ManageSieveServerConnectionFactory(),
                synchronizationInteraction);
        IOutlookRuleExporter exporter =
            new OutlookRuleExporter(
                new OutlookFolderNormalizer(
                    OutlookFolderMappingOptionsProvider.GetOptions()));
        var cli = new OutlookResieverCliApplication(
            new OutlookExportApplication(exporter, serializer),
            synchronization,
            new EnvironmentSieveServerConfigurationProvider(),
            interaction);

        try
        {
            return await cli.RunAsync(options);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
