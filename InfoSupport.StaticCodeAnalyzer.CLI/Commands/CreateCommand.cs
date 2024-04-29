using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.Services;
using InfoSupport.StaticCodeAnalyzer.CLI.Utils;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.CLI.Commands;
public class CreateCommand : ICommandHandler
{
    public async Task Run(ArgsUtil args)
    {
        var directory = Directory.GetCurrentDirectory();
        var isConfig = args.HasOption("config");

        if (!isConfig)
        {
            Console.WriteLine($"Unrecognized create type, currently only 'config' is supported");
            return;
        }

        var configFilePath = Path.Combine(directory, "analyzer-config.json");

        try
        {
            ProjectService.CreateConfigFileInternal(configFilePath);
            Console.WriteLine($"Successfully created config at {configFilePath}.");
        } 
        catch
        {
            Console.WriteLine("Failed to create config file.");
        }
    }
}
