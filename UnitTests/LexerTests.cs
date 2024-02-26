using System.Diagnostics;
using System.Diagnostics.Metrics;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace UnitTests;

using TokenList = List<(TokenKind kind, string Lexeme)>;

[TestClass]
public class LexerTests
{

    private void ValidateTokens(List<Token> actualTokens, TokenList expectedTokens)
    {
        Assert.AreEqual(expectedTokens.Count, actualTokens.Count);

        for (int i =  0; i < expectedTokens.Count; i++)
        {
            var expectedToken = expectedTokens[i];
            var actualToken = actualTokens[i];

            Assert.AreEqual(expectedToken.kind, actualToken.Kind);
            Assert.AreEqual(expectedToken.Lexeme, actualToken.Lexeme);
        }
    }

    [TestMethod]
    public void Lex_Identifiers_ReturnsIdentifierTokens()
    {
        var lexer = new Lexer("helloWorld55 test_new @class Uppercase T123A");
        var tokens = lexer.Lex();

        var expectedTokens = new TokenList()
        {
            (TokenKind.Identifier, "helloWorld55"),
            (TokenKind.Identifier, "test_new"),
            (TokenKind.Identifier, "@class"),
            (TokenKind.Identifier, "Uppercase"),
            (TokenKind.Identifier, "T123A"),
            (TokenKind.EndOfFile, string.Empty)
        };

        ValidateTokens(tokens, expectedTokens);
    }

    [TestMethod]
    public void Lex_NestedGenerics_ReturnsSeparateToken()
    {
        var lexer = new Lexer("List<List<string>> helloWorld = new()");
        var tokens = lexer.Lex();

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

        ValidateTokens(tokens, expectedTokens);
    }

    [TestMethod]
    public void Lex_NumericalLiterals_ReturnsValidTokens()
    {
        var lexer = new Lexer("36 .3 .74f .1f 63 97F 36.3f 3 483.3 0x4F 0xA3B9 0b00100111 22.2uL 3_000.5F");
        var tokens = lexer.Lex();

        var t = .5;

        var expectedTokens = new TokenList()
        {
            (TokenKind.NumericLiteral, "36"),
            (TokenKind.NumericLiteral, ".3"),
            (TokenKind.NumericLiteral, ".74f"),
            (TokenKind.NumericLiteral, ".1f"),
            (TokenKind.NumericLiteral, "63"),
            (TokenKind.NumericLiteral, "97F"),
            (TokenKind.NumericLiteral, "36.3f"),
            (TokenKind.NumericLiteral, "3"),
            (TokenKind.NumericLiteral, "483.3"),
            (TokenKind.NumericLiteral, "0x4F"),
            (TokenKind.NumericLiteral, "0xA3B9"),
            (TokenKind.NumericLiteral, "0b00100111"),
            (TokenKind.NumericLiteral, "22.2uL"),
            (TokenKind.NumericLiteral, "3_000.5F"),
            (TokenKind.EndOfFile, string.Empty),
        };

        ValidateTokens(tokens, expectedTokens);
    }
}