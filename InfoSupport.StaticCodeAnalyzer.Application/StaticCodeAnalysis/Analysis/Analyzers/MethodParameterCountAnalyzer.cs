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

public class MethodParameterCountAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var maxParametersInMethod = GetConfig<MaxParametersConfig>().MaxParameters;

        var methods = ast.Root
            .GetAllDescendantsImplementing<IMethod>()
            .Where(m => m.Parameters.Parameters.Count > maxParametersInMethod)
            .ToList();

        foreach (var method in methods)
        {
            issues.Add(new Issue(
                code: "too-many-method-parameters",
                description: "This method has too many parameters, consider passing them through a seperate data class or struct",
                location: ((AstNode)method).Location
            ));
        }

        return true;
    }
    public override AnalyzerConfig GetConfig()
        => AnalyzersListConfig.MethodParameterCount;

}
