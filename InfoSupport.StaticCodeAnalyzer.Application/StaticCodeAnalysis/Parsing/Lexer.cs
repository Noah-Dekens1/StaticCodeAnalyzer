using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

public enum TokenKind
{
    Identifier,
    Keyword, // Could be split up into specific keywords
    Semicolon,
    Dot,
    DotDot,
    Colon,
    ColonColon,
    OpenBrace,
    CloseBrace,
    OpenParen,
    CloseParen,
    OpenBracket,
    CloseBracket,
    Comma,
    Equals,
    EqualsEquals,
    EqualsGreaterThan,
    LessThan,
    LessThanEquals,
    GreaterThan,
    GreaterThanEquals,
    NumericLiteral,
    StringLiteral,
    InterpolatedStringLiteral,
    Plus,
    PlusPlus,
    PlusEquals,
    Minus,
    MinusMinus,
    MinusEquals,
    Question,
    QuestionQuestion,
    QuestionQuestionEquals,
    CharLiteral,
    Exclamation,
    ExclamationEquals,
    Ampersand,
    AmpersandAmpersand,
    AmpersandEquals,
    Bar, // |
    BarBar,
    BarEquals,
    Percent,
    PercentEquals,
    Caret,
    CaretEquals,
    Asterisk,
    AsteriskEquals,
    Tilde,
    Slash,
    SlashEquals,
    EndOfFile
}

public struct Position
{
    public ulong Line { get; set; }
    public ulong Column { get; set; }
}

public struct Token
{
    public TokenKind Kind { get; set; }
    public string Lexeme { get; set; }
    public Position Position { get; set; }
}

public class Lexer(string fileContent)
{
    private readonly char[] _input = fileContent.ToCharArray();
    private int _index = 0;

    private readonly List<Token> _tokens = [];

    private ulong _line = 1;
    private ulong _column = 0;

    private readonly Dictionary<char, char> _escapeSequences = new()
    {
        { '\\', '\\' },
        { 'a', '\a' },
        { 'b', '\b' },
        { 'f', '\f' },
        { 'n', '\n' },
        { 'r', '\r' },
        { 't', '\t' },
        { 'v', '\v' },
        { '\'', '\\' },
        { '"', '\"' },
        { '0', '\0' },
        // \x and \u \U have been excluded as they have trailing values!
    };

    private readonly List<string> _keywords = [
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while"
    ];

    public List<Token> GetTokens()
    {
        return _tokens;
    }

    public bool CanRead(int count = 1)
        => _index + count < _input.Length;

    public char Consume()
    {
        _column++;

        if (_input[_index] == '\n')
        {
            _line++;
            _column = 0;
        }
        return _input[_index++];
    }

    internal string GetContextForDbg(int lookAround=5)
    {
        var start = Math.Max(_index - lookAround, 0);
        var end = Math.Min(_index + lookAround, _input.Length);
        return new string(_input.Skip(start).Take(end - start).ToArray());
    }

    public char PeekNext()
    {
        return _input[_index];
    }

    public char Peek(int count = 1)
    {
        return CanRead(count) ? _input[_index + count] : '\0';
    }

    public bool ConsumeIfMatch(char c)
    {
        if (PeekNext() == c)
        {
            Consume();
            return true;
        }

        return false;
    }

    private void Emit(TokenKind kind, string content)
    {
        _tokens.Add(new Token { Kind = kind, Lexeme = content });
        //Console.WriteLine($"{_tokens[^1].Kind} {_tokens[^1].Lexeme}");
    }

    private void ReadIdentifierOrKeyword()
    {
        var nameBuilder = new StringBuilder();
        bool isFirst = true;

        while (CanRead(1))
        {
            char c = PeekNext();

            if (!char.IsAsciiLetterOrDigit(c) && c != '_' && !(isFirst && c == '@'))
                break;

            Consume();

            nameBuilder.Append(c);
            isFirst = false;
        }

        string name = nameBuilder.ToString();

        if (_keywords.Contains(name))
        {
            Emit(TokenKind.Keyword, name);
        }
        else
        {
            Emit(TokenKind.Identifier, name);
        }
    }

