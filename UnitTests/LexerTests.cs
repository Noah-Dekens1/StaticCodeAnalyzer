using System.Diagnostics;
using System.Diagnostics.Metrics;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace UnitTests;

using TokenList = List<(TokenKind Kind, string Lexeme)>;
using TokenListWithValues = List<(TokenKind Kind, string Lexeme, object? Value)>;

[TestClass]
public class LexerTests
{

    private static void ValidateTokens(TokenList expectedTokens, List<Token> actualTokens)
    {
        Assert.AreEqual(expectedTokens.Count, actualTokens.Count);

        for (int i =  0; i < expectedTokens.Count; i++)
        {
            var (kind, lexeme) = expectedTokens[i];
            var actualToken = actualTokens[i];

            Assert.AreEqual(kind, actualToken.Kind);
            Assert.AreEqual(lexeme, actualToken.Lexeme);
        }
    }

    private static void ValidateTokensWithValues(TokenListWithValues expectedTokens, List<Token> actualTokens)
    {
        Assert.AreEqual(expectedTokens.Count, actualTokens.Count);

        for (int i = 0; i < expectedTokens.Count; i++)
        {
            var (kind, lexeme, value) = expectedTokens[i];
            var actualToken = actualTokens[i];

            Assert.AreEqual(kind, actualToken.Kind);
            Assert.AreEqual(lexeme, actualToken.Lexeme);
            Assert.AreEqual(value?.GetType(), actualToken.Value?.GetType());
            Assert.AreEqual(value, actualToken.Value);
        }
    }

