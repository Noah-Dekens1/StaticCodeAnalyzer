using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis.FlowAnalysis.ControlFlow;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SemanticModelTests;

[TestClass]
public class ControlFlowTests
{
    private readonly SemanticModel _semanticModel = new();

    private AST Parse(string text)
    {
        var tokens = Lexer.Lex(text);
        return Parser.Parse(tokens);
    }

    public void AssertAllReachable(ControlFlowGraph cfg)
    {
        var reachable = _semanticModel.ComputeReachability(cfg);

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

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

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

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

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

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

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

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        // Ensure that Console.WriteLine("unreachable"); is unreachable here

        var reachable = _semanticModel.ComputeReachability(cfg);
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

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        // Ensure that Console.WriteLine("unreachable"); is unreachable here

        var reachable = _semanticModel.ComputeReachability(cfg);
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

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        var reachableNode = ast.Root
            .GetAllDescendantsOfType<ExpressionStatementNode>()
            .Select(e => e.Expression)
            .Where(e => e is InvocationExpressionNode)
            .Cast<InvocationExpressionNode>()
            .Where(e => e.GetAllDescendantsOfType<StringLiteralNode>().FirstOrDefault()?.Value == "reachable!")
            .FirstOrDefault();

        Assert.IsNotNull(reachableNode);
        Assert.IsTrue(_semanticModel.IsReachable(reachableNode, cfg));
    }

    [TestMethod]
    public void Analyze_UnconditionalContinue_IsNotReachable()
    {
        var ast = Parse("""
        int i = 0;
        while (true)
        {
            continue;

            Console.WriteLine("unreachable!");
        }

        Console.WriteLine("end");
        """);

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        var reachableNode = ast.Root
            .GetAllDescendantsOfType<ExpressionStatementNode>()
            .Select(e => e.Expression)
            .Where(e => e is InvocationExpressionNode)
            .Cast<InvocationExpressionNode>()
            .Where(e => e.GetAllDescendantsOfType<StringLiteralNode>().FirstOrDefault()?.Value == "unreachable!")
            .FirstOrDefault();

        Assert.IsNotNull(reachableNode);
        Assert.IsFalse(_semanticModel.IsReachable(reachableNode, cfg));
    }

    [TestMethod]
    public void Analyze_ConditionalAssignment_IsNotUnconditionallyReachable()
    {
        var ast = Parse("""
        if (someCondition)
            person = new Person();
        """);

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        var conditionalNode = ast.Root
            .GetAllDescendantsOfType<ExpressionStatementNode>()
            .Select(e => e.Expression)
            .Where(e => e is AssignmentExpressionNode)
            .Cast<AssignmentExpressionNode>()
            .FirstOrDefault();

        Assert.IsNotNull(conditionalNode);
        Assert.IsFalse(_semanticModel.IsUnconditionallyReachable(conditionalNode, cfg));
    }

    [TestMethod]
    public void Analyze_UnconditionalAssignment_IsUnconditionallyReachable()
    {
        var ast = Parse("""
        person = new Person();

        if (person is not null)
            Console.WriteLine(person);

        person = new Person();
        """);

        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);

        var unconditionalNode = ast.Root
            .GetAllDescendantsOfType<ExpressionStatementNode>()
            .Select(e => e.Expression)
            .Where(e => e is AssignmentExpressionNode)
            .Cast<AssignmentExpressionNode>()
            .FirstOrDefault();

        var conditionalNode = ast.Root
            .GetAllDescendantsOfType<ExpressionStatementNode>()
            .Select(e => e.Expression)
            .Where(e => e is InvocationExpressionNode)
            .FirstOrDefault();

        var unconditionalNode2 = ast.Root
            .GetAllDescendantsOfType<ExpressionStatementNode>()
            .Select(e => e.Expression)
            .Where(e => e is AssignmentExpressionNode)
            .Cast<AssignmentExpressionNode>()
            .ToList()[1];

        Assert.IsNotNull(unconditionalNode);
        Assert.IsNotNull(conditionalNode);
        Assert.IsTrue(_semanticModel.IsUnconditionallyReachable(unconditionalNode, cfg));
        Assert.IsFalse(_semanticModel.IsUnconditionallyReachable(conditionalNode, cfg));
        Assert.IsTrue(_semanticModel.IsUnconditionallyReachable(unconditionalNode2, cfg));
    }

    [TestMethod]
    public void Analyze_UnreachableCodeAfterReturn_ReturnsNotReachable()
    {
        var ast = Parse("""
            return;
            var person = new Person();
            """);
        
        _semanticModel.AnalyzeControlFlow(ast.Root, out var cfg);

        var statementNode = ast.Root
            .GetAllDescendantsOfType<VariableDeclarationStatement>()
            .FirstOrDefault();

        Assert.IsNotNull(cfg);
        Assert.IsNotNull(statementNode);

        Assert.IsFalse(_semanticModel.IsReachable(statementNode, cfg));
    }
}
