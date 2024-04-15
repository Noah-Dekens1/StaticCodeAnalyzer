using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.CLI.Commands;
using InfoSupport.StaticCodeAnalyzer.CLI.Utils;
using InfoSupport.StaticCodeAnalyzer.Domain;
using InfoSupport.StaticCodeAnalyzer.WebAPI;

var parms = new ArgsUtil(args);

if (!parms.Validate() || parms.GetCommand() is null or "help")
{
    Console.WriteLine("---- Static Code Analyzer");
    Console.WriteLine("help                -> Shows this usage guide.");
    Console.WriteLine("analyze [directory] -> Analyze a repository, creates a new project if one doesn't exist. " +
        "If directory is empty the current one will be used instead.");
    Console.WriteLine("launch              -> Launches web application");
    Console.WriteLine("----");
    Console.WriteLine("-CI                 -> Output to console, don't start webapp");
    return;
}

ICommandHandler handler = parms.GetCommand() switch
{
    "analyze" => new AnalyzeCommand(),
    "launch"  => new LaunchCommand(),
    _ => throw new InvalidOperationException()
};

await handler.Run(parms);
