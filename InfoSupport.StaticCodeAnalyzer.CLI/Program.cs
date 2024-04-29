using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.CLI.Commands;
using InfoSupport.StaticCodeAnalyzer.CLI.Utils;
using InfoSupport.StaticCodeAnalyzer.Domain;
using InfoSupport.StaticCodeAnalyzer.WebAPI;

var parms = new ArgsUtil(args);

if (!parms.Validate() || parms.GetCommand() is null or "help")
{
    Console.WriteLine("---- Static Code Analyzer");
    Console.WriteLine(" Basic usage: analyzer [command] [options]");
    Console.WriteLine("");
    Console.WriteLine("---- Commands");
    Console.WriteLine(" help                                   -> Shows this usage guide.");
    Console.WriteLine(" analyze [directory] [--output-console] -> Analyze a repository, creates a new project if one doesn't exist. " +
        "If no directory is provided the current one will be used instead.");
    Console.WriteLine(" launch                                 -> Launches web application");
    Console.WriteLine(" create config                          -> Creates a new config file in the project belonging to the currently open directory");
    Console.WriteLine("");
    Console.WriteLine("-------------------------");
    return;
}

ICommandHandler handler = parms.GetCommand() switch
{
    "analyze" => new AnalyzeCommand(),
    "launch"  => new LaunchCommand(),
    "create"  => new CreateCommand(),
    _ => throw new InvalidOperationException()
};

await handler.Run(parms);