    [TestMethod]
    public void Lex_Identifiers_ReturnsIdentifierTokens()
    {
        var tokens = Lexer.Lex("helloWorld55 test_new @class Uppercase T123A");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "helloWorld55"),
            (TokenKind.Identifier, "test_new"),
            (TokenKind.Identifier, "@class"),
            (TokenKind.Identifier, "Uppercase"),
            (TokenKind.Identifier, "T123A"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_NestedGenerics_ReturnsSeparateToken()
    {
        var tokens = Lexer.Lex("List<List<string>> helloWorld = new()");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "List"),
            (TokenKind.LessThan, "<"),
            (TokenKind.Identifier, "List"),
            (TokenKind.LessThan, "<"),
            (TokenKind.StringKeyword, "string"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.Identifier, "helloWorld"),
            (TokenKind.Equals, "="),
            (TokenKind.NewKeyword, "new"),
            (TokenKind.OpenParen, "("),
            (TokenKind.CloseParen, ")"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_NumericalLiterals_ReturnsValidTokens()
    {
        var tokens = Lexer.Lex("0xFFFFFFFFF 36 .3 .74f .1f 63 97F 36.3f 3 483.3 0x4F 0xA3B9 0b00100111 3_000.5F");

        var expectedTokens = new TokenListWithValues()
        {
            (TokenKind.NumericLiteral, "0xFFFFFFFFF", 0xFFFFFFFFF),
            (TokenKind.NumericLiteral, "36", 36),
            (TokenKind.NumericLiteral, ".3", .3),
            (TokenKind.NumericLiteral, ".74f", .74f),
            (TokenKind.NumericLiteral, ".1f", .1f),
            (TokenKind.NumericLiteral, "63", 63),
            (TokenKind.NumericLiteral, "97F", 97F),
            (TokenKind.NumericLiteral, "36.3f", 36.3f),
            (TokenKind.NumericLiteral, "3", 3),
            (TokenKind.NumericLiteral, "483.3", 483.3),
            (TokenKind.NumericLiteral, "0x4F", 0x4F),
            (TokenKind.NumericLiteral, "0xA3B9", 0xA3B9),
            (TokenKind.NumericLiteral, "0b00100111", 0b00100111),
            (TokenKind.NumericLiteral, "3_000.5F", 3_000.5F),
            (TokenKind.EndOfFile, string.Empty, null),
        };

        ValidateTokensWithValues(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_VarContextualKeyword_ReturnsIdentifierToken()
    {
        var tokens = Lexer.Lex(@"var test = ""Hello world!""");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "test"),
            (TokenKind.Equals, "="),
            (TokenKind.StringLiteral, "\"Hello world!\""),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_SimpleStringLiterals_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex(@"""Hello world!"" $""Basic interpolated string {value}"" @""Verbatim string literal"" ");

        var expectedTokens = new TokenList()
        {
            (TokenKind.StringLiteral, @"""Hello world!"""),
            (TokenKind.InterpolatedStringLiteral, @"$""Basic interpolated string {value}"""),
            (TokenKind.StringLiteral, @"@""Verbatim string literal"""),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_CommonOperators_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("3 += 2f * 5/1 - (1^(2%1))");

        var expectedTokens = new TokenList()
        {
            (TokenKind.NumericLiteral, "3"),
            (TokenKind.PlusEquals, "+="),
            (TokenKind.NumericLiteral, "2f"),
            (TokenKind.Asterisk, "*"),
            (TokenKind.NumericLiteral, "5"),
            (TokenKind.Slash, "/"),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.Minus, "-"),
            (TokenKind.OpenParen, "("),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.Caret, "^"),
            (TokenKind.OpenParen, "("),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.Percent, "%"),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_SimpleClass_ReturnsValidTokens()
    {
        var tokens = Lexer.Lex("""
            class Example
            {
                public static void Main()
                {
                    Console.WriteLine("Hello world!");
                }
            }
        """);

        var expectedTokens = new TokenList()
        {
            (TokenKind.ClassKeyword, "class"),
            (TokenKind.Identifier, "Example"),
            (TokenKind.OpenBrace, "{"),
            (TokenKind.PublicKeyword, "public"),
            (TokenKind.StaticKeyword, "static"),
            (TokenKind.VoidKeyword, "void"),
            (TokenKind.Identifier, "Main"),
            (TokenKind.OpenParen, "("),
            (TokenKind.CloseParen, ")"),
            (TokenKind.OpenBrace, "{"),
            (TokenKind.Identifier, "Console"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "WriteLine"),
            (TokenKind.OpenParen, "("),
            (TokenKind.StringLiteral, "\"Hello world!\""),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.CloseBrace, "}"),
            (TokenKind.CloseBrace, "}"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [DataTestMethod]
    [DataRow("\"\";")] // empty string
    [DataRow("\"::{645FF040-5081-101B-9F08-00AA002F954E}\";")]
    // the ending """ isn't a multiline string, it's a verbatim "" escape and an end quote "
    [DataRow(""""@"""Hello world!""";"""")]

    public void Lex_StringLiterals_ReturnCorrectTokens(string str)
    {
        var tokens = Lexer.Lex(str);

        var expectedTokens = new TokenList()
        {
            (TokenKind.StringLiteral, str[..^1]),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [DataTestMethod]
    [DataRow(""""$$"""Hello world! {{"interpolated!"}}""";"""")]
    [DataRow("""""$$$$$"""" Hello {{{{{@$"multiline {"world!" + """}}""" + $"""{"Test!" + @" ""abc"" "}"""}"}}}}} """;"""";""""")]
    [DataRow("""$"t \" {"simple"} {{ }}\"\"";""")]
    [DataRow("""$"Hello {"nested }"} string!";""")]
    [DataRow("""$"{{ { ("regular", $@"}}""{$"Hello {"nested }"} string!"}")}";""")]
    [DataRow("""@$"{{ {"hello"} {{}} {$"{"hello" + $"{"a}"}"}"} ""Hello 1""  ";""")]
    [DataRow(""""@$"/s ""{Path.Combine("", true ? "SetFilesAsDefault.reg" : "UnsetFilesAsDefault.reg")}""";"""")]
    public void Lex_ComplexInterpolatedStringLiterals_ReturnCorrectTokens(string str)
    {
        var tokens = Lexer.Lex(str);

        var expectedTokens = new TokenList()
        {
            (TokenKind.InterpolatedStringLiteral, str[..^1]),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    [DataRow("'c'")]
    [DataRow("'a'")]
    [DataRow("'\\\"'")]
    public void Lex_CharLiterals_ReturnsLiteralTokens(string c)
    {
        var tokens = Lexer.Lex(c);

        var expectedTokens = new TokenList()
        {
            (TokenKind.CharLiteral, c),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_ComplexExpression_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("var result = 3 + (4 * 2) - 10 / 2;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "result"),
            (TokenKind.Equals, "="),
            (TokenKind.NumericLiteral, "3"),
            (TokenKind.Plus, "+"),
            (TokenKind.OpenParen, "("),
            (TokenKind.NumericLiteral, "4"),
            (TokenKind.Asterisk, "*"),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Minus, "-"),
            (TokenKind.NumericLiteral, "10"),
            (TokenKind.Slash, "/"),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_BooleanAndNullKeywords_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("bool isValid = true; object obj = null;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.BoolKeyword, "bool"),
            (TokenKind.Identifier, "isValid"),
            (TokenKind.Equals, "="),
            (TokenKind.TrueKeyword, "true"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.ObjectKeyword, "object"),
            (TokenKind.Identifier, "obj"),
            (TokenKind.Equals, "="),
            (TokenKind.NullKeyword, "null"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_Ternary_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("bool result = x > 5 ? true : false;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.BoolKeyword, "bool"),
            (TokenKind.Identifier, "result"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "x"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.NumericLiteral, "5"),
            (TokenKind.Question, "?"),
            (TokenKind.TrueKeyword, "true"),
            (TokenKind.Colon, ":"),
            (TokenKind.FalseKeyword, "false"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_LogicalOperators_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("bool andResult = true && false; bool orResult = true || false;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.BoolKeyword, "bool"),
            (TokenKind.Identifier, "andResult"),
            (TokenKind.Equals, "="),
            (TokenKind.TrueKeyword, "true"),
            (TokenKind.AmpersandAmpersand, "&&"),
            (TokenKind.FalseKeyword, "false"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.BoolKeyword, "bool"),
            (TokenKind.Identifier, "orResult"),
            (TokenKind.Equals, "="),
            (TokenKind.TrueKeyword, "true"),
            (TokenKind.BarBar, "||"),
            (TokenKind.FalseKeyword, "false"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_ArrayAndIndexerAccess_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("int[] numbers = new int[5]; var firstNumber = numbers[0];");

        var expectedTokens = new TokenList()
        {
            (TokenKind.IntKeyword, "int"),
            (TokenKind.OpenBracket, "["),
            (TokenKind.CloseBracket, "]"),
            (TokenKind.Identifier, "numbers"),
            (TokenKind.Equals, "="),
            (TokenKind.NewKeyword, "new"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.OpenBracket, "["),
            (TokenKind.NumericLiteral, "5"),
            (TokenKind.CloseBracket, "]"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "firstNumber"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "numbers"),
            (TokenKind.OpenBracket, "["),
            (TokenKind.NumericLiteral, "0"),
            (TokenKind.CloseBracket, "]"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_PatternMatching_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("if (obj is string str) { Console.WriteLine(str); }");

        var expectedTokens = new TokenList()
        {
            (TokenKind.IfKeyword, "if"),
            (TokenKind.OpenParen, "("),
            (TokenKind.Identifier, "obj"),
            (TokenKind.IsKeyword, "is"),
            (TokenKind.StringKeyword, "string"),
            (TokenKind.Identifier, "str"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.OpenBrace, "{"),
            (TokenKind.Identifier, "Console"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "WriteLine"),
            (TokenKind.OpenParen, "("),
            (TokenKind.Identifier, "str"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.CloseBrace, "}"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_LocalFunctions_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("void OuterMethod() { int LocalFunction(int x) => x * 2; }");

        var expectedTokens = new TokenList()
        {
            (TokenKind.VoidKeyword, "void"),
            (TokenKind.Identifier, "OuterMethod"),
            (TokenKind.OpenParen, "("),
            (TokenKind.CloseParen, ")"),
            (TokenKind.OpenBrace, "{"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "LocalFunction"),
            (TokenKind.OpenParen, "("),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "x"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.EqualsGreaterThan, "=>"),
            (TokenKind.Identifier, "x"),
            (TokenKind.Asterisk, "*"),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.CloseBrace, "}"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_TupleDeconstruction_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("(int a, int b) = (1, 2);");

        var expectedTokens = new TokenList()
        {
            (TokenKind.OpenParen, "("),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "a"),
            (TokenKind.Comma, ","),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "b"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Equals, "="),
            (TokenKind.OpenParen, "("),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.Comma, ","),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_SwitchExpressions_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("var result = x switch { 1 => \"one\", _ => \"many\" };");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "result"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "x"),
            (TokenKind.SwitchKeyword, "switch"),
            (TokenKind.OpenBrace, "{"),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.EqualsGreaterThan, "=>"),
            (TokenKind.StringLiteral, "\"one\""),
            (TokenKind.Comma, ","),
            (TokenKind.Identifier, "_"),
            (TokenKind.EqualsGreaterThan, "=>"),
            (TokenKind.StringLiteral, "\"many\""),
            (TokenKind.CloseBrace, "}"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_RecordTypes_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("public record Person(string Name, int Age);");

        var expectedTokens = new TokenList()
        {
            (TokenKind.PublicKeyword, "public"),
            (TokenKind.Identifier, "record"), // contextual keyword
            (TokenKind.Identifier, "Person"),
            (TokenKind.OpenParen, "("),
            (TokenKind.StringKeyword, "string"),
            (TokenKind.Identifier, "Name"),
            (TokenKind.Comma, ","),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "Age"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_NullableTypes_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("int? nullableInt = null;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Question, "?"),
            (TokenKind.Identifier, "nullableInt"),
            (TokenKind.Equals, "="),
            (TokenKind.NullKeyword, "null"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_NullCoalescingOperator_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("var result = nullableInt ?? -1;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "result"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "nullableInt"),
            (TokenKind.QuestionQuestion, "??"),
            (TokenKind.Minus, "-"),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_NullConditionalOperators_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("var length = someObject?.Length; var firstItem = someList?[0];");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "length"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "someObject"),
            (TokenKind.Question, "?"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "Length"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "firstItem"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "someList"),
            (TokenKind.Question, "?"),
            (TokenKind.OpenBracket, "["),
            (TokenKind.NumericLiteral, "0"),
            (TokenKind.CloseBracket, "]"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_NullCoalescingAssignmentOperator_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("someObject ??= new Object();");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "someObject"),
            (TokenKind.QuestionQuestionEquals, "??="),
            (TokenKind.NewKeyword, "new"),
            (TokenKind.Identifier, "Object"),
            (TokenKind.OpenParen, "("),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_BitwiseOperations_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("var flags = Flag.Read | Flag.Write & Flag.Execute;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "flags"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "Flag"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "Read"),
            (TokenKind.Bar, "|"),
            (TokenKind.Identifier, "Flag"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "Write"),
            (TokenKind.Ampersand, "&"),
            (TokenKind.Identifier, "Flag"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "Execute"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_GenericTypeDeclarations_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("List<int> numbers = new List<int>();");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "List"),
            (TokenKind.LessThan, "<"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.Identifier, "numbers"),
            (TokenKind.Equals, "="),
            (TokenKind.NewKeyword, "new"),
            (TokenKind.Identifier, "List"),
            (TokenKind.LessThan, "<"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.OpenParen, "("),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_GenericMethodInvocations_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("var result = Enumerable.Empty<string>();");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "result"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "Enumerable"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "Empty"),
            (TokenKind.LessThan, "<"),
            (TokenKind.StringKeyword, "string"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.OpenParen, "("),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_ComplexGenerics_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("Dictionary<string, List<int>> complexDict = new Dictionary<string, List<int>>();");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "Dictionary"),
            (TokenKind.LessThan, "<"),
            (TokenKind.StringKeyword, "string"),
            (TokenKind.Comma, ","),
            (TokenKind.Identifier, "List"),
            (TokenKind.LessThan, "<"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.Identifier, "complexDict"),
            (TokenKind.Equals, "="),
            (TokenKind.NewKeyword, "new"),
            (TokenKind.Identifier, "Dictionary"),
            (TokenKind.LessThan, "<"),
            (TokenKind.StringKeyword, "string"),
            (TokenKind.Comma, ","),
            (TokenKind.Identifier, "List"),
            (TokenKind.LessThan, "<"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.OpenParen, "("),
            (TokenKind.CloseParen, ")"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_IncrementDecrementOperators_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("x++; y--;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "x"),
            (TokenKind.PlusPlus, "++"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "y"),
            (TokenKind.MinusMinus, "--"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_CompoundAssignmentOperators_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("x += 10; y -= 5; z *= 2; w /= 2; a &= 1; b |= 2; c ^= 3;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "x"),
            (TokenKind.PlusEquals, "+="),
            (TokenKind.NumericLiteral, "10"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "y"),
            (TokenKind.MinusEquals, "-="),
            (TokenKind.NumericLiteral, "5"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "z"),
            (TokenKind.AsteriskEquals, "*="),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "w"),
            (TokenKind.SlashEquals, "/="),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "a"),
            (TokenKind.AmpersandEquals, "&="),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "b"),
            (TokenKind.BarEquals, "|="),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "c"),
            (TokenKind.CaretEquals, "^="),
            (TokenKind.NumericLiteral, "3"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_ComparisonLogicalOperators_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("if (x == y && a != b || c > d && e < f) { }");

        var expectedTokens = new TokenList()
        {
            (TokenKind.IfKeyword, "if"),
            (TokenKind.OpenParen, "("),
            (TokenKind.Identifier, "x"),
            (TokenKind.EqualsEquals, "=="),
            (TokenKind.Identifier, "y"),
            (TokenKind.AmpersandAmpersand, "&&"),
            (TokenKind.Identifier, "a"),
            (TokenKind.ExclamationEquals, "!="),
            (TokenKind.Identifier, "b"),
            (TokenKind.BarBar, "||"),
            (TokenKind.Identifier, "c"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.Identifier, "d"),
            (TokenKind.AmpersandAmpersand, "&&"),
            (TokenKind.Identifier, "e"),
            (TokenKind.LessThan, "<"),
            (TokenKind.Identifier, "f"),
            (TokenKind.CloseParen, ")"),
            (TokenKind.OpenBrace, "{"),
            (TokenKind.CloseBrace, "}"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_LambdaExpressions_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("Func<int, int> square = x => x * x;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "Func"),
            (TokenKind.LessThan, "<"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Comma, ","),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.Identifier, "square"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "x"),
            (TokenKind.EqualsGreaterThan, "=>"),
            (TokenKind.Identifier, "x"),
            (TokenKind.Asterisk, "*"),
            (TokenKind.Identifier, "x"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_UnaryNegation_ReturnsCorrectToken()
    {
        var tokens = Lexer.Lex("bool isNotTrue = !true;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.BoolKeyword, "bool"),
            (TokenKind.Identifier, "isNotTrue"),
            (TokenKind.Equals, "="),
            (TokenKind.Exclamation, "!"),
            (TokenKind.TrueKeyword, "true"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_RangeOperator_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("var subset = numbers[1..4];");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "subset"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "numbers"),
            (TokenKind.OpenBracket, "["),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.DotDot, ".."),
            (TokenKind.NumericLiteral, "4"),
            (TokenKind.CloseBracket, "]"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_SingleLineComment_IgnoresComment()
    {
        var tokens = Lexer.Lex("int x = 1; // This is a comment\nint y = 2;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "x"),
            (TokenKind.Equals, "="),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "y"),
            (TokenKind.Equals, "="),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_MultiLineComment_IgnoresComment()
    {
        var tokens = Lexer.Lex("int x = 1; /* This is a\nmulti-line comment */int y = 2;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "x"),
            (TokenKind.Equals, "="),
            (TokenKind.NumericLiteral, "1"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.IntKeyword, "int"),
            (TokenKind.Identifier, "y"),
            (TokenKind.Equals, "="),
            (TokenKind.NumericLiteral, "2"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_ColonColonOperator_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("var global = global::System.DateTime.Now;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "var"),
            (TokenKind.Identifier, "global"),
            (TokenKind.Equals, "="),
            (TokenKind.Identifier, "global"),
            (TokenKind.ColonColon, "::"),
            (TokenKind.Identifier, "System"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "DateTime"),
            (TokenKind.Dot, "."),
            (TokenKind.Identifier, "Now"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

    [TestMethod]
    public void Lex_OtherOperators_ReturnsCorrectTokens()
    {
        var tokens = Lexer.Lex("x %= y; a >= b; c <= d;");

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "x"),
            (TokenKind.PercentEquals, "%="),
            (TokenKind.Identifier, "y"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "a"),
            (TokenKind.GreaterThanEquals, ">="),
            (TokenKind.Identifier, "b"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.Identifier, "c"),
            (TokenKind.LessThanEquals, "<="),
            (TokenKind.Identifier, "d"),
            (TokenKind.Semicolon, ";"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(expectedTokens, tokens);
    }

}