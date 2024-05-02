using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
public abstract class Analyzer
{
    // Review: The signature of this method can use some improvement. 
    // The return type bool is not used
    // Most importantly, the Analyzers using this abstract base class update the issues list directly.
    // In your Runner.cs class it results in a strange setup where you first create an empty Issue list and then pass it to the Analyze method.
    // Instead you could let this method return a list of issues.
    public abstract bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues);
    
    public AnalyzersListConfig AnalyzersListConfig { get; set; } = default!;

    public abstract AnalyzerConfig GetConfig();

    public T GetConfig<T>() where T : AnalyzerConfig
        => (T)GetConfig();
}
