using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using NuGet.Frameworks;

namespace UnitTests;

[TestClass]
public class ParserTests
{
    private static T GetGlobalStatement<T>(AST ast, int index=0) where T : StatementNode
    {
        return (T)ast.Root.GlobalStatements[index].Statement;
    }

    [TestMethod]
    public void Parse_BasicBinaryExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("3 + 4 * 1 - (9 / 3)");
        var ast = Parser.Parse(tokens);

        // How do I test a AST in a sane way?
        /*
        var expectedAst = AST.Build();

        expectedAst.Root.GlobalStatements.Add(new GlobalStatementNode {
            Statement = new ExpressionStatementNode
            {
                Expression = new AddExpressionNode
                {
                    LHS = new NumericLiteralNode
                    {
                        Value = 3,
                    },
                    RHS = new AddExpressionNode
                    {
                        LHS = 
                    }
                }
            }
        });
        */

        AstAssertions.AssertThat(ast).IsValidTree()
            .GetGlobalStatement<ExpressionStatementNode>(ast)
            .GetExpression()
                .Validate<AddExpressionNode>(n =>
                    ((NumericLiteralNode)n.LHS).Value!.Equals(3) && n.RHS.GetType() == typeof(AddExpressionNode))
            .GetChild<AddExpressionNode>(n => n.RHS)
                .Validate<AddExpressionNode>(n =>
                    ((NumericLiteralNode)n.LHS).Value!.Equals(4) && n.RHS.GetType() == typeof(AddExpressionNode))
            .GetChild<AddExpressionNode>(n => n.RHS)
                .Validate<AddExpressionNode>(n =>
                    ((NumericLiteralNode)n.LHS).Value!.Equals(2) && n.RHS.GetType() == typeof(IdentifierExpression))
            .GetChild<AddExpressionNode>(n => n.RHS)
                .Validate<IdentifierExpression>(n => n.Identifier == "someIdentifier");
    }

    [TestMethod]
    public void Parse_BasicBinaryExpressionWithUnaryOperators_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("3 + -4 + 2 + -(someIdentifier * 8 + -(-(4)));");
        var ast = Parser.Parse(tokens);
    }

    [TestMethod]
    public void Parse_UnaryIncrementDecrementOperators_ReturnsValidAST()
    {
        var identifier = 0;
        var expr = 3 - identifier++ + 1;
        var tokens = Lexer.Lex("3 - identifier++ + -1");
        var ast = Parser.Parse(tokens);
    }

    [TestMethod]
    public void Parse_BasicBooleanExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("(a == b && c >= 3) || (a != c && !!!d) && a < j && j <= b && q > e");
        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_BasicVariableDeclaration_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("var test = (a == b && c >= 3) || (a != c && !!!d) && a < j && j <= b && q > e;");
        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_VariableDeclarationWithType_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("int a = 0;");
        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_BasicBinaryExpression2_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("3 * 4 / 2 + someIdentifier % 3;");
        var ast = Parser.Parse(tokens);

        var expr = GetGlobalStatement<ExpressionStatementNode>(ast, 0);

        Assert.IsNotNull(expr);
    }

    [TestMethod]
    public void Parse_IfStatement_ReturnsValidAST()
    {

        var tokens = Lexer.Lex("""
            if (10 > 3 && (5 < 7 || 2 != 3))
                ;
            """);

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_IfStatementWithBlockBody_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            if (true)
            {
                var a = "Hello world!";
                var b = true;
                var c = false;
            }
            """);

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_IfStatementWithElse_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            if (true)
            {
                ;
            }
            else if (false) // embedded if statement in else clause
            {
                ;
            }
            else
                ;
            """);

        var ast = Parser.Parse(tokens);
        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_DoStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var a = 0;
            do
            {
                a++;
            } while (a < 10);
            """);

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_ForStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            for (int i = 0; i < 10; i++)
            {
                i++;
            }
            """);

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_ComplexForStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            int i = 0;
            int b = 10;
            int c = 5;
            for (i = 3, ++i, i--, i++; i < 10; i++, c = 0)
            {
                b--;
            }
            """);

        List<string> l = [""];
        var t = (l, 1);
        foreach (var s in t.l)
        {

        }

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_EmptyForStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            for (;;)
                ;
            """);

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_ForEachStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            int count = 0;
            foreach (var item in test.someList)
                count++;
            """);

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_WhileStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var problems = 100;
            while ((a == true && b < 4) || c && (l > r*2))
            {
                problems = problems - 1;
            }
            """);

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [TestMethod]
    public void Parse_MemberAccess_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            abc.def.g.hi++;
            ++abc.def.g.hi;
            ++a;
            a++;
            a.b++ + -3;
            a = ++b;
            a = b++ + 3;
            a = ++b * c;
            """);

        var ast = Parser.Parse(tokens);

        Assert.IsTrue(false);
    }

    [DataTestMethod]
    [DataRow("1", 1)]
    [DataRow("3", 3)]
    [DataRow("173", 173)]
    [DataRow("74.3f", 74.3f)]
    public void Parse_NumericLiteral_ReturnsValidAST(string numericLiteral, object value)
    {
        // Arrange
        var tokens = Lexer.Lex(numericLiteral);

        // Act
        var ast = Parser.Parse(tokens);

        // Assert
        var statement = ast.Root.GlobalStatements[0].Statement;

        Assert.IsNotNull(ast);
        Assert.IsNotNull(ast.Root);
        Assert.IsNotNull(statement);

        Assert.IsTrue(ast.Root.GlobalStatements.Count != 0);
        Assert.AreEqual(typeof(ExpressionStatementNode), statement.GetType());

        var expr = ((ExpressionStatementNode)statement).Expression;
        Assert.IsNotNull(expr);

        Assert.AreEqual(typeof(NumericLiteralNode), expr.GetType());
        Assert.AreEqual(value, ((NumericLiteralNode)expr).Value);
    }

    [DataTestMethod]
    [DataRow("true", true)]
    [DataRow("false", false)]
    public void Parse_BooleanLiteral_ReturnsValidAST(string literal, bool value)
    {
        // Arrange
        var tokens = Lexer.Lex(literal);

        // Act
        var ast = Parser.Parse(tokens);

        // Assert
        Assert.IsNotNull(ast);
        var expr = GetGlobalStatement<ExpressionStatementNode>(ast).Expression;
        Assert.IsNotNull(expr);

        Assert.AreEqual(typeof(BooleanLiteralNode), expr.GetType());
        var booleanLiteral = (BooleanLiteralNode)expr;

        Assert.AreEqual(value, booleanLiteral.Value);
    }

    // @FIXME: Don't we need to parse the string here?
    [DataTestMethod]
    [DataRow(@"""Hello world!""", "Hello world!")]
    [DataRow(@"""Other plain \"" string""", "Other plain \" string")]
    public void Parse_StringLiteral_ReturnsValidAST(string literal, string expected)
    {
        // Arrange
        var tokens = Lexer.Lex(literal);

        // Act
        var ast = Parser.Parse(tokens);

        // Assert
        Assert.IsNotNull(ast);
        var expr = GetGlobalStatement<ExpressionStatementNode>(ast).Expression;
        Assert.IsNotNull(expr);

        Assert.AreEqual(typeof(StringLiteralNode), expr.GetType());
        var strLiteral = (StringLiteralNode)expr;

        Assert.AreEqual(expected, strLiteral.Value);
    }

    
}
