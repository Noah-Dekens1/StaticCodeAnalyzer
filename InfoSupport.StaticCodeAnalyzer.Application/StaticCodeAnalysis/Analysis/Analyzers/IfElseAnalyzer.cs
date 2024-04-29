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

public static class IfElseExtensions
{
    public static int GetElseCount(this IfStatementNode node)
    {
        if (node.ElseBody is null)
            return 0;

        return node.ElseBody is IfStatementNode elseBody 
            ? 1 + elseBody.GetElseCount() 
            : 1;
    }
}

public class IfElseAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var maxElseCount = GetConfig<MaxElsesConfig>().MaxElses;

        var ifStatements = ast.Root
            .GetAllDescendantsOfType<IfStatementNode>()
            .Where(s => s.Parent is not IfStatementNode)
            .Where(s => s.GetElseCount() > maxElseCount)
            .ToList();

        foreach (var ifStatement in ifStatements)
        {
            issues.Add(new Issue(
                "too-many-elses",
                "This if statement has too many elses, try refactoring your code to make it more readable",
                ifStatement.Location
            ));
        }

        return true;
    }

    public override AnalyzerConfig GetConfig()
        => AnalyzersListConfig.IfElse;
}
