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

public class LargeTypeAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var maxMembersInType = GetConfig<MaxMembersConfig>().MaxMembers;

        var types = ast.Root
            .GetAllDescendantsOfType<BasicDeclarationNode>()
            .Where(d => d.Members.Count > maxMembersInType)
            .ToList();

        foreach (var type in types)
        {
            issues.Add(
                new Issue(
                    "class-too-large",
                    type.Location,
                    severity: GetSeverity()
                )
            );
        }

        return true;
    }

    public override AnalyzerConfig GetConfig()
        => AnalyzersListConfig.LargeTypes;
}
