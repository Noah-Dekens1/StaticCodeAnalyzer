using System.Diagnostics;
using System.Diagnostics.Metrics;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace UnitTests;

[TestClass]
public class LexerTests
{
    private void ValidateTokens(List<Token> actualTokens, List<(TokenKind kind, string Lexeme)> expectedTokens)
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

        var expectedTokens = new List<(TokenKind Kind, string Lexeme)>()
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
}