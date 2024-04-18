using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.SemanticModelTests;

[TestClass]
public class SymbolTests
{
    [TestMethod]
    public void Resolve_LocalVariable_ReturnsValidSymbol()
    {
        var tokens = Lexer.Lex("""
            var a = 0;
            Console.WriteLine(a);
            """);

        var ast = Parser.Parse(tokens);

        var symbolResolver = new SymbolResolver();
        symbolResolver.Resolve(ast);

        var symbol = symbolResolver.GetSymbolForExpression(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Identifier == "a")
            .First()
        );

        Debug.Assert(symbol is not null);
        Debug.Assert(symbol.Name == "a");
        Debug.Assert(symbol.Kind == SymbolKind.LocalVariable);
    }

    [TestMethod]
    public void Resolve_LocalVariableInInnerScope_ReturnsValidSymbol()
    {
        var tokens = Lexer.Lex("""
            var a = 0;
            {
                Console.WriteLine(a);
            }
            """);

        var ast = Parser.Parse(tokens);

        var symbolResolver = new SymbolResolver();
        symbolResolver.Resolve(ast);

        var symbol = symbolResolver.GetSymbolForExpression(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Identifier == "a")
            .First()
        );

        Debug.Assert(symbol is not null);
        Debug.Assert(symbol.Name == "a");
        Debug.Assert(symbol.Kind == SymbolKind.LocalVariable);
    }

    [TestMethod]
    public void Resolve_LocalVariableNotInScope_ReturnsNullSymbol()
    {
        var tokens = Lexer.Lex("""
            {
                var a = 0;
            }
            Console.WriteLine(a); // a is out of scope here
            """);

        var ast = Parser.Parse(tokens);

        var symbolResolver = new SymbolResolver();
        symbolResolver.Resolve(ast);

        var symbol = symbolResolver.GetSymbolForExpression(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Identifier == "a")
            .First()
        );

        Debug.Assert(symbol is null);
    }

    /**
     * TODO
     * - Test type declarations
     * - Members (methods, fields, properties)
     * - Local functions
     * - ..? Capture exception variables? Maybe change them to be declarator nodes as well (like foreach?)
     * - Test methods/classes with generic names
     * - (scoped) using directives + using aliases (is counted as a declaration?)
     * - Namespaces / member access
     * 
     * How do we deal with using directives & aliases?
     * 
     */

    [TestMethod]
    public void Resolve_LocalFunctionDeclaration_ReturnsValidSymbol()
    {
        var tokens = Lexer.Lex("""
            void A()
            {

            }

            A();
            """);

        var ast = Parser.Parse(tokens);

        var symbolResolver = new SymbolResolver();
        symbolResolver.Resolve(ast);

        var symbol = symbolResolver.GetSymbolForExpression(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Identifier == "A")
            .First()
        );

        Debug.Assert(symbol is not null);
        Debug.Assert(symbol.Name == "A");
        Debug.Assert(symbol.Kind == SymbolKind.Method);
    }

    [TestMethod]
    public void Resolve_LocalFunctionDeclarationInNamespace_ReturnsValidSymbol()
    {
        var tokens = Lexer.Lex("""
            namespace Example
            {
                public static class Utils
                {
                    public static void A()
                    {

                    }
                }
            }

            class Program
            {
                public static void Main()
                {
                    Example.Utils.A();
                }
            }
            
            """);

        var ast = Parser.Parse(tokens);

        var symbolResolver = new SymbolResolver();
        symbolResolver.Resolve(ast);

        var symbol = symbolResolver.GetSymbolForExpression(ast.Root
            .GetAllDescendantsOfType<MemberAccessExpressionNode>()
            .Where(e => e.Identifier.AsIdentifier() == "A")
            .First()
        );

        Debug.Assert(symbol is not null);
        Debug.Assert(symbol.Name == "A");
        Debug.Assert(symbol.Kind == SymbolKind.Method);
    }
}