    private string ReadNumericLiteral()
    {
        bool isHexadecimal = false;
        bool isBinary = false;

        var literalBuilder = new StringBuilder();

        if (CanRead(2))
        {
            var a = Peek(0);
            var b = Peek(1);

            bool maybeHexadecimal = char.ToLower(b) == 'x';
            bool maybeBinary = b == 'b';

            if (a == '0' && (maybeHexadecimal || maybeBinary))
            {
                isHexadecimal = maybeHexadecimal;
                isBinary = maybeBinary;

                Consume();
                Consume();
            }
        }

        while (CanRead(1))
        {
            char c = PeekNext();

            bool isHexadecimalDigit = (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            if (!(char.IsDigit(c) || c == '_' || char.ToLower(c) == 'u' || (isHexadecimal && isHexadecimalDigit)))
                break;

            if (isBinary && !(c == '0' || char.ToLower(c) == 'u' || c == '1' || c == '_'))
                throw new Exception("Invalid binary numeric literal");

            literalBuilder.Append(c);
            Consume();
        }

        var numericLiteral = literalBuilder.ToString();

        if (numericLiteral.Last() == '_')
            throw new Exception("Invalid trailing underscore in numeric literal");

        for (int i = 0; i < numericLiteral.Length - 1; i++)
        {
            if (char.ToLower(numericLiteral[i]) == 'u')
            {
                throw new Exception("Invalid numeric literal, didn't expect 'u'");
            }
        }

        return numericLiteral;
    }

    private char ResolveEscapeSequence()
    {
        var c = Consume();
        // Assume the backslash was already escaped

        if (_escapeSequences.TryGetValue(c, out var escaped))
        {
            return escaped;
        }

        throw new Exception($"Unknown escape sequence \"\\{c}\"");
    }

    // @fixme: multiline strings and \n!!!
    private string ReadStringLiteral(bool isVerbatim, bool isInterpolated)
    {
        var literalBuilder = new StringBuilder();
        literalBuilder.Append(Consume()); // we already checked for "

        int backslashCount = 0;
        int scopeCounter = 0;
        int braceCounter = 0;
        bool isCountingOpen = false;
        int verbatimQuoteCounter = 0;

        void FlushBrace()
        {
            if (braceCounter % 2 != 0)
            {
                // if not even amount of open/close braces we either entered or exited a scope
                if (isCountingOpen)
                    scopeCounter++;
                else
                    scopeCounter--;
            }
            braceCounter = 0;
        }

        while (CanRead(1))
        {
            char c = Consume();

            /*
            if (c == '\\')
            {
                c = ResolveEscapeSequence();
            }
            */ // Don't resolve escape sequences?

            literalBuilder.Append(c);

            if (isInterpolated && (c == '{' || c == '}'))
            {
                if (braceCounter != 0 && ((isCountingOpen && c != '{') || (!isCountingOpen && c != '}')))
                {
                    // SWITCH
                    FlushBrace();
                }
                isCountingOpen = c == '{';
                braceCounter++;
            }
            else if (isInterpolated && braceCounter != 0)
            {
                // we're no longer seeing a sequence of % 
                FlushBrace();
            }

            if (isVerbatim)
            {
                if (c == '"')
                {
                    verbatimQuoteCounter++;
                }
                else
                {
                    verbatimQuoteCounter = 0;
                }
            }

            // @fixme: so many things are horribly wrong here
            // it currently breaks on the nested brace closing in a string ("a}") because it's not a string literal
            // do we need to recursively lex it or something?
            if (c == '"' && (isVerbatim || backslashCount % 2 == 0) && scopeCounter == 0 && (!isVerbatim || (verbatimQuoteCounter % 2 != 0 && PeekNext() != '"')))
                break;


            if (c == '\\')
                backslashCount++;
            else
                backslashCount = 0;
        }

        return literalBuilder.ToString();
    }

    private string ReadCharLiteral()
    {
        var literalBuilder = new StringBuilder();
        literalBuilder.Append('\'');
        char c = Consume();

        literalBuilder.Append(c);

        if (c == '\\')
        {
            literalBuilder.Append(Consume());
        }

        literalBuilder.Append('\'');

        return literalBuilder.ToString();
    }

    private void ReadSingleLineComment()
    {
        var comment = new StringBuilder();
        while (CanRead(1))
        {
            char c = Consume();

            if (c == '\n')
                break;

            comment.Append(c);
        }

        return;
    }

    private void ReadMultiLineComment()
    {
        var comment = new StringBuilder();

        while (CanRead(1))
        {
            char c = Consume();

            if (c == '*' && ConsumeIfMatch('/'))
                break;

            comment.Append(c);
        }

        return;
    }

    public List<Token> Lex()
    {
        while (CanRead())
        {
            char c = PeekNext();

            var singleCharMatch = new Dictionary<char, TokenKind>()
            {
                { ';', TokenKind.Semicolon },
                { '{', TokenKind.OpenBrace },
                { '}', TokenKind.CloseBrace },
                { ',', TokenKind.Comma },
                { '(', TokenKind.OpenParen },
                { ')', TokenKind.CloseParen },
                { '[', TokenKind.OpenBracket },
                { ']', TokenKind.CloseBracket },
                { '~', TokenKind.Tilde },
            };

            if (singleCharMatch.TryGetValue(c, out TokenKind kind))
            {
                Consume();
                Emit(kind, c.ToString());
                continue;
            }

            bool isVerbatimString = c == '@' && // we want to figure this out early to deal with identifiers starting with @
                (Peek(1) == '$' && Peek(2) == '"') || // @$"string" 
                (Peek(1) == '"');                     // @"string"

            switch (c)
            {
                // @todo: Maybe remove simple chars and move them to a lookup table?
                case ' ' or '\t' or '\r' or '\n':
                    Consume();
                    break; // Skip over whitespace
                case ';':
                    Consume();
                    Emit(TokenKind.Semicolon, ";");
                    break;
                case ':':
                    Consume();
                    if (ConsumeIfMatch(':'))
                        Emit(TokenKind.ColonColon, "::");
                    else
                        Emit(TokenKind.Colon, ":");
                    break;
                case '/':
                    Consume();

                    if (ConsumeIfMatch('/'))
                        ReadSingleLineComment();
                    else if (ConsumeIfMatch('*'))
                        ReadMultiLineComment();
                    else if (ConsumeIfMatch('='))
                        Emit(TokenKind.SlashEquals, "/=");
                    else
                        Emit(TokenKind.Slash, "/");
                    break;
                case '=':
                    // Could be =, ==, => (lambda arrow)
                    Consume();

                    if (ConsumeIfMatch('='))
                    {
                        Emit(TokenKind.EqualsEquals, "==");
                    }
                    else if (ConsumeIfMatch('>'))
                    {
                        Emit(TokenKind.EqualsGreaterThan, "=>");
                    }
                    else
                    {
                        Emit(TokenKind.Equals, "=");
                    }
                    break;
                case '.': // @todo: .. tokens
                    Consume();
                    if (ConsumeIfMatch('.'))
                        Emit(TokenKind.DotDot, "..");
                    else
                        Emit(TokenKind.Dot, ".");
                    break;
                case (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or '@' when !isVerbatimString:
                    ReadIdentifierOrKeyword();
                    break;
                case (>= '0' and <= '9'):
                    Emit(TokenKind.NumericLiteral, ReadNumericLiteral());
                    break;
                case '"':
                    Emit(TokenKind.StringLiteral, ReadStringLiteral(false, false));
                    break;
                case '+':
                    Consume();
                    // +, +=, ++
                    if (ConsumeIfMatch('='))
                        Emit(TokenKind.PlusEquals, "+=");
                    else if (ConsumeIfMatch('+'))
                        Emit(TokenKind.PlusPlus, "++");
                    else
                        Emit(TokenKind.Plus, "+");

                    break;
                case '-':
                    Consume();
                    // -, -=, --
                    if (ConsumeIfMatch('='))
                        Emit(TokenKind.MinusEquals, "-=");
                    else if (ConsumeIfMatch('+'))
                        Emit(TokenKind.MinusMinus, "--");
                    else
                        Emit(TokenKind.Minus, "-");

                    break;
                case '?':
                    Consume();
                    // ?, ??, ??=
                    if (ConsumeIfMatch('?'))
                    {
                        if (ConsumeIfMatch('='))
                        {
                            Emit(TokenKind.QuestionQuestionEquals, "??=");
                        }
                        else
                        {
                            Emit(TokenKind.QuestionQuestion, "??");
                        }
                    }
                    else
                        Emit(TokenKind.Question, "?");

                    break;
                case '\'':
                    Consume();
                    Emit(TokenKind.CharLiteral, ReadCharLiteral());
                    break;
                case '!':
                    // !, !=, NOTE: reading !! is up to the parser, the lexer should show it as 2 separate tokens
                    Consume();

                    if (ConsumeIfMatch('='))
                        Emit(TokenKind.ExclamationEquals, "!=");
                    else
                        Emit(TokenKind.Exclamation, "!");

                    break;
                case '&':
                    // & (both unary address-of & bitwise and), &&, &=
                    Consume();

                    if (ConsumeIfMatch('&'))
                        Emit(TokenKind.AmpersandAmpersand, "&&");
                    else if (ConsumeIfMatch('='))
                        Emit(TokenKind.AmpersandEquals, "&=");
                    else
                        Emit(TokenKind.Ampersand, "&");

                    break;
                case '|':
                    Consume();

                    if (ConsumeIfMatch('|'))
                        Emit(TokenKind.BarBar, "||");
                    else if (ConsumeIfMatch('='))
                        Emit(TokenKind.BarEquals, "|=");
                    else 
                        Emit(TokenKind.Bar, "|");

                    break;

                case '%':
                    Consume();

                    if (ConsumeIfMatch('='))
                        Emit(TokenKind.PercentEquals, "%=");
                    else
                        Emit(TokenKind.Percent, "%");

                    break;

                    // @note: for > and < there are no << and >> for shifting as that'd be ambigious with generics (for example List<List<string>>)
                    // so the parser needs to resolve that, not the lexer
                case '>':
                    Consume();

                    if (ConsumeIfMatch('='))
                        Emit(TokenKind.GreaterThanEquals, ">=");
                    else
                        Emit(TokenKind.GreaterThan, ">");

                    break;

                case '<':
                    Consume();

                    if (ConsumeIfMatch('='))
                        Emit(TokenKind.LessThanEquals, "<=");
                    else
                        Emit(TokenKind.LessThan, "<");

                    break;

                case '^':
                    Consume();

                    if (ConsumeIfMatch('='))
                        Emit(TokenKind.CaretEquals, "^=");
                    else
                        Emit(TokenKind.Caret, "^");

                    break;

                case '*':
                    Consume();

                    if (ConsumeIfMatch('='))
                        Emit(TokenKind.AsteriskEquals, "*=");
                    else
                        Emit(TokenKind.Asterisk, "*");

                    break;

                case '#': // possible preprocessor directive (@fixme: find a better way to deal with this)
                    {
                        Consume();
                        // @todo: ensure the line starts with #
                        while (CanRead(1))
                        {
                            if (Consume() == '\n')
                                break;
                        }
                    }
                    break;

                case '$':
                case '@' when isVerbatimString:
                    // $"", $@"", @"", @$""
                    {
                        Consume();
                        bool isInterpolated = c == '$';
                        bool isVerbatim = c == '@'; // @fixme: Do we care about verbatim strings as lexer?

                        var second = PeekNext();
                        var isValid = second == '$' || second == '@' || second == '"';
                        isInterpolated |= second == '$';
                        isVerbatim |= second == '@';

                        bool secondIsQuote = second == '\"';

                        if (!isValid)
                        {
                            throw new Exception(GetContextForDbg());
                        }

                        if (!secondIsQuote && isValid)
                            Consume();

                        var str = ReadStringLiteral(isVerbatim, isInterpolated);
                        Emit(isInterpolated ? TokenKind.InterpolatedStringLiteral : TokenKind.StringLiteral, c.ToString() + (!secondIsQuote ? second.ToString() : "") + str);
                    }

                    break;

                default:
                    throw new NotImplementedException($"Unknown char: {c}, context: {GetContextForDbg()}, after processing {_tokens.Count} tokens");
            }
        }

        Emit(TokenKind.EndOfFile, string.Empty);

        Console.WriteLine($"Successfully finished lexing {_tokens.Count} tokens!");

        return _tokens;
    }
}
