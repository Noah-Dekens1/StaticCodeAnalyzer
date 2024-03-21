﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing.Misc;
using InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

using NuGet.Frameworks;

namespace UnitTests;

[TestClass]
public class ParserTests
{
    private static T GetGlobalStatement<T>(AST ast, int index=0) where T : StatementNode
    {
        return (T)ast.Root.GlobalStatements[index].Statement;
    }

    [DebuggerHidden]
    private static void AssertStandardASTEquals(AST expected, AST actual)
    {
        AstComparator.
            Create()
            .IgnorePropertyOfType<AstNode>(n => n.Tokens)
            .Compare(expected, actual);
    }

    [TestMethod]
    public void Parse_BasicBinaryExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("3 + 4 * 1 - (9 / 3);");
        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(new GlobalStatementNode(
            statement: new ExpressionStatementNode
            (
                expression: new AddExpressionNode(
                    lhs: new NumericLiteralNode(3),
                    rhs: new MultiplyExpressionNode(
                        lhs: new NumericLiteralNode(4),
                        rhs: new SubtractExpressionNode(
                            lhs: new NumericLiteralNode(1),
                            rhs: new ParenthesizedExpressionNode(
                                expr: new DivideExpressionNode(
                                    lhs: new NumericLiteralNode(9),
                                    rhs: new NumericLiteralNode(3)
                                )
                            )
                        )
                    )
                )
            )
        ));

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_BasicBinaryExpressionWithUnaryOperators_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("3 + -4 + 2 + -(someIdentifier * 8 + -(-(4)));");

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();
        
