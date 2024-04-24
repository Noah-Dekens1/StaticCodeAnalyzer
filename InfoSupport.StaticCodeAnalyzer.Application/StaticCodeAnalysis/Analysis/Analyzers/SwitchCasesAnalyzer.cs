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

public class SwitchCasesAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        const int maxSwitchSections = 10;

        var switchStatements = ast.Root
            .GetAllDescendantsOfType<SwitchStatementNode>()
            .Where(s => s.SwitchSectionNodes.Count > maxSwitchSections)
            .ToList();

        foreach (var switchStatement in switchStatements)
        {
            issues.Add(new Issue(
                "switch-too-many-cases",
                "Too many switch cases can become unreadable, try to split up the code more or use a lookup dictionary",
                switchStatement.Location
            ));
        }

        return true;
    }
}
