﻿using System;
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

    public static bool IsAssertion(this InvocationExpressionNode call, TestAssertionsConfig config)
    {
        var name = call.LHS.AsLongIdentifier();

        if (name is null)
            return false;

        if (config.AnyNameIncludingAssert && name.Contains("assert", StringComparison.CurrentCultureIgnoreCase))
            return true;

        if (config.UseCustomAssertionMethods)
        {
            foreach (var assertionMethod in config.AssertionMethods)
            {
                if (name.Contains(assertionMethod))
                    return true;
            }
        }

        return false;
    }

    public static bool DoesMethodContainAssertion(this MethodNode method, SymbolResolver symbolResolver, TestAssertionsConfig config)
    {
        if (Cache.TryGetValue(method, out bool result))
            return result;

        if (Locked.Contains(method))
            return false;

        Locked.Add(method);

        var calls = method.GetAllDescendantsOfType<InvocationExpressionNode>().ToList();
        
        foreach (var call in calls)
        {
            if (call.IsAssertion(config))
            {
                Cache.Add(method, true);
                Locked.Remove(method);
                return true;
            }

            if (!config.CheckCalledMethods)
                continue;

            var symbol = symbolResolver.GetSymbolForNode(call.LHS);

            if (symbol is not null && symbol.Kind == SymbolKind.Method)
            {
                var callee = symbol.Node as MethodNode;

                if (callee?.DoesMethodContainAssertion(symbolResolver, config) ?? false)
                {
                    Locked.Remove(method);
                    return true;
                }
            }
        }

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
            var containsAssertions = test.DoesMethodContainAssertion(projectRef.SemanticModel.SymbolResolver, GetConfig<TestAssertionsConfig>());

            if (!containsAssertions && !isExpectedExceptionAttribute)
            {
                issues.Add(
                    new Issue(
                        "test-method-without-assertion",
                        test.Location,
                        severity: GetSeverity()
                    )
                );
            }
        }

        return true;
    }

    public override AnalyzerConfig GetConfig()
        => AnalyzersListConfig.TestAssertions;
}