        expected.Root.GlobalStatements.Add(new GlobalStatementNode(
            statement: new ExpressionStatementNode(
                expression: new AddExpressionNode(
                    lhs: new NumericLiteralNode(3),
                    rhs: new AddExpressionNode(
                        lhs: new UnaryNegationNode(new NumericLiteralNode(4)),
                        rhs: new AddExpressionNode(
                            lhs: new NumericLiteralNode(2),
                            rhs: new UnaryNegationNode(
                                expr: new ParenthesizedExpressionNode(
                                    expr: new MultiplyExpressionNode(
                                        lhs: new IdentifierExpression("someIdentifier"),
                                        rhs: new AddExpressionNode(
                                            lhs: new NumericLiteralNode(8),
                                            rhs: new UnaryNegationNode(
                                                expr: new ParenthesizedExpressionNode(
                                                    expr: new UnaryNegationNode(
                                                        expr: new ParenthesizedExpressionNode(
                                                            expr: new NumericLiteralNode(4)
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            )
        ));
        

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_UnaryIncrementDecrementOperators_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("3 - identifier++ + -1;");
        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new SubtractExpressionNode(
                        lhs: new NumericLiteralNode(3),
                        rhs: new AddExpressionNode(
                            lhs: new UnaryIncrementNode(
                                expr: new IdentifierExpression("identifier"),
                                isPrefix: false
                            ),
                            rhs: new UnaryNegationNode(
                                expr: new NumericLiteralNode(1)
                            )
                        )
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_BasicBooleanExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("(a == b && c >= 3) || (a != c && !!!d) && a < j && j <= b && q > e;");
        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        // @note: at the moment the order-of-operations is not supported and will be wrong

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new LogicalOrExpressionNode(
                        lhs: new ParenthesizedExpressionNode(
                            expr: new EqualsExpressionNode(
                                lhs: new IdentifierExpression("a"),
                                rhs: new LogicalAndExpressionNode(
                                    lhs: new IdentifierExpression("b"),
                                    rhs: new GreaterThanEqualsExpressionNode(
                                        lhs: new IdentifierExpression("c"),
                                        rhs: new NumericLiteralNode(3)
                                    )
                                )
                            )
                        ),
                        rhs: new LogicalAndExpressionNode(
                            lhs: new ParenthesizedExpressionNode(
                                expr: new NotEqualsExpressionNode(
                                    lhs: new IdentifierExpression("a"),
                                    rhs: new LogicalAndExpressionNode(
                                        lhs: new IdentifierExpression("c"),
                                        rhs: new UnaryLogicalNotNode(
                                            expr: new UnaryLogicalNotNode(
                                                expr: new UnaryLogicalNotNode(
                                                    new IdentifierExpression("d")
                                                )
                                            )
                                        )
                                    )
                                )
                            ),
                            rhs: new LessThanExpressionNode(
                                lhs: new IdentifierExpression("a"),
                                rhs: new LogicalAndExpressionNode(
                                    lhs: new IdentifierExpression("j"),
                                    rhs: new LessThanEqualsExpressionNode(
                                        lhs: new IdentifierExpression("j"),
                                        rhs: new LogicalAndExpressionNode(
                                            lhs: new IdentifierExpression("b"),
                                            rhs: new GreaterThanExpressionNode(
                                                lhs: new IdentifierExpression("q"),
                                                rhs: new IdentifierExpression("e")
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_BasicVariableDeclaration_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("var test = (a == b && c >= 3) || (a != c && !!!d) && a < j && j <= b && q > e;");
        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("var")),
                    identifier: "test",
                    expression: new LogicalOrExpressionNode(
                        lhs: new ParenthesizedExpressionNode(
                            expr: new EqualsExpressionNode(
                                lhs: new IdentifierExpression("a"),
                                rhs: new LogicalAndExpressionNode(
                                    lhs: new IdentifierExpression("b"),
                                    rhs: new GreaterThanEqualsExpressionNode(
                                        lhs: new IdentifierExpression("c"),
                                        rhs: new NumericLiteralNode(3)
                                    )
                                )
                            )
                        ),
                        rhs: new LogicalAndExpressionNode(
                            lhs: new ParenthesizedExpressionNode(
                                expr: new NotEqualsExpressionNode(
                                    lhs: new IdentifierExpression("a"),
                                    rhs: new LogicalAndExpressionNode(
                                        lhs: new IdentifierExpression("c"),
                                        rhs: new UnaryLogicalNotNode(
                                            expr: new UnaryLogicalNotNode(
                                                expr: new UnaryLogicalNotNode(
                                                    new IdentifierExpression("d")
                                                )
                                            )
                                        )
                                    )
                                )
                            ),
                            rhs: new LessThanExpressionNode(
                                lhs: new IdentifierExpression("a"),
                                rhs: new LogicalAndExpressionNode(
                                    lhs: new IdentifierExpression("j"),
                                    rhs: new LessThanEqualsExpressionNode(
                                        lhs: new IdentifierExpression("j"),
                                        rhs: new LogicalAndExpressionNode(
                                            lhs: new IdentifierExpression("b"),
                                            rhs: new GreaterThanExpressionNode(
                                                lhs: new IdentifierExpression("q"),
                                                rhs: new IdentifierExpression("e")
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_VariableDeclarationWithType_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("int a = 0;");
        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("int")),
                    identifier: "a",
                    expression: new NumericLiteralNode(0)
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_VariableDeclarationWithNonPrimitiveType_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("SomeClass a = new SomeClass();");
        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("SomeClass")),
                    identifier: "a",
                    expression: new ObjectCreationExpressionNode(
                        type: new TypeNode(new IdentifierExpression("SomeClass")),
                        arguments: new ArgumentListNode([])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_BasicBinaryExpression2_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("3 * 4 / 2 + someIdentifier % 3;");
        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new MultiplyExpressionNode(
                        lhs: new NumericLiteralNode(3),
                        rhs: new DivideExpressionNode(
                            lhs: new NumericLiteralNode(4),
                            rhs: new AddExpressionNode(
                                lhs: new NumericLiteralNode(2),
                                rhs: new ModulusExpressionNode(
                                    lhs: new IdentifierExpression("someIdentifier"),
                                    rhs: new NumericLiteralNode(3)
                                )
                            )
                        )
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_IfStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            if (10 > 3 && (5 < 7 || 2 != 3))
                ;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new IfStatementNode(
                    expression: new GreaterThanExpressionNode(
                        lhs: new NumericLiteralNode(10),
                        rhs: new LogicalAndExpressionNode(
                            lhs: new NumericLiteralNode(3),
                            rhs: new ParenthesizedExpressionNode(
                                expr: new LessThanExpressionNode(
                                    lhs: new NumericLiteralNode(5),
                                    rhs: new LogicalOrExpressionNode(
                                        lhs: new NumericLiteralNode(7),
                                        rhs: new NotEqualsExpressionNode(
                                            lhs: new NumericLiteralNode(2),
                                            rhs: new NumericLiteralNode(3)
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    body: new EmptyStatementNode(),
                    elseBody: null
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
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

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new IfStatementNode(
                    expression: new BooleanLiteralNode(true),
                    body: new BlockNode(
                        statements: [
                            new VariableDeclarationStatement(
                                type: new TypeNode(new IdentifierExpression("var")),
                                identifier: "a",
                                expression: new StringLiteralNode("Hello world!")
                            ),
                            new VariableDeclarationStatement(
                                type: new TypeNode(new IdentifierExpression("var")),
                                identifier: "b",
                                expression: new BooleanLiteralNode(true)
                            ),
                            new VariableDeclarationStatement(
                                type: new TypeNode(new IdentifierExpression("var")),
                                identifier: "c",
                                expression: new BooleanLiteralNode(false)
                            ),
                        ]
                    ),
                    elseBody: null
                )
            )
        );

        AssertStandardASTEquals(expected , actual);
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

        var actual = Parser.Parse(tokens);
        
        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new IfStatementNode(
                    expression: new BooleanLiteralNode(true),
                    body: new BlockNode(statements: [new EmptyStatementNode()]),
                    elseBody: new IfStatementNode(
                        expression: new BooleanLiteralNode(false),
                        body: new BlockNode(statements: [new EmptyStatementNode()]),
                        elseBody: new EmptyStatementNode()
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
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

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("var")),
                    identifier: "a",
                    expression: new NumericLiteralNode(0)
                )
            ),
            new GlobalStatementNode(
                statement: new DoStatementNode(
                    condition: new LessThanExpressionNode(
                        lhs: new IdentifierExpression("a"),
                        rhs: new NumericLiteralNode(10)
                    ),
                    body: new BlockNode(
                        statements: [
                            new ExpressionStatementNode(
                                expression: new UnaryIncrementNode(
                                    expr: new IdentifierExpression("a"),
                                    isPrefix: false
                                )
                            )
                        ]
                    )
                )
            )
        ]);

        AssertStandardASTEquals(expected, actual);
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

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ForStatementNode(
                    initializer: new VariableDeclarationStatement(
                        type: new TypeNode(new IdentifierExpression("int")),
                        identifier: "i",
                        expression: new NumericLiteralNode(0)
                    ),
                    condition: new LessThanExpressionNode(
                        lhs: new IdentifierExpression("i"),
                        rhs: new NumericLiteralNode(10)
                    ),
                    iteration: new ExpressionStatementListNode([new ExpressionStatementNode(
                        expression: new UnaryIncrementNode(
                            expr: new IdentifierExpression("i"),
                            isPrefix: false
                        )
                    )]),
                    body: new BlockNode(
                        statements: [new ExpressionStatementNode(
                            expression: new UnaryIncrementNode(
                                expr: new IdentifierExpression("i"),
                                isPrefix: false
                            )
                        )
                    ])
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
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

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("int")),
                    identifier: "i",
                    expression: new NumericLiteralNode(0)
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("int")),
                    identifier: "b",
                    expression: new NumericLiteralNode(10)
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("int")),
                    identifier: "c",
                    expression: new NumericLiteralNode(5)
                )
            ),
            new GlobalStatementNode(
                statement: new ForStatementNode(
                    initializer: new ExpressionStatementListNode([
                        new ExpressionStatementNode(
                            expression: new AssignmentExpressionNode(
                                lhs: new IdentifierExpression("i"),
                                rhs: new NumericLiteralNode(3)
                            )
                        ),
                        new ExpressionStatementNode(
                            expression: new UnaryIncrementNode(
                                expr: new IdentifierExpression("i"),
                                isPrefix: true
                            )
                        ),
                        new ExpressionStatementNode(
                            expression: new UnaryDecrementNode(
                                expr: new IdentifierExpression("i"),
                                isPrefix: false
                            )
                        ),
                        new ExpressionStatementNode(
                            expression: new UnaryIncrementNode(
                                expr: new IdentifierExpression("i"),
                                isPrefix: false
                            )
                        )
                    ]),
                    condition: new LessThanExpressionNode(
                        lhs: new IdentifierExpression("i"),
                        rhs: new NumericLiteralNode(10)
                    ),
                    iteration: new ExpressionStatementListNode([
                        new ExpressionStatementNode(
                            expression: new UnaryIncrementNode(
                                expr: new IdentifierExpression("i"),
                                isPrefix: false
                            )
                        ),
                        new ExpressionStatementNode(
                            expression: new AssignmentExpressionNode(
                                lhs: new IdentifierExpression("c"),
                                rhs: new NumericLiteralNode(0)
                            )
                        )
                    ]),
                    body: new BlockNode([
                        new ExpressionStatementNode(
                            expression: new UnaryDecrementNode(
                                expr: new IdentifierExpression("b"),
                                isPrefix: false
                            )
                        )
                    ])
                )
            )
        ]);

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_EmptyForStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            for (;;)
                ;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ForStatementNode(
                    initializer: new ExpressionStatementListNode([]),
                    condition: null,
                    iteration: new ExpressionStatementListNode([]),
                    body: new EmptyStatementNode()
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_ForEachStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            uint count = 0;
            foreach (var item in test.someList)
                count++;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("uint")),
                    identifier: "count",
                    expression: new NumericLiteralNode(0)
                )
            ),
            new GlobalStatementNode(
                statement: new ForEachStatementNode(
                    variableType: new TypeNode(new IdentifierExpression("var")),
                    variableIdentifier: "item",
                    collection: new MemberAccessExpressionNode(
                        lhs: new IdentifierExpression("test"),
                        identifier: new IdentifierExpression("someList")
                    ),
                    body: new ExpressionStatementNode(
                        expression: new UnaryIncrementNode(
                            expr: new IdentifierExpression("count"),
                            isPrefix: false
                        )
                    )
                )
            )
        ]);

        AssertStandardASTEquals(expected, actual);
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

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("var")),
                    identifier: "problems",
                    expression: new NumericLiteralNode(100)
                )
            ),
            new GlobalStatementNode(
                statement: new WhileStatementNode(
                    condition: new LogicalOrExpressionNode(
                        lhs: new ParenthesizedExpressionNode(
                            expr: new EqualsExpressionNode(
                                lhs: new IdentifierExpression("a"),
                                rhs: new LogicalAndExpressionNode(
                                    lhs: new BooleanLiteralNode(true),
                                    rhs: new LessThanExpressionNode(
                                        lhs: new IdentifierExpression("b"),
                                        rhs: new NumericLiteralNode(4)
                                    )
                                )
                            )
                        ),
                        rhs: new LogicalAndExpressionNode(
                            lhs: new IdentifierExpression("c"),
                            rhs: new ParenthesizedExpressionNode(
                                expr: new GreaterThanExpressionNode(
                                    lhs: new IdentifierExpression("l"),
                                    rhs: new MultiplyExpressionNode(
                                        lhs: new IdentifierExpression("r"),
                                        rhs: new NumericLiteralNode(2)
                                    )
                                )
                            )
                        )
                    ),
                    body: new BlockNode([
                        new ExpressionStatementNode(
                            expression: new AssignmentExpressionNode(
                                lhs: new IdentifierExpression("problems"),
                                rhs: new SubtractExpressionNode(
                                    lhs: new IdentifierExpression("problems"),
                                    rhs: new NumericLiteralNode(1)
                                )
                            )
                        )
                    ])
                )
            )
        ]);

        AssertStandardASTEquals(expected, actual);
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

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new UnaryIncrementNode(
                        expr: new MemberAccessExpressionNode(
                            lhs: new MemberAccessExpressionNode(
                                lhs: new MemberAccessExpressionNode(
                                    lhs: new IdentifierExpression("abc"),
                                    identifier: new IdentifierExpression("def")
                                ),
                                identifier: new IdentifierExpression("g")
                            ),
                            identifier: new IdentifierExpression("hi")
                        ),
                        isPrefix: false
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new UnaryIncrementNode(
                        expr: new MemberAccessExpressionNode(
                            lhs: new MemberAccessExpressionNode(
                                lhs: new MemberAccessExpressionNode(
                                    lhs: new IdentifierExpression("abc"),
                                    identifier: new IdentifierExpression("def")
                                ),
                                identifier: new IdentifierExpression("g")
                            ),
                            identifier: new IdentifierExpression("hi")
                        ),
                        isPrefix: true
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new UnaryIncrementNode(
                        expr: new IdentifierExpression("a"),
                        isPrefix: true
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new UnaryIncrementNode(
                        expr: new IdentifierExpression("a"),
                        isPrefix: false
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new AddExpressionNode(
                        lhs: new UnaryIncrementNode(
                            expr: new MemberAccessExpressionNode(
                                lhs: new IdentifierExpression("a"),
                                identifier: new IdentifierExpression("b")
                            ),
                            isPrefix: false
                        ),
                        rhs: new UnaryNegationNode(
                            expr: new NumericLiteralNode(3)
                        )
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new AssignmentExpressionNode(
                        lhs: new IdentifierExpression("a"),
                        rhs: new UnaryIncrementNode(
                            expr: new IdentifierExpression("b"),
                            isPrefix: true
                        )
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new AssignmentExpressionNode(
                        lhs: new IdentifierExpression("a"),
                        rhs: new AddExpressionNode(
                            lhs: new UnaryIncrementNode(
                                expr: new IdentifierExpression("b"),
                                isPrefix: false
                            ),
                            rhs: new NumericLiteralNode(3)
                        )
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new AssignmentExpressionNode(
                        lhs: new IdentifierExpression("a"),
                        rhs: new MultiplyExpressionNode(
                            lhs: new UnaryIncrementNode(
                                expr: new IdentifierExpression("b"),
                                isPrefix: true
                            ),
                            rhs: new IdentifierExpression("c")
                        )
                    )
                )
            )
        ]);

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_UsingDirective_ShouldReturnValidAST()
    {
        var tokens = Lexer.Lex("""
            using Test = System.CoolStuff.Test;
            using System.Text;
            using System.Runtime.CompilerServices;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.UsingDirectives.AddRange([
            new UsingDirectiveNode(
                ns: new QualifiedNameNode(
                    lhs: new QualifiedNameNode(
                        lhs: new IdentifierExpression("System"),
                        identifier: new IdentifierExpression("CoolStuff")
                    ),
                    identifier: new IdentifierExpression("Test")
                ),
                alias: "Test"
            ),
            new UsingDirectiveNode(
                ns: new QualifiedNameNode(
                    lhs: new IdentifierExpression("System"),
                    identifier: new IdentifierExpression("Text")
                ),
                alias: null
            ),
            new UsingDirectiveNode(
                ns: new QualifiedNameNode(
                    lhs: new QualifiedNameNode(
                        lhs: new IdentifierExpression("System"),
                        identifier: new IdentifierExpression("Runtime")
                    ),
                    identifier: new IdentifierExpression("CompilerServices")
                ),
                alias: null
            )
        ]);

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_SimpleMethodCall_ShouldReturnValidAST()
    {
        var tokens = Lexer.Lex("""
            someObj.MethodCall(3 + a(named: true), 9 - 8 + -1, 5 != 3);
            """);

        var actual = Parser.Parse(tokens);
        
        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new InvocationExpressionNode(
                        lhs: new MemberAccessExpressionNode(
                            lhs: new IdentifierExpression("someObj"),
                            identifier: new IdentifierExpression("MethodCall")
                        ),
                        arguments: new ArgumentListNode([
                            new ArgumentNode(
                                expression: new AddExpressionNode(
                                    lhs: new NumericLiteralNode(3),
                                    rhs: new InvocationExpressionNode(
                                        lhs: new IdentifierExpression("a"),
                                        arguments: new ArgumentListNode([
                                            new ArgumentNode(
                                                expression: new BooleanLiteralNode(true),
                                                name: "named"
                                            )
                                        ])
                                    )
                                ),
                                name: null
                            ),
                            new ArgumentNode(
                                expression: new SubtractExpressionNode(
                                    lhs: new NumericLiteralNode(9),
                                    rhs: new AddExpressionNode(
                                        lhs: new NumericLiteralNode(8),
                                        rhs: new UnaryNegationNode(
                                            expr: new NumericLiteralNode(1)
                                        )
                                    )
                                ),
                                name: null
                            ),
                            new ArgumentNode(
                                expression: new NotEqualsExpressionNode(
                                    lhs: new NumericLiteralNode(5),
                                    rhs: new NumericLiteralNode(3)
                                ),
                                name: null
                            )
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_Class_ShouldReturnValidAST()
    {
        // fields, properties, constructors, methods, inheritance, nested types ...?
        var tokens = Lexer.Lex("""
            internal partial class TestClass : OtherClass
            {
                private int _test = 3;
                private int _test2;
                private int _test3 = 9 - (1 * 2);
                public bool IsValid { get; protected set; } = true;
                public bool OtherProperty { protected get; } = false;
                public bool InitOnly { get; init; }
                public bool ExpressionBodied { get => true; }
                public bool BlockBodied { get { return _field; } private set { _field = true; } }

                protected readonly string _hello;

                public TestClass()
                {
                    _hello = "Hello world!";
                }

                virtual void Test();

                public override string ToString()
                {
                    return _hello;
                }
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.TypeDeclarations.Add(
            new ClassDeclarationNode(
                className: AstUtils.SimpleName("TestClass"),
                parentName: AstUtils.SimpleName("OtherClass"),
                accessModifier: AccessModifier.Internal,
                modifiers: [OptionalModifier.Partial],
                members: [
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [],
                        fieldName: "_test",
                        fieldType: new TypeNode(new IdentifierExpression("int")),
                        value: new NumericLiteralNode(3)
                    ),
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [],
                        fieldName: "_test2",
                        fieldType: new TypeNode(new IdentifierExpression("int")),
                        value: null
                    ),
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [],
                        fieldName: "_test3",
                        fieldType: new TypeNode(new IdentifierExpression("int")),
                        value: new SubtractExpressionNode(
                            lhs: new NumericLiteralNode(9),
                            rhs: new ParenthesizedExpressionNode(
                                expr: new MultiplyExpressionNode(
                                    lhs: new NumericLiteralNode(1),
                                    rhs: new NumericLiteralNode(2)
                                )
                            )
                        )
                    ),
                    new PropertyMemberNode(
                        propertyName: "IsValid",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: null
                        ),
                        setter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Protected,
                            expressionBody: null,
                            blockBody: null
                        ),
                        value: new BooleanLiteralNode(true)
                    ),
                    new PropertyMemberNode(
                        propertyName: "OtherProperty",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Protected,
                            expressionBody: null,
                            blockBody: null
                        ),
                        setter: null,
                        value: new BooleanLiteralNode(false)
                    ),
                    new PropertyMemberNode(
                        propertyName: "InitOnly",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: null
                        ),
                        setter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: null,
                            initOnly: true
                        ),
                        value: null
                    ),
                    new PropertyMemberNode(
                        propertyName: "ExpressionBodied",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.ExpressionBodied,
                            accessModifier: AccessModifier.Public,
                            expressionBody: new BooleanLiteralNode(true),
                            blockBody: null
                        ),
                        setter: null,
                        value: null
                    ),
                    new PropertyMemberNode(
                        propertyName: "BlockBodied",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.BlockBodied,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: new BlockNode([
                                new ReturnStatementNode(new IdentifierExpression("_field"))
                            ])
                        ),
                        setter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.BlockBodied,
                            accessModifier: AccessModifier.Private,
                            expressionBody: null,
                            blockBody: new BlockNode([
                                new ExpressionStatementNode(
                                    expression: new AssignmentExpressionNode(
                                        lhs: new IdentifierExpression("_field"),
                                        rhs: new BooleanLiteralNode(true)
                                    )
                                )
                            ])
                        ),
                        value: null
                    ),
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Protected,
                        modifiers: [OptionalModifier.Readonly],
                        fieldName: "_hello",
                        fieldType: new TypeNode(new IdentifierExpression("string")),
                        value: null
                    ),
                    new ConstructorNode(
                        accessModifier: AccessModifier.Public,
                        parameters: new ParameterListNode([]),
                        body: new BlockNode([
                            new ExpressionStatementNode(
                                expression: new AssignmentExpressionNode(
                                    lhs: new IdentifierExpression("_hello"),
                                    rhs: new StringLiteralNode("Hello world!")
                                )
                            )
                        ])
                    ),
                    new MethodNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [OptionalModifier.Virtual],
                        returnType: new TypeNode(new IdentifierExpression("void")),
                        methodName: AstUtils.SimpleName("Test"),
                        parameters: new ParameterListNode([]),
                        body: null
                    ),
                    new MethodNode(
                        accessModifier: AccessModifier.Public,
                        modifiers: [OptionalModifier.Override],
                        returnType: new TypeNode(new IdentifierExpression("string")),
                        methodName: AstUtils.SimpleName("ToString"),
                        parameters: new ParameterListNode([]),
                        body: new BlockNode([
                            new ReturnStatementNode(new IdentifierExpression("_hello"))
                        ])
                    )
                ]
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_Enum_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            public enum Color : byte
            {
                Red,
                Green = 1,
                Blue
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.TypeDeclarations.Add(
            new EnumDeclarationNode(
                enumName: AstUtils.SimpleName("Color"),
                parentType: AstUtils.SimpleName("byte"),
                accessModifier: AccessModifier.Public,
                modifiers: [],
                members: [
                    new EnumMemberNode(
                        identifier: "Red",
                        value: null
                    ),
                    new EnumMemberNode(
                        identifier: "Green",
                        value: new NumericLiteralNode(1)
                    ),
                    new EnumMemberNode(
                        identifier: "Blue",
                        value: null
                    )
                ]
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_Interface_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            private protected interface ITry
            {
                public string Name { get; protected set; }
                internal void ShouldBe(int a, bool b, ITry c);
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.TypeDeclarations.Add(
            new InterfaceDeclarationNode(
                name: AstUtils.SimpleName("ITry"),
                accessModifier: AccessModifier.PrivateProtected,
                modifiers: [],
                members: [
                    new PropertyMemberNode(
                        propertyName: "Name",
                        propertyType: new TypeNode(new IdentifierExpression("string")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: null
                        ),
                        setter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Protected,
                            expressionBody: null,
                            blockBody: null
                        ),
                        value: null
                    ),
                    new MethodNode(
                        accessModifier: AccessModifier.Internal,
                        modifiers: [],
                        returnType: new TypeNode(new IdentifierExpression("void")),
                        methodName: AstUtils.SimpleName("ShouldBe"),
                        parameters: new ParameterListNode([
                            new ParameterNode(type: new TypeNode(new IdentifierExpression("int")), identifier: "a"),
                            new ParameterNode(type: new TypeNode(new IdentifierExpression("bool")), identifier: "b"),
                            new ParameterNode(type: new TypeNode(new IdentifierExpression("ITry")), identifier: "c"),
                        ]),
                        body: null
                    )
                ]
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_Struct_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            protected internal struct Test
            {
                public int A { get; private set; }
                public static Test Create()
                {
                    var test = new Test();
                    test.A = 100;
                    return test;
                }
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.TypeDeclarations.Add(
            new StructDeclarationNode(
                name: AstUtils.SimpleName("Test"),
                accessModifier: AccessModifier.ProtectedInternal,
                modifiers: [],
                members: [
                    new PropertyMemberNode(
                        propertyName: "A",
                        propertyType: new TypeNode(new IdentifierExpression("int")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: null
                        ),
                        setter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Private,
                            expressionBody: null,
                            blockBody: null
                        ),
                        value: null
                    ),
                    new MethodNode(
                        accessModifier: AccessModifier.Public,
                        modifiers: [OptionalModifier.Static],
                        returnType: new TypeNode(new IdentifierExpression("Test")),
                        methodName: AstUtils.SimpleName("Create"),
                        parameters: new ParameterListNode([]),
                        body: new BlockNode([
                            new VariableDeclarationStatement(
                                type: new TypeNode(new IdentifierExpression("var")),
                                identifier: "test",
                                expression: new ObjectCreationExpressionNode(
                                    type: new TypeNode(new IdentifierExpression("Test")),
                                    arguments: new ArgumentListNode([])
                                )
                            ),
                            new ExpressionStatementNode(
                                new AssignmentExpressionNode(
                                    lhs: new MemberAccessExpressionNode(
                                        lhs: new IdentifierExpression("test"),
                                        identifier: new IdentifierExpression("A")
                                    ),
                                    rhs: new NumericLiteralNode(100)
                                )
                            ),
                            new ReturnStatementNode(
                                new IdentifierExpression("test")
                            )
                        ])
                    )
                ]
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_LocalFunction_ShouldReturnValidAST()
    {
        var tokens = Lexer.Lex("""
            var a = 0;
            void Increment()
            {
                a += 1;
            }
            Console.WriteLine(a);
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("var")),
                    identifier: "a",
                    expression: new NumericLiteralNode(0)
                )
            ),
            new GlobalStatementNode(
                statement: new LocalFunctionDeclarationNode(
                    modifiers: [],
                    name: new IdentifierExpression("Increment"),
                    returnType: new TypeNode(new IdentifierExpression("void")),
                    parameters: new ParameterListNode([]),
                    body: new BlockNode([
                        new ExpressionStatementNode(
                            expression: new AddAssignExpressionNode(
                                lhs: new IdentifierExpression("a"),
                                rhs: new NumericLiteralNode(1)
                            )
                        )
                    ])
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new InvocationExpressionNode(
                        lhs: new MemberAccessExpressionNode(
                            lhs: new IdentifierExpression("Console"),
                            identifier: new IdentifierExpression("WriteLine")
                        ),
                        arguments: new ArgumentListNode([
                            new ArgumentNode(
                                expression: new IdentifierExpression("a"),
                                name: null
                            )
                        ])
                    )
                )
            )
        ]);

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_IndexExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var a = list[0];
            var b = list[-3];
            var c = dict["hello"];
            """);

        var ast = Parser.Parse(tokens);

        var expected = AST.Build();
        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(new TypeNode(new IdentifierExpression("var")), "a", new ElementAccessExpressionNode(
                    lhs: new IdentifierExpression("list"),
                    arguments: new BracketedArgumentList([
                        new ArgumentNode(expression: new IndexExpressionNode(new NumericLiteralNode(0)), name: null)
                    ])
                ))
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(new TypeNode(new IdentifierExpression("var")), "b", new ElementAccessExpressionNode(
                    lhs: new IdentifierExpression("list"),
                    arguments: new BracketedArgumentList([
                        new ArgumentNode(expression: new IndexExpressionNode(new UnaryNegationNode(new NumericLiteralNode(3))), name: null)
                    ])
                ))
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(new TypeNode(new IdentifierExpression("var")), "c", new ElementAccessExpressionNode(
                    lhs: new IdentifierExpression("dict"),
                    arguments: new BracketedArgumentList([
                        new ArgumentNode(expression: new IndexExpressionNode(new StringLiteralNode( "hello")), name: null)
                    ])
                ))
            ),
        ]);

        AssertStandardASTEquals(expected, ast);
    }

    [DataTestMethod]
    [DataRow("1;", 1)]
    [DataRow("3;", 3)]
    [DataRow("173;", 173)]
    [DataRow("74.3f;", 74.3f)]
    public void Parse_NumericLiteral_ReturnsValidAST(string numericLiteral, object value)
    {
        var tokens = Lexer.Lex(numericLiteral);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new NumericLiteralNode(value)
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [DataTestMethod]
    [DataRow("true;", true)]
    [DataRow("false;", false)]
    public void Parse_BooleanLiteral_ReturnsValidAST(string literal, bool value)
    {
        var tokens = Lexer.Lex(literal);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new BooleanLiteralNode(value)
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    // @FIXME: Don't we need to parse the string here?
    [DataTestMethod]
    [DataRow(@"""Hello world!"";", "Hello world!")]
    [DataRow(@"""Other plain \"" string"";", "Other plain \" string")]
    public void Parse_StringLiteral_ReturnsValidAST(string literal, string value)
    {
        var tokens = Lexer.Lex(literal);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new StringLiteralNode(value)
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_BasicGenericType_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            new NameSpace.Other.SomeClass<T1>();
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new ObjectCreationExpressionNode(
                        type: new TypeNode(
                            baseType: AstUtils.ResolveMemberAccess("NameSpace.Other.SomeClass"),
                            typeArguments: new TypeArgumentsNode([
                                new TypeNode(
                                    baseType: new IdentifierExpression("T1")
                                )
                            ])
                        ),
                        arguments: new ArgumentListNode([])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_NestedGenericType_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            SomeClass.SomeMethod<Dictionary<T2, T3>>(true);
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new InvocationExpressionNode(
                        lhs: new MemberAccessExpressionNode(
                            lhs: new IdentifierExpression("SomeClass"),
                            identifier: new GenericNameNode(
                                identifier: new IdentifierExpression("SomeMethod"),
                                typeArguments: new TypeArgumentsNode([
                                    new TypeNode(
                                        baseType: new IdentifierExpression("Dictionary"),
                                        typeArguments: new TypeArgumentsNode([
                                            AstUtils.SimpleNameAsType("T2"),
                                            AstUtils.SimpleNameAsType("T3")
                                        ])
                                    )
                                ])
                            )
                        ),
                        arguments: new ArgumentListNode([
                            new ArgumentNode(
                                expression: new BooleanLiteralNode(true),
                                name: null
                            )
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_GenericLocalFunction_ShouldReturnValidAST()
    {
        var tokens = Lexer.Lex("""
            var a = 0;
            void Increment<GenericType, Dictionary<T1, T2<T3>>>()
            {
                a += 1;
            }
            Console.WriteLine(a);
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(new IdentifierExpression("var")),
                    identifier: "a",
                    expression: new NumericLiteralNode(0)
                )
            ),
            new GlobalStatementNode(
                statement: new LocalFunctionDeclarationNode(
                    modifiers: [],
                    name: new GenericNameNode(
                        identifier: new IdentifierExpression("Increment"),
                        typeArguments: new TypeArgumentsNode([
                            new TypeNode(new IdentifierExpression("GenericType")),
                            new TypeNode(
                                baseType: new IdentifierExpression("Dictionary"),
                                typeArguments: new TypeArgumentsNode([
                                    new TypeNode(new IdentifierExpression("T1")),
                                    new TypeNode(
                                        baseType: new IdentifierExpression("T2"),
                                        typeArguments: new TypeArgumentsNode([
                                            AstUtils.SimpleNameAsType("T3")
                                        ])
                                    ),
                                ])
                            ),
                        ])
                    ),
                    returnType: new TypeNode(new IdentifierExpression("void")),
                    parameters: new ParameterListNode([]),
                    body: new BlockNode([
                        new ExpressionStatementNode(
                            expression: new AddAssignExpressionNode(
                                lhs: new IdentifierExpression("a"),
                                rhs: new NumericLiteralNode(1)
                            )
                        )
                    ])
                )
            ),
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new InvocationExpressionNode(
                        lhs: new MemberAccessExpressionNode(
                            lhs: new IdentifierExpression("Console"),
                            identifier: new IdentifierExpression("WriteLine")
                        ),
                        arguments: new ArgumentListNode([
                            new ArgumentNode(
                                expression: new IdentifierExpression("a"),
                                name: null
                            )
                        ])
                    )
                )
            )
        ]);

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_GenericClass_ShouldReturnValidAST()
    {
        // fields, properties, constructors, methods, inheritance, nested types ...?
        var tokens = Lexer.Lex("""
            internal partial class GenericTestClass<T1, T2> : OtherClass<T1, T2>
            {
                private int _test = 3;
                private int _test2;
                private int _test3 = 9 - (1 * 2);
                private List<string> _fancyStrings = new List<string>();
                public List<string> FancyStrings { get => _fancyStrings; }
                public bool IsValid { get; protected set; } = true;
                public bool OtherProperty { protected get; } = false;
                public bool InitOnly { get; init; }
                public bool ExpressionBodied { get => true; }
                public bool BlockBodied { get { return _field; } private set { _field = true; } }

                protected readonly string _hello;

                public GenericTestClass()
                {
                    _hello = "Hello world!";
                }

                virtual void Test<T1, List<T2>>();

                public override string ToString()
                {
                    return _hello;
                }
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.TypeDeclarations.Add(
            new ClassDeclarationNode(
                className: new GenericNameNode(
                    identifier: new IdentifierExpression("GenericTestClass"),
                    typeArguments: new TypeArgumentsNode([
                        AstUtils.SimpleNameAsType("T1"),
                        AstUtils.SimpleNameAsType("T2")
                    ])
                ),
                parentName: new GenericNameNode(
                    identifier: new IdentifierExpression("OtherClass"),
                    typeArguments: new TypeArgumentsNode([
                        AstUtils.SimpleNameAsType("T1"),
                        AstUtils.SimpleNameAsType("T2")
                    ])
                ),
                accessModifier: AccessModifier.Internal,
                modifiers: [OptionalModifier.Partial],
                members: [
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [],
                        fieldName: "_test",
                        fieldType: new TypeNode(new IdentifierExpression("int")),
                        value: new NumericLiteralNode(3)
                    ),
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [],
                        fieldName: "_test2",
                        fieldType: new TypeNode(new IdentifierExpression("int")),
                        value: null
                    ),
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [],
                        fieldName: "_test3",
                        fieldType: new TypeNode(new IdentifierExpression("int")),
                        value: new SubtractExpressionNode(
                            lhs: new NumericLiteralNode(9),
                            rhs: new ParenthesizedExpressionNode(
                                expr: new MultiplyExpressionNode(
                                    lhs: new NumericLiteralNode(1),
                                    rhs: new NumericLiteralNode(2)
                                )
                            )
                        )
                    ),
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [],
                        fieldName: "_fancyStrings",
                        fieldType: new TypeNode(
                            baseType: new IdentifierExpression("List"),
                            typeArguments: new TypeArgumentsNode([
                                new TypeNode(
                                    baseType: new IdentifierExpression("string")
                                )
                            ])
                        ),
                        value: new ObjectCreationExpressionNode(
                            type: new TypeNode(
                                baseType: new IdentifierExpression("List"),
                                typeArguments: new TypeArgumentsNode([
                                    new TypeNode(
                                        baseType: new IdentifierExpression("string")
                                    )
                                ])
                            ),
                            arguments: new ArgumentListNode([])
                        )
                    ),
                    new PropertyMemberNode(
                        propertyName: "FancyStrings",
                        propertyType: new TypeNode(
                            baseType: new IdentifierExpression("List"),
                            typeArguments: new TypeArgumentsNode([
                                new TypeNode(
                                    baseType: new IdentifierExpression("string")
                                )
                            ])
                        ),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.ExpressionBodied,
                            accessModifier: AccessModifier.Public,
                            expressionBody: new IdentifierExpression("_fancyStrings"),
                            blockBody: null
                        ),
                        setter: null,
                        value: null
                    ),
                    new PropertyMemberNode(
                        propertyName: "IsValid",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: null
                        ),
                        setter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Protected,
                            expressionBody: null,
                            blockBody: null
                        ),
                        value: new BooleanLiteralNode(true)
                    ),
                    new PropertyMemberNode(
                        propertyName: "OtherProperty",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Protected,
                            expressionBody: null,
                            blockBody: null
                        ),
                        setter: null,
                        value: new BooleanLiteralNode(false)
                    ),
                    new PropertyMemberNode(
                        propertyName: "InitOnly",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: null
                        ),
                        setter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.Auto,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: null,
                            initOnly: true
                        ),
                        value: null
                    ),
                    new PropertyMemberNode(
                        propertyName: "ExpressionBodied",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.ExpressionBodied,
                            accessModifier: AccessModifier.Public,
                            expressionBody: new BooleanLiteralNode(true),
                            blockBody: null
                        ),
                        setter: null,
                        value: null
                    ),
                    new PropertyMemberNode(
                        propertyName: "BlockBodied",
                        propertyType: new TypeNode(new IdentifierExpression("bool")),
                        getter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.BlockBodied,
                            accessModifier: AccessModifier.Public,
                            expressionBody: null,
                            blockBody: new BlockNode([
                                new ReturnStatementNode(new IdentifierExpression("_field"))
                            ])
                        ),
                        setter: new PropertyAccessorNode(
                            accessorType: PropertyAccessorType.BlockBodied,
                            accessModifier: AccessModifier.Private,
                            expressionBody: null,
                            blockBody: new BlockNode([
                                new ExpressionStatementNode(
                                    expression: new AssignmentExpressionNode(
                                        lhs: new IdentifierExpression("_field"),
                                        rhs: new BooleanLiteralNode(true)
                                    )
                                )
                            ])
                        ),
                        value: null
                    ),
                    new FieldMemberNode(
                        accessModifier: AccessModifier.Protected,
                        modifiers: [OptionalModifier.Readonly],
                        fieldName: "_hello",
                        fieldType: new TypeNode(new IdentifierExpression("string")),
                        value: null
                    ),
                    new ConstructorNode(
                        accessModifier: AccessModifier.Public,
                        parameters: new ParameterListNode([]),
                        body: new BlockNode([
                            new ExpressionStatementNode(
                                expression: new AssignmentExpressionNode(
                                    lhs: new IdentifierExpression("_hello"),
                                    rhs: new StringLiteralNode("Hello world!")
                                )
                            )
                        ])
                    ),
                    new MethodNode(
                        accessModifier: AccessModifier.Private,
                        modifiers: [OptionalModifier.Virtual],
                        returnType: new TypeNode(new IdentifierExpression("void")),
                        methodName: new GenericNameNode(
                            identifier: new IdentifierExpression("Test"),
                            typeArguments: new TypeArgumentsNode([
                                AstUtils.SimpleNameAsType("T1"),
                                new TypeNode(
                                    baseType: new IdentifierExpression("List"),
                                    typeArguments: new TypeArgumentsNode([
                                        AstUtils.SimpleNameAsType("T2")
                                    ])
                                )
                            ])
                        ),
                        parameters: new ParameterListNode([]),
                        body: null
                    ),
                    new MethodNode(
                        accessModifier: AccessModifier.Public,
                        modifiers: [OptionalModifier.Override],
                        returnType: new TypeNode(new IdentifierExpression("string")),
                        methodName: AstUtils.SimpleName("ToString"),
                        parameters: new ParameterListNode([]),
                        body: new BlockNode([
                            new ReturnStatementNode(new IdentifierExpression("_hello"))
                        ])
                    )
                ]
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_AmbigiousGenerics1_ReturnsValidAST()
    {
        // Example case from 
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure
        // Should parse as a call to F with one argument which is a call to G
        // This is due to the open paren ( acting as a disambiguating token)
        var tokens = Lexer.Lex("""
            F(G<A, B>(7));
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new InvocationExpressionNode(
                        lhs: new IdentifierExpression("F"),
                        arguments: new ArgumentListNode([
                            new ArgumentNode(
                                expression: new InvocationExpressionNode(
                                    lhs: new GenericNameNode(
                                        identifier: new IdentifierExpression("G"),
                                        typeArguments: new TypeArgumentsNode([
                                            AstUtils.SimpleNameAsType("A"),
                                            AstUtils.SimpleNameAsType("B")
                                        ])
                                    ),
                                    arguments: new ArgumentListNode([
                                        new ArgumentNode(
                                            expression: new NumericLiteralNode(7), 
                                            name: null
                                        )
                                    ])
                                ),
                                name: null
                            )
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_AmbigiousGenerics2_ReturnsValidAST()
    {
        // Example case from 
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure
        // Should parse as a call to F with two arguments (G<A and B>7) because there's no disambiguating ( token here
        var tokens = Lexer.Lex("""
            F(G<A, B>7);
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new InvocationExpressionNode(
                        lhs: new IdentifierExpression("F"),
                        arguments: new ArgumentListNode([
                            new ArgumentNode(
                                expression: new LessThanExpressionNode(
                                    lhs: new IdentifierExpression("G"),
                                    rhs: new IdentifierExpression("A")
                                ),
                                name: null
                            ),
                            new ArgumentNode(
                                expression: new GreaterThanExpressionNode(
                                    lhs: new IdentifierExpression("B"),
                                    rhs: new NumericLiteralNode(7)
                                ),
                                name: null
                            )
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_CollectionInitializerWithIndexers_ReturnsValidAST()
    {
        // Example from https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers
        var tokens = Lexer.Lex("""
            var thing = new IndexersExample
            {
                name = "object one",
                [1] = '1',
                [2] = '4',
                [3] = '9',
                Size = Math.PI
            };
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "thing",
                    expression: new ObjectCreationExpressionNode(
                        type: AstUtils.SimpleNameAsType("IndexersExample"),
                        initializer: new CollectionInitializerNode([
                            new IndexedCollectionInitializerNode(
                                key: new IdentifierExpression("name"),
                                value: new StringLiteralNode("object one")
                            ),
                            new IndexedCollectionInitializerNode(
                                key: new NumericLiteralNode(1),
                                value: new CharLiteralNode('1')
                            ),
                            new IndexedCollectionInitializerNode(
                                key: new NumericLiteralNode(2),
                                value: new CharLiteralNode('4')
                            ),
                            new IndexedCollectionInitializerNode(
                                key: new NumericLiteralNode(3),
                                value: new CharLiteralNode('9')
                            ),
                            new IndexedCollectionInitializerNode(
                                key: new IdentifierExpression("Size"),
                                value: AstUtils.ResolveMemberAccess("Math.PI")
                            )
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_RegularCollectionInitializer_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var list = new List<string>() { "hello", "world" };
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "list",
                    expression: new ObjectCreationExpressionNode(
                        type: new TypeNode(
                            baseType: new IdentifierExpression("List"),
                            typeArguments: new TypeArgumentsNode([
                                AstUtils.SimpleNameAsType("string")
                            ])
                        ),
                        initializer: new CollectionInitializerNode([
                            new RegularCollectionInitializerNode(
                                value: new StringLiteralNode("hello")
                            ),
                            new RegularCollectionInitializerNode(
                                value: new StringLiteralNode("world")
                            )
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_ComplexCollectionInitializer_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            Dictionary<char, string> dict = new()
            {
                { 'a', "a" },
                { 'b', "b" },
                { 'c', "c" }
            };
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(
                        baseType: new IdentifierExpression("Dictionary"),
                        typeArguments: new TypeArgumentsNode([
                            AstUtils.SimpleNameAsType("char"),
                            AstUtils.SimpleNameAsType("string")
                        ])
                    ),
                    identifier: "dict",
                    expression: new ObjectCreationExpressionNode(
                        type: null,
                        initializer: new CollectionInitializerNode([
                            new ComplexCollectionInitializerNode([
                                new CharLiteralNode('a'),
                                new StringLiteralNode("a")
                            ]),
                            new ComplexCollectionInitializerNode([
                                new CharLiteralNode('b'),
                                new StringLiteralNode("b")
                            ]),
                            new ComplexCollectionInitializerNode([
                                new CharLiteralNode('c'),
                                new StringLiteralNode("c")
                            ])
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_CollectionExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            List<char> chars = [..basicChars, ..specialChars, 'a', 'b', 'c', ..getRemainingChars()];
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(
                        baseType: new IdentifierExpression("List"),
                        typeArguments: new TypeArgumentsNode([
                            AstUtils.SimpleNameAsType("char")
                        ])
                    ),
                    identifier: "chars",
                    expression: new CollectionExpressionNode([
                        new SpreadElementNode(
                            expression: new IdentifierExpression("basicChars")
                        ),
                        new SpreadElementNode(
                            expression: new IdentifierExpression("specialChars")
                        ),
                        new ExpressionElementNode(
                            expression: new CharLiteralNode('a')
                        ),
                        new ExpressionElementNode(
                            expression: new CharLiteralNode('b')
                        ),
                        new ExpressionElementNode(
                            expression: new CharLiteralNode('c')
                        ),
                        new SpreadElementNode(
                            expression: new InvocationExpressionNode(
                                lhs: new IdentifierExpression("getRemainingChars"),
                                arguments: new ArgumentListNode([])
                            )
                        )
                    ])
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }


    [TestMethod]
    public void Parse_LambdaExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var simpleLambdaExpr1 = () => true;
            var simpleLambdaExpr2 = (a) => a + 1;
            var simpleLambdaExpr3 = (int a) => a + 1;
            var simpleLambdaExpr4 = (int a, int b) => a + b;
            var simpleLambdaExpr5 = (a, b) => a + b;
            var simpleLambdaExpr6 = (int a, int b) => { return a + b; };
            var square = x => x * x;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "simpleLambdaExpr1",
                    expression: new LambdaExpressionNode(
                        parameters: [],
                        body: new BooleanLiteralNode(true)
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "simpleLambdaExpr2",
                    expression: new LambdaExpressionNode(
                        parameters: [new LambdaParameterNode("a")],
                        body: new AddExpressionNode(
                            lhs: new IdentifierExpression("a"),
                            rhs: new NumericLiteralNode(1)
                        )
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "simpleLambdaExpr3",
                    expression: new LambdaExpressionNode(
                        parameters: [new LambdaParameterNode("a", AstUtils.SimpleNameAsType("int"))],
                        body: new AddExpressionNode(
                            lhs: new IdentifierExpression("a"),
                            rhs: new NumericLiteralNode(1)
                        )
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "simpleLambdaExpr4",
                    expression: new LambdaExpressionNode(
                        parameters: [
                            new LambdaParameterNode("a", AstUtils.SimpleNameAsType("int")),
                            new LambdaParameterNode("b", AstUtils.SimpleNameAsType("int"))
                        ],
                        body: new AddExpressionNode(
                            lhs: new IdentifierExpression("a"),
                            rhs: new IdentifierExpression("b")
                        )
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "simpleLambdaExpr5",
                    expression: new LambdaExpressionNode(
                        parameters: [
                            new LambdaParameterNode("a"),
                            new LambdaParameterNode("b")
                        ],
                        body: new AddExpressionNode(
                            lhs: new IdentifierExpression("a"),
                            rhs: new IdentifierExpression("b")
                        )
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "simpleLambdaExpr6",
                    expression: new LambdaExpressionNode(
                        parameters: [
                            new LambdaParameterNode("a", AstUtils.SimpleNameAsType("int")),
                            new LambdaParameterNode("b", AstUtils.SimpleNameAsType("int"))
                        ],
                        body: new BlockNode([
                            new ReturnStatementNode(
                                returnExpression: new AddExpressionNode(
                                    lhs: new IdentifierExpression("a"),
                                    rhs: new IdentifierExpression("b")
                                )
                            )
                        ])
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "square",
                    expression: new LambdaExpressionNode(
                        parameters: [new LambdaParameterNode("x")],
                        body: new MultiplyExpressionNode(
                            lhs: new IdentifierExpression("x"),
                            rhs: new IdentifierExpression("x")
                        )
                    )
                )
            ),
        ]);

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_SwitchStatement_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            switch (a)
            {
                case 1:
                    Console.WriteLine("1");
                case 2:
                {
                    b += 1;
                    Console.WriteLine("2");
                }
                default:
                    Console.WriteLine("Default");
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new SwitchStatementNode(
                    switchExpression: new IdentifierExpression("a"),
                    sections: [
                        new SwitchCaseNode(
                            casePattern: new ConstantPatternNode(
                                value: new NumericLiteralNode(1)
                            ),
                            statements: [
                                new ExpressionStatementNode(
                                    expression: new InvocationExpressionNode(
                                        lhs: AstUtils.ResolveMemberAccess("Console.WriteLine"),
                                        arguments: new ArgumentListNode([
                                            new ArgumentNode(
                                                expression: new StringLiteralNode("1"),
                                                name: null
                                            )
                                        ])
                                    )
                                )
                            ]
                        ),
                        new SwitchCaseNode(
                            casePattern: new ConstantPatternNode(
                                value: new NumericLiteralNode(2)
                            ),
                            statements: [
                                new BlockNode(
                                    statements: [
                                        new ExpressionStatementNode(
                                            expression: new AddAssignExpressionNode(
                                                lhs: new IdentifierExpression("b"),
                                                rhs: new NumericLiteralNode(1)
                                            )
                                        ),
                                        new ExpressionStatementNode(
                                            expression: new InvocationExpressionNode(
                                                lhs: AstUtils.ResolveMemberAccess("Console.WriteLine"),
                                                arguments: new ArgumentListNode([
                                                    new ArgumentNode(
                                                        expression: new StringLiteralNode("2"),
                                                        name: null
                                                    )
                                                ])
                                            )
                                        )
                                    ]
                                )
                            ]
                        ),
                        new SwitchDefaultCaseNode(
                            statements: [
                                new ExpressionStatementNode(
                                    expression: new InvocationExpressionNode(
                                        lhs: AstUtils.ResolveMemberAccess("Console.WriteLine"),
                                        arguments: new ArgumentListNode([
                                            new ArgumentNode(
                                                expression: new StringLiteralNode("Default"),
                                                name: null
                                            )
                                        ])
                                    )
                                )
                            ]
                        ),
                    ]
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_SwitchStatementWithPatterns_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            switch (a)
            {
                case < b:
                case (>= 50) or < -5 or >999:
                    break;
                case not not <= 10:
                default:
                    Console.WriteLine("hello world");
                    break;
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new SwitchStatementNode(
                    switchExpression: new IdentifierExpression("a"),
                    sections: [
                        new SwitchCaseNode(
                            casePattern: new RelationalPatternNode(
                                op: RelationalPatternOperator.LessThan,
                                value: new IdentifierExpression("b")
                            ),
                            statements: []
                        ),
                        new SwitchCaseNode(
                            casePattern: new OrPatternNode(
                                lhs: new ParenthesizedPatternNode(
                                    pattern: new RelationalPatternNode(
                                        RelationalPatternOperator.GreaterThanOrEqual,
                                        value: new NumericLiteralNode(50)
                                    )
                                ),
                                rhs: new OrPatternNode(
                                    lhs: new RelationalPatternNode(
                                        op: RelationalPatternOperator.LessThan,
                                        value: new UnaryNegationNode(
                                            expr: new NumericLiteralNode(5)
                                        )
                                    ),
                                    rhs: new RelationalPatternNode(
                                        op: RelationalPatternOperator.GreaterThan,
                                        value: new NumericLiteralNode(999)
                                    )
                                )
                            ),
                            statements: [
                                new BreakStatementNode()
                            ]
                        ),
                        new SwitchCaseNode(
                            casePattern: new NotPatternNode(
                                pattern: new NotPatternNode(
                                    pattern: new RelationalPatternNode(
                                        op: RelationalPatternOperator.LessThanOrEqual,
                                        value: new NumericLiteralNode(10)
                                    )
                                )
                            ),
                            statements: []
                        ),
                        new SwitchDefaultCaseNode(
                            statements: [
                                new ExpressionStatementNode(
                                    expression: new InvocationExpressionNode(
                                        lhs: AstUtils.ResolveMemberAccess("Console.WriteLine"),
                                        arguments: new ArgumentListNode([
                                            new ArgumentNode(
                                                expression: new StringLiteralNode("hello world"),
                                                name: null
                                            )
                                        ])
                                    )
                                ),
                                new BreakStatementNode()
                            ]
                        )
                    ]
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_SwitchStatementWithWhenClause_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            switch (a)
            {
                case < b when true:
                case (>= 50) or < -5 or >999:
                    break;
                case not not <= 10 when 3 * 3 == 9:
                default:
                    Console.WriteLine("hello world");
                    break;
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new SwitchStatementNode(
                    switchExpression: new IdentifierExpression("a"),
                    sections: [
                        new SwitchCaseNode(
                            casePattern: new RelationalPatternNode(
                                op: RelationalPatternOperator.LessThan,
                                value: new IdentifierExpression("b")
                            ),
                            statements: [],
                            whenClause: new BooleanLiteralNode(true)
                        ),
                        new SwitchCaseNode(
                            casePattern: new OrPatternNode(
                                lhs: new ParenthesizedPatternNode(
                                    pattern: new RelationalPatternNode(
                                        RelationalPatternOperator.GreaterThanOrEqual,
                                        value: new NumericLiteralNode(50)
                                    )
                                ),
                                rhs: new OrPatternNode(
                                    lhs: new RelationalPatternNode(
                                        op: RelationalPatternOperator.LessThan,
                                        value: new UnaryNegationNode(
                                            expr: new NumericLiteralNode(5)
                                        )
                                    ),
                                    rhs: new RelationalPatternNode(
                                        op: RelationalPatternOperator.GreaterThan,
                                        value: new NumericLiteralNode(999)
                                    )
                                )
                            ),
                            statements: [
                                new BreakStatementNode()
                            ]
                        ),
                        new SwitchCaseNode(
                            casePattern: new NotPatternNode(
                                pattern: new NotPatternNode(
                                    pattern: new RelationalPatternNode(
                                        op: RelationalPatternOperator.LessThanOrEqual,
                                        value: new NumericLiteralNode(10)
                                    )
                                )
                            ),
                            statements: [],
                            whenClause: new MultiplyExpressionNode(
                                lhs: new NumericLiteralNode(3),
                                rhs: new EqualsExpressionNode(
                                    lhs: new NumericLiteralNode(3),
                                    rhs: new NumericLiteralNode(9)
                                )
                            )
                        ),
                        new SwitchDefaultCaseNode(
                            statements: [
                                new ExpressionStatementNode(
                                    expression: new InvocationExpressionNode(
                                        lhs: AstUtils.ResolveMemberAccess("Console.WriteLine"),
                                        arguments: new ArgumentListNode([
                                            new ArgumentNode(
                                                expression: new StringLiteralNode("hello world"),
                                                name: null
                                            )
                                        ])
                                    )
                                ),
                                new BreakStatementNode()
                            ]
                        )
                    ]
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_Namespace_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            namespace Test
            {
                class TestClass
                {
                    
                }
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.Namespaces.Add(
            new NamespaceNode(
                name: "Test",
                typeDeclarations: [
                    new ClassDeclarationNode(
                        className: new IdentifierExpression("TestClass"),
                        members: []
                    )
                ]
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_FileScopedNamespace_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            namespace Test;

            class TestClass
            {
                    
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.Namespaces.Add(
            new NamespaceNode(
                name: "Test",
                isFileScoped: true,
                typeDeclarations: [
                    new ClassDeclarationNode(
                        className: new IdentifierExpression("TestClass"),
                        members: []
                    )
                ]
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_NestedNamespaces_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            namespace Test
            {
                class TestClass
                {
                    
                }

                namespace Test2
                {
                    class TestClass2
                    {

                    }
                }
            }
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.Namespaces.Add(
            new NamespaceNode(
                name: "Test",
                typeDeclarations: [
                    new ClassDeclarationNode(
                        className: new IdentifierExpression("TestClass"),
                        members: []
                    )
                ],
                namespaces: [
                    new NamespaceNode(
                        name: "Test2",
                        typeDeclarations: [
                            new ClassDeclarationNode(
                                className: new IdentifierExpression("TestClass2"),
                                members: []
                            )
                        ]
                    )
                ]
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_RegularArray_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            byte[] buffer = new byte[256];
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(
                        baseType: new IdentifierExpression("byte"),
                        arrayType: new ArrayTypeData(rank: null) // omitted
                    ),
                    identifier: "buffer",
                    expression: new ObjectCreationExpressionNode(
                        type: new TypeNode(
                            baseType: new IdentifierExpression("byte"),
                            arrayType: new ArrayTypeData(rank: 256)
                        )
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_SwitchExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var result = 10 switch
            {
                1 => "1",
                2 when true => "2",
                _ => "Invalid"
            };
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "result",
                    expression: new SwitchExpressionNode(
                        switchExpression: new NumericLiteralNode(10),
                        arms: [
                            new SwitchExpressionArmNode(
                                condition: new ConstantPatternNode(new NumericLiteralNode(1)),
                                value: new StringLiteralNode("1")
                            ),
                            new SwitchExpressionArmNode(
                                condition: new ConstantPatternNode(new NumericLiteralNode(2)),
                                value: new StringLiteralNode("2"),
                                whenClause: new BooleanLiteralNode(true)
                            ),
                            new SwitchExpressionArmNode(
                                condition: new DiscardPatternNode(),
                                value: new StringLiteralNode("Invalid")
                            ),
                        ]
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_TernaryExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var result = 5 * 3 < 20 ? 0 : 1;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "result",
                    expression: new MultiplyExpressionNode(
                        lhs: new NumericLiteralNode(5),
                        rhs: new LessThanExpressionNode(
                            lhs: new NumericLiteralNode(3),
                            rhs: new TernaryExpressionNode(
                                condition: new NumericLiteralNode(20),
                                trueExpr: new NumericLiteralNode(0),
                                falseExpr: new NumericLiteralNode(1)
                            )
                        )
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_NestedTernaryExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var result = true ? 1 : isValid ? 2 : 3;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "result",
                    expression: new TernaryExpressionNode(
                        condition: new BooleanLiteralNode(true),
                        trueExpr: new NumericLiteralNode(1),
                        falseExpr: new TernaryExpressionNode(
                            condition: new IdentifierExpression("isValid"),
                            trueExpr: new NumericLiteralNode(2),
                            falseExpr: new NumericLiteralNode(3)
                        )
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_NullableTypes_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            Person? person = null;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: new TypeNode(
                        baseType: new IdentifierExpression("Person"),
                        isNullable: true
                    ),
                    identifier: "person",
                    expression: new NullLiteralNode()
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_NullConditionalMemberAccess_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var name = test!.person?.Name!;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "name",
                    expression: new ConditionalMemberAccessExpressionNode(
                        lhs: new MemberAccessExpressionNode(
                            lhs: new IdentifierExpression("test", true),
                            identifier: new IdentifierExpression("person")
                        ),
                        identifier: new IdentifierExpression("Name", true)
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_NullCoalescing_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            result ??= a ?? b ?? c;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new NullCoalescingAssignmentExpressionNode(
                        lhs: new IdentifierExpression("result"),
                        rhs: new NullCoalescingExpressionNode(
                            lhs: new IdentifierExpression("a"),
                            rhs: new NullCoalescingExpressionNode(
                                lhs: new IdentifierExpression("b"),
                                rhs: new IdentifierExpression("c")
                            )
                        )
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_Cast_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var people = (IList<Person>?)result!;
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "people",
                    expression: new CastExpressionNode(
                        type: new TypeNode(
                            baseType: AstUtils.SimpleName("IList"),
                            typeArguments: new TypeArgumentsNode([
                                new TypeNode(
                                    baseType: AstUtils.SimpleName("Person")
                                )
                            ]),
                            isNullable: true
                        ),
                        expr: new IdentifierExpression("result", true)
                    )
                )
            )
        );

        AssertStandardASTEquals(actual, expected);
    }

    [TestMethod]
    public void Parse_NullableArrayIndex_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var result = array?[5];
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "result",
                    expression: new ConditionalElementAccessExpressionNode(
                        lhs: new IdentifierExpression("array"),
                        arguments: new BracketedArgumentList([
                            new ArgumentNode(
                                expression: new IndexExpressionNode(new NumericLiteralNode(5)),
                                name: null
                            )
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_NameofExpression_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            var n1 = nameof(List);
            var n2 = nameof(List<int>);
            var n3 = nameof(List<int>.Count);
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.AddRange([
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "n1",
                    expression: new NameofExpressionNode(
                        expr: new IdentifierExpression("List")
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "n2",
                    expression: new NameofExpressionNode(
                        expr: new GenericNameNode(
                            identifier: new IdentifierExpression("List"),
                            typeArguments: new TypeArgumentsNode([
                                AstUtils.SimpleNameAsType("int")
                            ])
                        )
                    )
                )
            ),
            new GlobalStatementNode(
                statement: new VariableDeclarationStatement(
                    type: AstUtils.SimpleNameAsType("var"),
                    identifier: "n3",
                    expression: new NameofExpressionNode(
                        expr: new MemberAccessExpressionNode(
                            lhs: new GenericNameNode(
                                identifier: new IdentifierExpression("List"),
                                typeArguments: new TypeArgumentsNode([
                                    AstUtils.SimpleNameAsType("int")
                                ])
                            ),
                            identifier: new IdentifierExpression("Count")
                        )
                    )
                )
            )
        ]);

        AssertStandardASTEquals(expected, actual);
    }

    [TestMethod]
    public void Parse_Sizeof_ReturnsValidAST()
    {
        var tokens = Lexer.Lex("""
            Console.WriteLine(sizeof(int));
            """);

        var actual = Parser.Parse(tokens);

        var expected = AST.Build();

        expected.Root.GlobalStatements.Add(
            new GlobalStatementNode(
                statement: new ExpressionStatementNode(
                    expression: new InvocationExpressionNode(
                        lhs: AstUtils.ResolveMemberAccess("Console.WriteLine"),
                        arguments: new ArgumentListNode([
                            new ArgumentNode(
                                expression: new SizeofExpressionNode(
                                    AstUtils.SimpleNameAsType("int")
                                ),
                                name: null
                            )
                        ])
                    )
                )
            )
        );

        AssertStandardASTEquals(expected, actual);
    }
}
