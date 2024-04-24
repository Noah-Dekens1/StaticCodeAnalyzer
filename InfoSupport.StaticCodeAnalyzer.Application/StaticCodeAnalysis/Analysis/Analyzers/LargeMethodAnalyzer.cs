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
        const int maxStatementsInBody = 30;

        var methods = ast.Root
            .GetAllDescendantsImplementing<IMethod>()
            .Where(m => (m.Body?.GetAllDescendantsOfType<StatementNode>().Count ?? 0) > maxStatementsInBody)
            .ToList();

        foreach (var method in methods)
        {
            issues.Add(
                new Issue(
                    "method-too-large",
                    "Large methods can be difficult to read and contain duplicated code, split up the method in smaller methods",
                    ((AstNode)method).Location
                )
            );
        }

        return true;
    }
}
