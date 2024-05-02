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
    public abstract bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues);

    public AnalyzersListConfig AnalyzersListConfig { get; set; } = default!;

    public abstract AnalyzerConfig GetConfig();

    public T GetConfig<T>() where T : AnalyzerConfig
        => (T)GetConfig();

    public AnalyzerSeverity GetSeverity()
        => GetConfig().AnalyzerSeverity;
}
