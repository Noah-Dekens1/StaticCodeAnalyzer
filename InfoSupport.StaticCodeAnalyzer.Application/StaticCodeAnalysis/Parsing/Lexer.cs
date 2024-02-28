using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

using ObservingStringType = (bool IsInterpolated, bool IsVerbatim);
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

[DebuggerDisplay("{Kind} {Lexeme} {Position}")]
public struct Token
{
    public TokenKind Kind { get; set; }
    public string Lexeme { get; set; }
    public Position Position { get; set; }
    public object? Value { get; set; } // Mostly for numeric types
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

    public bool CanPeek(int count = 1)
        => _index + count < _input.Length;

    public bool IsAtEnd()
        => _index >= _input.Length;

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

    public char PeekCurrent()
    {
        return _input[_index];
    }

    public char Peek(int count = 1)
    {
        return CanPeek(count) ? _input[_index + count] : '\0';
    }

    public bool ConsumeIfMatch(char c, bool includeConsumed = false)
    {
        int negativeSearch = includeConsumed ? 1 : 0;

        if (Peek(negativeSearch) == c)
        {
            Consume();
            return true;
        }

        return false;
    }

    private void Emit(TokenKind kind, string content, object? value=null)
    {
        _tokens.Add(new Token { Kind = kind, Lexeme = content, Value = value });
        //Console.WriteLine($"{_tokens[^1].Kind} {_tokens[^1].Lexeme}");
    }

