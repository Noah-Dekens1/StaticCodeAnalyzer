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

public class MagicNumberAnalyzer : Analyzer
{
    private void Test(int i)
    {

    }

    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var args = ast.Root.GetAllDescendantsOfType<InvocationExpressionNode>()
            .SelectMany(i => i.Arguments.Arguments)
            .Concat(ast.Root.GetAllDescendantsOfType<ObjectCreationExpressionNode>().SelectMany(o => o.Arguments.Arguments))
            .Where(a => a.Expression is NumericLiteralNode && a.Name is null);

        issues.AddRange(args.Select(a => new Issue(
            "magic-number",
            a.Location,
            severity: GetSeverity()
        )).ToList());

        Test(39);

        return true;
    }
    public override AnalyzerConfig GetConfig()
        => AnalyzersListConfig.MagicNumbers;
}
