using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.CLI.Utils;
public class ArgsUtil(string[] args)
{
    private readonly string[] _args = args;

    private string[] _availableCommands = [
        "analyze",
        "launch",
        "create",
        "help"
    ];


    public bool Validate()
    {
        return _availableCommands.Contains(_args.FirstOrDefault());
    }

    public string GetCommand()
    {
        return _args[0];
    }

    public string GetDirectory()
    {
        var dir = args.Length >= 2
            ? args[1]
            : Directory.GetCurrentDirectory();

        // normalize
        return dir.Replace('\\', '/').TrimEnd('/');
    }

    public bool HasOption(string option)
    {
        return _args.Contains(option);
    }
}