    private void ReadIdentifierOrKeyword()
    {
        var nameBuilder = new StringBuilder();
        bool isFirst = true;

        while (!IsAtEnd())
        {
            char c = PeekCurrent();

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

    private static object? ParseSmallestNumericTypeForInteger(string number, int fromBase)
    {
        // Parse as largest (so the number can fit), then cast downwards
        var parsed = Convert.ToUInt64(number, fromBase);

        if (parsed < int.MaxValue)
            return (int)parsed;

        if (parsed < uint.MaxValue)
            return (uint)parsed;

        if (parsed < long.MaxValue)
            return(long)parsed;

        return parsed;
    }

    private static object? ParseNumericLiteral(string numericLiteral)
    {
        var cleaned = numericLiteral.ToLower().Replace("_", "");
        var suffix = "";

        bool isDecimal = numericLiteral.Contains('.');
        object? result = null;

        bool isHex = cleaned.StartsWith("0x");
        bool isBinary = cleaned.StartsWith("0b");

        for (int i = cleaned.Length - 1; i >= 0; i--)
        {
            var c = cleaned[i];

            if (!isHex && !isBinary && char.IsLetter(c))
            {
                suffix += c;
                cleaned = cleaned.Remove(i);
            }

            if ((isHex || isBinary) && (c == 'u' || c == 'l'))
            {
                suffix += c == 'u' ? 'u' : 'l';
            }
        }

        if (cleaned.Length == 0)
            throw new Exception($"Unable to parse numeric literal of length 0, '{numericLiteral}'");

        if (cleaned[0] == '.')
            cleaned = "0" + cleaned;

        if (isHex)
        {
            var hexStr = cleaned[2..];

            result = ParseSmallestNumericTypeForInteger(hexStr, 16);
        }
        else if (isBinary)
        {
            var binaryStr = cleaned[2..];

            result = ParseSmallestNumericTypeForInteger(binaryStr, 2);
        }
        else
        {
            // Convert.To... doesn't deal with decimal points for integer types so we have to check manually
            if (isDecimal || suffix == "f" || suffix == "m" || suffix == "d")
            {
                bool floatLiteral = suffix == "f";
                bool decimalLiteral = suffix == "m";
                bool doubleLiteral = suffix == "d" || string.IsNullOrWhiteSpace(suffix);

                if (floatLiteral)
                    result = float.Parse(cleaned, CultureInfo.InvariantCulture);

                if (decimalLiteral)
                    result = decimal.Parse(cleaned, CultureInfo.InvariantCulture);

                if (doubleLiteral)
                    result = double.Parse(cleaned, CultureInfo.InvariantCulture);
            }
            else
            {
                result = ParseSmallestNumericTypeForInteger(cleaned, 10);
            }
        }

        return result;
    }

    private (string lexeme, object? value) ReadNumericLiteral()
    {
        bool isHexadecimal = false;
        bool isBinary = false;

        bool isFraction = false;

        var literalBuilder = new StringBuilder();

        if (CanPeek(1))
        {
            var a = Peek(0);
            var b = Peek(1);

            bool maybeHexadecimal = char.ToLower(b) == 'x';
            bool maybeBinary = char.ToLower(b) == 'b';

            if (a == '0' && (maybeHexadecimal || maybeBinary))
            {
                isHexadecimal = maybeHexadecimal;
                isBinary = maybeBinary;

                literalBuilder.Append(a);
                literalBuilder.Append(b);

                Consume();
                Consume();
            }
        }

        while (!IsAtEnd())
        {
            char c = PeekCurrent();
            char lower = char.ToLower(c);

            bool isHexadecimalDigit = (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            List<char> numericSuffixes = ['f', 'd', 'u', 'l', 'm'];

            if (
                (numericSuffixes.Contains(lower) && !isHexadecimal && !isBinary) ||
                lower == 'u' || lower == 'l')
            {
                string suffix = c.ToString();

                if (lower == 'u' && char.ToLower(Peek(1)) == 'l')
                    suffix += Peek(1);
                else if (lower == 'l' && char.ToLower(Peek(1)) == 'u')
                    suffix += Peek(1); // lu suffix seems to be valid?


                literalBuilder.Append(suffix);

                Consume();

                break;
            }

            // Account for .5, 5.5, [5..5] etc In the case of 5..5 we stop reading immediately
            if (c == '.' && CanPeek(1) && char.IsLetterOrDigit(Peek(1)))
            {
                if (isFraction) // may be issues with parsing ranges if this happens?
                    throw new Exception("Encountered more than one dot while lexing a numeric literal");

                isFraction = true;

                literalBuilder.Append(c);
                Consume();
                continue;
            }

            if (!(char.IsDigit(c) || c == '_' || (isHexadecimal && isHexadecimalDigit)))
                break;

            if (isBinary && !(c == '0' || char.ToLower(c) == 'u' || c == '1' || c == '_'))
                throw new Exception("Invalid binary numeric literal");

            literalBuilder.Append(c);
            Consume();
        }

        var numericLiteral = literalBuilder.ToString();

        if (numericLiteral.Last() == '_')
            throw new Exception("Invalid trailing underscore in numeric literal");

        var value = ParseNumericLiteral(numericLiteral);

        return (numericLiteral, value);
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

    public bool ConsumeIfMatchSequence(char[] s, bool includeConsumed = false)
    {
        int negativeSearch = includeConsumed ? 1 : 0;

        if (!CanPeek(s.Length - 1 - negativeSearch))
            return false;

        for (int i = 0; i < s.Length; i++)
            if (s[i] != Peek(i - negativeSearch))
                return false;

        _index += s.Length - negativeSearch;

        return true;
    }

    // @todo: clean this up
    // @fixme: does this work for all cases? needs to be tested
    private string ReadStringLiteral_New(ObservingStringType stringType)
    {
        var literalBuilder = new StringBuilder();
        var stringStart = _index;

        Consume();

        Stack<(bool IsCode, ObservingStringType?)> stack = [];
        stack.Push((false, stringType));

        bool MatchString(out bool interpolated, out bool verbatim, out bool regularQuote, bool includeConsumed)
        {
            interpolated = verbatim = regularQuote = false;

            if (
                ConsumeIfMatchSequence(['@', '$', '"'], includeConsumed) ||
                ConsumeIfMatchSequence(['$', '@', '"'], includeConsumed)
                )
            {
                interpolated = true;
                verbatim = true;
                return true;
            }
            else if (ConsumeIfMatchSequence(['@', '"'], includeConsumed))
            {
                verbatim = true;
                return true;
            }
            else if (ConsumeIfMatchSequence(['$', '"'], includeConsumed))
            {
                interpolated = true;
                return true;
            }
            else if (ConsumeIfMatch('"', includeConsumed))
            {
                regularQuote = true;
                return true;
            }

            return false;
        }

        while (!IsAtEnd() && stack.Count != 0)
        {
            char c = PeekCurrent();

            //literalBuilder.Append(c);

            var (isCode, str) = stack.Peek();
            var (isInterpolated, isVerbatim) = str ?? default;

            if (isCode)
            {
                var possibleMatchStart = _index;
                if (MatchString(out var interpolated, out var verbatim, out var regularQuote, false))
                {
                    //Console.WriteLine("Pushing as sequence was matched " + GetContextForDbg());
                    //LogContextForDbgMulti(possibleMatchStart, false);
                    stack.Push((false, (interpolated, verbatim)));
                }
                else if (ConsumeIfMatch('}'))
                {
                    //Console.WriteLine("Popping interpolation as close brace was found " + GetContextForDbg() + " Next is code? -> " + stack.Peek().IsCode);
                    //LogContextForDbg(true);
                    Debug.Assert(stack.Peek().IsCode);
                    stack.Pop();
                }
                else
                {
                    Consume();
                }

                continue;
            }

            if (!isVerbatim)
            {
                if (ConsumeIfMatch('\\'))
                {
                    if (ConsumeIfMatch('"'))
                    {
                        continue;
                    }
                    else if (ConsumeIfMatch('\\'))
                    {
                        continue;
                    }
                }

                if (ConsumeIfMatch('"'))
                {
                    //Console.WriteLine("Popping as ending \" was found in regular string " + GetContextForDbg() + " Next is code? -> " + stack.Peek().IsCode);
                    //LogContextForDbg(true);
                    stack.Pop();
                    continue;
                }
                else if (!isInterpolated)
                {
                    Consume(); // if interpolated we might still consume the character there
                    continue;
                }
            }
            else
            {

                if (ConsumeIfMatch('"'))
                {
                    if (ConsumeIfMatch('"'))
                    {
                        // how many do we add to the literal builder?
                    }
                    else
                    {
                        // Do we enter/exit a string here?
                        bool shouldEnterString = stack.Peek().IsCode;

                        if (!shouldEnterString)
                        {
                            //Console.WriteLine("Popping as singular ending quote was found in verbatim string " + GetContextForDbg() + " Next is code? -> " + stack.Peek().IsCode);
                            //LogContextForDbg(true);
                            stack.Pop();
                            continue;
                        }
                        else
                            throw new NotImplementedException();
                    }
                }
                else if (!isInterpolated) // We've handled it so consume any character
                {
                    Consume();
                    continue;
                }
            }

            if (isInterpolated)
            {
                if (ConsumeIfMatch('{'))
                {
                    if (!ConsumeIfMatch('{'))
                    {
                        //Console.WriteLine("Pushing interpolation as open brace was found " + GetContextForDbg());
                        //LogContextForDbg(false);
                        stack.Push((true, null));
                    }
                }
                /*
                else if (ConsumeIfMatch('}'))
                {
                    if (!ConsumeIfMatch('}'))
                    {
                        Console.WriteLine("Popping interpolation as close brace was found " + GetContextForDbg());
                        Debug.Assert(stack.Peek().IsCode);
                        stack.Pop();
                    }
                }
                */

                else
                {
                    Consume();
                }
            }
        }
        // This doesn't catch all issues of course (stack could be empty before string completes)
        // But this way we do prevent *some* overruns
        Debug.Assert(stack.Count == 0);
        var stringEnd = _index;

        for (int i = stringStart; i < stringEnd; i++)
        {
            literalBuilder.Append(_input[i]);
        }

        Console.WriteLine(literalBuilder.ToString());

        return literalBuilder.ToString();
    }

    // @fixme: multiline strings and \n!!!
    // @todo: maybe instead of using plain counters we can use a stack of enums to keep track of what kind of string we're in?
    private string ReadStringLiteral(bool isVerbatim, bool isInterpolated)
    {
        return ReadStringLiteral_New((isInterpolated, isVerbatim)); // caution: order changed
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
        while (CanPeek(1))
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

        while (!IsAtEnd())
        {
            char c = Consume();

            if (c == '*' && ConsumeIfMatch('/'))
                break;

            comment.Append(c);
        }

        return;
    }

    public static List<Token> Lex(string content)
    {
        var lexer = new Lexer(content);
        return lexer.LexInternal();
    }

    private List<Token> LexInternal()
    {
        while (!IsAtEnd())
        {
            char c = PeekCurrent();

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

                    if (char.IsDigit(Peek(1)))
                    {
                        var numericLiteral = ReadNumericLiteral();
                        Emit(TokenKind.NumericLiteral, numericLiteral.lexeme, numericLiteral.value);
                        continue;
                    }

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
                    {
                        var result = ReadNumericLiteral();
                        Emit(TokenKind.NumericLiteral, result.lexeme, result.value);
                    }
                    break;
                case '"':
                    if (CanPeek(2) && Peek(1) == '"' && Peek(2) == '"')
                    {
                        throw new NotImplementedException("No support for multiline string literals yet!");
                    }
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
                        while (!IsAtEnd())
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

                        var second = PeekCurrent();
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
