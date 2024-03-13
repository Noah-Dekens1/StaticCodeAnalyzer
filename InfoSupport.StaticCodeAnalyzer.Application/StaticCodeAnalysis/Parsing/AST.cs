using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
public class AST
{
    public required RootNode Root { get; set; }

    public static AST Build()
    {
        return new AST { Root = new RootNode() };
    }
}
