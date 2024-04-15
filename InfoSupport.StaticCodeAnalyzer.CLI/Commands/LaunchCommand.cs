using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.CLI.Utils;

namespace InfoSupport.StaticCodeAnalyzer.CLI.Commands;
public class LaunchCommand : ICommandHandler
{
    public async Task Run(ArgsUtil args)
    {
        await FrontendUtil.StartIfNotRunning();
    }
}
