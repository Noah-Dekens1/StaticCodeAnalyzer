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
public class NestedTernaryAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        //var example = true ? true : true ? false : true;

        var ternaryExpressions = ast.Root
            .GetAllDescendantsOfType<TernaryExpressionNode>()
            .Where(t => t.GetAllDescendantsOfType<TernaryExpressionNode>().Count > 0)
            .ToList();

        issues.AddRange(ternaryExpressions
            .Select(e => new Issue("nested-ternary", "Nested ternary expressions can be difficult to read and debug", e.Location))
            .ToList()
        );

        return true;
    }
}
