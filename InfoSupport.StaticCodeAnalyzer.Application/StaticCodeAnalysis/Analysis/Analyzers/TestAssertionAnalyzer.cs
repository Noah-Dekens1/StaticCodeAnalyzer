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

internal static class TestAssertionExtensions
{
    public static bool DoesMethodContainAssertion(this MethodNode method)
    {
        var calls = method.GetAllDescendantsOfType<InvocationExpressionNode>().ToList();
        
        foreach (var call in calls)
        {
            var name = call.LHS.AsLongIdentifier();

            if (name is not null && name.StartsWith("Assert")) // Extremely naive implementation
            {
                return true;
            }
        }

        //method.GetAllCalledMethods();

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
            var methodCalls = testMethods
                .Select(s => s.DoesMethodContainAssertion()).ToList();
        }

        return true;
    }
}
