using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis.FlowAnalysis.ControlFlow;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;
public class SemanticModel
{
    public SymbolResolver SymbolResolver { get; set; } = new SymbolResolver();

    public void ProcessFile(AST ast)
    {
        SymbolResolver.Resolve(ast);
    }

    public void ProcessFinished()
    {
        SymbolResolver.ResolveUsings();
    }
}
