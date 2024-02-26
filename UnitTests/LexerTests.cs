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
            (TokenKind.Keyword, "string"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.GreaterThan, ">"),
            (TokenKind.Identifier, "helloWorld"),
            (TokenKind.Equals, "="),
            (TokenKind.Keyword, "new"),
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

    }
}