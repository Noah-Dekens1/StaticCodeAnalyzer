using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.CLI.Utils;

namespace InfoSupport.StaticCodeAnalyzer.CLI.Commands;
public interface ICommandHandler
{
    public Task Run(ArgsUtil args);
}
