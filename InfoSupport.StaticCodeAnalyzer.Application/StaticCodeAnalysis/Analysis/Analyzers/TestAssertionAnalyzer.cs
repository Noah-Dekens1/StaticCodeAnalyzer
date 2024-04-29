using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;

internal static class TestAssertionExtensions
{
    private readonly static Dictionary<MethodNode, bool> Cache = [];
    private readonly static HashSet<MethodNode> Locked = [];

    public static bool DoesMethodContainAssertion(this MethodNode method, SymbolResolver symbolResolver)
    {

        // The cache also prevents cyclic loops
        if (Cache.TryGetValue(method, out bool result))
            return result;

        if (Locked.Contains(method))
            return false;

        Locked.Add(method);

        var calls = method.GetAllDescendantsOfType<InvocationExpressionNode>().ToList();
        
        foreach (var call in calls)
        {
            var name = call.LHS.AsLongIdentifier();

            if (name is not null && name.Contains("Assert")) // Extremely naive implementation
            {
                Cache.Add(method, true);
                Locked.Remove(method);
                return true;
            }

            var symbol = symbolResolver.GetSymbolForNode(call.LHS);

            if (symbol is not null && symbol.Kind == SymbolKind.Method)
            {
                var callee = symbol.Node as MethodNode;

                if (callee?.DoesMethodContainAssertion(symbolResolver) ?? false)
                {
                    Locked.Remove(method);
                    return true;
                }
            }
        }

        //method.GetAllCalledMethods();

        Locked.Remove(method);
        Cache.Add(method, false);

        return false;
    }
}

public class TestAssertionAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var classes = ast.GetClasses();
        var testClasses = classes.Where(c => c.HasAttribute("TestClass")).ToList();

        var methods = testClasses.SelectMany(c => c.Members).OfType<MethodNode>().ToList();
        var testMethods = methods.Where(m => m.HasAttribute("TestMethod")).ToList();

        foreach (var test in testMethods)
        {
            bool isExpectedExceptionAttribute = test.HasAttribute("ExpectedException");
            var containsAssertions = test.DoesMethodContainAssertion(projectRef.SemanticModel.SymbolResolver);

            if (!containsAssertions && !isExpectedExceptionAttribute)
            {
                issues.Add(
                    new Issue(
                        "test-method-without-assertion",
                        "This test method doesn't contain an assertion resulting in nothing being tested",
                        test.Location
                    )
                );
            }
        }

        return true;
    }

    public override AnalyzerConfig GetConfig()
        => AnalyzersListConfig.TestAssertions;
}
