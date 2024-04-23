using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis.FlowAnalysis.ControlFlow;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SemanticModelTests;

[TestClass]
public class ControlFlowTests
{
    private AST Parse(string text)
    {
        var tokens = Lexer.Lex(text);
        return Parser.Parse(tokens);
    }

    public static void AssertAllReachable(ControlFlowGraph cfg)
    {
        var reachable = ControlFlowAnalyzer.ComputeReachability(cfg);

        foreach (var node in cfg.Nodes)
        {
            Assert.IsTrue(reachable.Contains(node));
        }
    }

    [TestMethod]
    public void Analyze_SingleBlockExplicitExitPoint_ReturnsValidCFG()
    {
        var ast = Parse("""
            var a = 0;
            return;
            """);

        var analyzer = new ControlFlowAnalyzer();
        ControlFlowAnalyzer.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);
    }

    [TestMethod]
    public void Analyze_IfElseStatement_ReturnsValidCFG()
    {
        var ast = Parse("""
            var a = 0;

            if (a > 0)
            {
                Console.WriteLine("hello");
            }
            else
            {
                Console.WriteLine("world");
            }

            return;
            """);

        var analyzer = new ControlFlowAnalyzer();
        ControlFlowAnalyzer.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);
    }

    [TestMethod]
    public void Analyze_NestedIfElseStatements_ReturnsValidCFG()
    {
        var ast = Parse("""
            var a = 0;

            if (a > 0) // this should branch to the first blocks in if/else
            {
                if (a == 3)
                    Console.WriteLine("hello"); // this should branch to "hello there"

                Console.WriteLine("hello there"); // this should branch to "return;"
            }
            else
            {
                Console.WriteLine("world"); // this should branch to "return;"
            }

            return;
            """);

        ControlFlowAnalyzer.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);
        AssertAllReachable(cfg);
    }

    [TestMethod]
    public void Analyze_UnreachableAfterBreak_ReturnsValidCFG()
    {
        var ast = Parse("""
            int i = 0;
            while (true)
            {
                i++;
                Console.WriteLine("hey");
                if (i > 10)
                {
                    break;
                    Console.WriteLine("unreachable");
                }
            }

            Console.WriteLine("end");
            """);

        ControlFlowAnalyzer.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        // Ensure that Console.WriteLine("unreachable"); is unreachable here

        var reachable = ControlFlowAnalyzer.ComputeReachability(cfg);
        var unreachable = cfg.Nodes.Except(reachable).ToList();
        Assert.IsTrue(unreachable.Count == 1);

        var instructions = unreachable.First().Instructions;
        Assert.IsTrue(instructions.Count == 1);

        var firstInstruction = instructions.First();
        Assert.IsTrue(firstInstruction is ExpressionStatementNode);
        Assert.IsTrue(((ExpressionStatementNode)firstInstruction).Expression is InvocationExpressionNode);
    }

    [TestMethod]
    public void Analyze_UnreachableAfterContinue_ReturnsValidCFG()
    {
        var ast = Parse("""
            int i = 0;
            while (true)
            {
                i++;
                Console.WriteLine("hey");
                if (i > 10)
                {
                    continue;
                    Console.WriteLine("unreachable");
                }
            }

            Console.WriteLine("end");
            """);

        ControlFlowAnalyzer.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        // Ensure that Console.WriteLine("unreachable"); is unreachable here

        var reachable = ControlFlowAnalyzer.ComputeReachability(cfg);
        var unreachable = cfg.Nodes.Except(reachable).ToList();
        Assert.IsTrue(unreachable.Count == 1);

        var instructions = unreachable.First().Instructions;
        Assert.IsTrue(instructions.Count == 1);

        var firstInstruction = instructions.First();
        Assert.IsTrue(firstInstruction is ExpressionStatementNode);
        Assert.IsTrue(((ExpressionStatementNode)firstInstruction).Expression is InvocationExpressionNode);
    }

    [TestMethod]
    public void Analyze_ConditionalContinue_IsReachable()
    {
        var ast = Parse("""
            int i = 0;
            while (true)
            {
                if (i == 10)
                    continue;

                Console.WriteLine("reachable!");
            }

            Console.WriteLine("end");
            """);

        ControlFlowAnalyzer.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        var reachableNode = ast.Root
            .GetAllDescendantsOfType<ExpressionStatementNode>()
            .Select(e => e.Expression)
            .Where(e => e is InvocationExpressionNode)
            .Cast<InvocationExpressionNode>()
            .Where(e => e.GetAllDescendantsOfType<StringLiteralNode>().FirstOrDefault()?.Value == "reachable!")
            .FirstOrDefault();

        Assert.IsNotNull(reachableNode);
        Assert.IsTrue(ControlFlowAnalyzer.IsReachable(reachableNode, cfg));
    }
}
