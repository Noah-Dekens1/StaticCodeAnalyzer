﻿using System;
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

    private AST Parse(string text)
    {
        var tokens = Lexer.Lex(text);
        return Parser.Parse(tokens);
    }

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

        var symbol = symbolResolver.GetSymbolForNode(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Identifier == "a")
            .First()
        );

        Assert.IsNotNull(symbol);
        Assert.AreEqual("a", symbol.Name);
        Assert.AreEqual(SymbolKind.LocalVariable, symbol.Kind);
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

        var symbol = symbolResolver.GetSymbolForNode(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Identifier == "a")
            .First()
        );

        Assert.IsNotNull(symbol);
        Assert.AreEqual("a", symbol.Name);
        Assert.AreEqual(SymbolKind.LocalVariable, symbol.Kind);
    }

    [TestMethod]
    public void Resolve_Parameter_ReturnsValidSymbol()
    {
        var tokens = Lexer.Lex("""
            void Test(int a)
            {
                Console.WriteLine(a);
            }

            Test(0);
            """);

        var ast = Parser.Parse(tokens);

        var symbolResolver = new SymbolResolver();
        symbolResolver.Resolve(ast);

        var symbol = symbolResolver.GetSymbolForNode(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Parent is not ParameterNode)
            .Where(e => e.Identifier == "a")
            .First()
        );

        Assert.IsNotNull(symbol);
        Assert.AreEqual("a", symbol.Name);
        Assert.AreEqual(SymbolKind.Parameter, symbol.Kind);
    }

    [TestMethod]
    public void Resolve_ParameterOutOfScope_ReturnsNullSymbol()
    {
        var tokens = Lexer.Lex("""
            void Test(int a)
            {
                
            }

            Console.WriteLine(a);
            """);

        var ast = Parser.Parse(tokens);

        var symbolResolver = new SymbolResolver();
        symbolResolver.Resolve(ast);

        var symbol = symbolResolver.GetSymbolForNode(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Parent is not ParameterNode)
            .Where(e => e.Identifier == "a")
            .First()
        );

        Assert.IsNull(symbol);
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

        var symbol = symbolResolver.GetSymbolForNode(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Identifier == "a")
            .First()
        );

        Assert.IsNull(symbol);
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

        var symbol = symbolResolver.GetSymbolForNode(ast.Root
            .GetAllDescendantsOfType<IdentifierExpression>()
            .Where(e => e.Identifier == "A")
            .First()
        );

        Assert.IsNotNull(symbol);
        Assert.AreEqual("A", symbol.Name);
        Assert.AreEqual(SymbolKind.Method, symbol.Kind);
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

        var symbol = symbolResolver.GetSymbolForNode(ast.Root
            .GetAllDescendantsOfType<MemberAccessExpressionNode>()
            .Where(e => e.Identifier.AsIdentifier() == "A")
            .First()
        );

        Assert.IsNotNull(symbol);
        Assert.AreEqual("A", symbol.Name);
        Assert.AreEqual(SymbolKind.Method, symbol.Kind);
    }

    [TestMethod]
    public void Resolve_MultipleFiles_ReturnsValidSymbol()
    {
        var file1 = Parse("""
            class Program
            {
                public static void Main()
                {
                    Example.Utils.A();
                }
            }
            """);

        var file2 = Parse("""
            namespace Example;

            class Utils
            {
                public static void A()
                {

                }
            }
            """);

        var resolver = new SymbolResolver();
        resolver.Resolve(file1);
        resolver.Resolve(file2);

        var symbol = resolver.GetSymbolForNode(
            file1.Root
                .GetAllDescendantsOfType<MemberAccessExpressionNode>()
                .Where(e => e.Identifier.AsIdentifier() == "A")
                .First()
        );

        Assert.IsNotNull(symbol);
        Assert.AreEqual("A", symbol.Name);
        Assert.AreEqual(SymbolKind.Method, symbol.Kind);
    }

    [TestMethod]
    public void Resolve_Usings_ReturnsValidSymbol()
    {
        var file1 = Parse("""
            using Example;

            class Program
            {
                public static void Main()
                {
                    Utils.A();
                }
            }
            """);

        var file2 = Parse("""
            namespace Example;

            class Utils
            {
                public static void A()
                {

                }
            }
            """);

        var resolver = new SymbolResolver();
        resolver.Resolve(file1);
        resolver.Resolve(file2);
        resolver.ResolveUsings();

        var symbol = resolver.GetSymbolForNode(
            file1.Root
                .GetAllDescendantsOfType<MemberAccessExpressionNode>()
                .Where(e => e.Identifier.AsIdentifier() == "A")
                .First()
        );

        Assert.IsNotNull(symbol);
        Assert.AreEqual("A", symbol.Name);
        Assert.AreEqual(SymbolKind.Method, symbol.Kind);
    }

    [TestMethod]
    public void Resolve_CyclicUsings_ReturnsValidSymbol()
    {
        var file1 = Parse("""
            using Example;

            namespace ProgramNs;

            class Program
            {
                public static void Main()
                {
                    Utils.A();
                }
            }
            """);

        var file2 = Parse("""
            using ProgramNs;

            namespace Example;

            class Utils
            {
                public static void A()
                {
                    Program.Main();
                }
            }
            """);

        var resolver = new SymbolResolver();
        resolver.Resolve(file1);
        resolver.Resolve(file2);
        resolver.ResolveUsings();

        var symbol1 = resolver.GetSymbolForNode(
            file1.Root
                .GetAllDescendantsOfType<MemberAccessExpressionNode>()
                .Where(e => e.Identifier.AsIdentifier() == "A")
                .First()
        );

        var symbol2 = resolver.GetSymbolForNode(
            file2.Root
                .GetAllDescendantsOfType<MemberAccessExpressionNode>()
                .Where(e => e.Identifier.AsIdentifier() == "Main")
                .First()
        );

        Debug.Assert(symbol1 is not null);
        Debug.Assert(symbol1.Name == "A");
        Debug.Assert(symbol1.Kind == SymbolKind.Method);

        Debug.Assert(symbol2 is not null);
        Debug.Assert(symbol2.Name == "Main");
        Debug.Assert(symbol2.Kind == SymbolKind.Method);
    }

    [TestMethod]
    public void Resolve_GenericTypes_ReturnsValidSymbol()
    {
        var ast = Parse("""
            class A<T>
            {
                public static void SomeMethod<T1, T2>() { }
            }

            class Program
            {
                public static void Main()
                {
                    A<int>.SomeMethod<bool, string>();
                }
            }
            """);

        var resolver = new SymbolResolver();
        resolver.Resolve(ast);
        resolver.ResolveUsings();

        var symbol = resolver.GetSymbolForNode(
            ast.Root
                .GetAllDescendantsOfType<MemberAccessExpressionNode>()
                .Where(e => e.Identifier is GenericNameNode)
                .First()
        );

        Assert.IsNotNull(symbol);
        Assert.AreEqual("SomeMethod", symbol.Name);
        Assert.AreEqual(SymbolKind.Method, symbol.Kind);
    }
}