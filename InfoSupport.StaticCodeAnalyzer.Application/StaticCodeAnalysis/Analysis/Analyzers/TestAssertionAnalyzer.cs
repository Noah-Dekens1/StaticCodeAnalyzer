using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;

public class TestAssertionAnalyzer : Analyzer
{
    public override bool Analyze(AST ast, List<Issue> issues)
    {
        Console.WriteLine("Hello world!");
        issues = new List<Issue>();
        return false;
    }
}
