using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;

public class LargeMethodAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var maxStatementsInBody = GetConfig<MaxStatementsConfig>().MaxStatements;

        var methods = ast.Root
            .GetAllDescendantsImplementing<IMethod>()
            .Where(m => (m.Body?.GetAllDescendantsOfType<StatementNode>().Count ?? 0) > maxStatementsInBody)
            .ToList();

        foreach (var method in methods)
        {
            issues.Add(
                new Issue(
                    "method-too-large",
                    ((AstNode)method).Location,
                    severity: GetSeverity()
                )
            );
        }

        return true;
    }

    public override AnalyzerConfig GetConfig()
        => AnalyzersListConfig.LargeMethods;
}
