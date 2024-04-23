using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        var analyzer = new ControlFlowAnalyzer();
        ControlFlowAnalyzer.AnalyzeControlFlow(ast.Root, out var cfg);

        Assert.IsNotNull(cfg);
    }
}
