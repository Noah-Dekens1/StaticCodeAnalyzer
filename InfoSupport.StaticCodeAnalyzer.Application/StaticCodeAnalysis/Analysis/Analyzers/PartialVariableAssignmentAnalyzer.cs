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

public class PartialVariableAssignmentAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var variableDeclarations = ast.Root
            .GetAllDescendantsOfType<VariableDeclarationStatement>();

        var partialDeclarators = new List<VariableDeclaratorNode>();

        foreach (var variableDeclarationStatement in variableDeclarations)
        {
            var totalDeclarators = variableDeclarationStatement.Declarators;
            var assignedDeclarators = variableDeclarationStatement.Declarators.Where(d => d.Value is not null).ToList();

            if (totalDeclarators.Count != 1 && totalDeclarators.Count != assignedDeclarators.Count && assignedDeclarators.Count != 0)
            {
                partialDeclarators.AddRange(totalDeclarators.Except(assignedDeclarators));
            }
        }

        foreach (var partialDeclarator in partialDeclarators)
        {
            issues.Add(new Issue(
                "partial-variable-assignment",
                "If only a few variables are assigned in a single variable declaration with multiple declarators it can become unclear which variables are assigned to",
                partialDeclarator.Location
            ));
        }

        return true;
    }
}
