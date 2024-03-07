using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
public class Parser
{
    private Token[] _input = [];

    private int _index = 0;

    private static readonly Dictionary<char, char> EscapeSequences = new()
    {
        { '\\', '\\' },
        { 'a', '\a' },
        { 'b', '\b' },
        { 'f', '\f' },
        { 'n', '\n' },
        { 'r', '\r' },
        { 't', '\t' },
        { 'v', '\v' },
        { '\'', '\\' }, // Shouldn't this be \' again?
        { '"', '\"' },
        { '0', '\0' },
        // \x and \u \U have been excluded as they have trailing values!
        // not a major issue though as nobody uses them ;)
    };
    public bool CanPeek(int count = 1)
        => _index + count < _input.Length;

    public bool IsAtEnd()
        => PeekCurrent().Kind == TokenKind.EndOfFile || _index >= _input.Length;

    public int Tell()
            => _index;

    public void Seek(int pos)
    {
        _index = pos;
    }

    public Token Consume(int count = 1)
    {
        var oldIndex = _index;
        Seek(_index + count);
        return _input[oldIndex];
    }

    public Token PeekCurrent()
    {
        return _input[_index];
    }

    public Token Peek(int count = 1)
    {
        return CanPeek(count) ? _input[_index + count] : throw new ArgumentOutOfRangeException();
    }

    public bool ConsumeIfMatch(TokenKind c, bool includeConsumed = false)
    {
        int negativeSearch = includeConsumed ? 1 : 0;

        if (Peek(negativeSearch).Kind == c)
        {
            Consume();
            return true;
        }

        return false;
    }

    public int ConsumeIfMatchGreedy(TokenKind c, int minMatch = 0, int maxMatch = int.MaxValue)
    {
        int i = -1;

        while (CanPeek(++i) && Peek(i).Kind == c) ;

        if (i >= minMatch && i <= maxMatch)
            Seek(_index + i);
        else
            i = -1;

        return i;
    }

    public int PeekMatchGreedy(TokenKind c, ref int peekIndex, int minMatch = 0, int maxMatch = int.MaxValue)
    {
        int i = -1;

        while (CanPeek(++i + peekIndex) && Peek(i + peekIndex).Kind == c) ;

        if (i >= minMatch && i <= maxMatch)
            peekIndex += i;
        else
            i = -1;

        return i;
    }

    private static char ResolveEscapeSequence(char c)
    {
        return EscapeSequences.TryGetValue(c, out var resolved) ? resolved : c;
    }

    private static string ParseStringLiteral(string str)
    {
        // Assume only regular string literals for now

        var sb = new StringBuilder();

        for (int i = 1; i < str.Length - 1; i++)
        {
            var c = str[i];

            if (c != '\\')
                sb.Append(str[i]);
            else
                sb.Append(ResolveEscapeSequence(str[++i]));
        }

        return sb.ToString();
    }

    private bool PeekLiteralExpression([MaybeNullWhen(false)] out LiteralExpressionNode literal, Token? providedToken=null)
    {
        var token = providedToken ?? PeekCurrent();
        var kind = token.Kind;

        literal = null;

        if (kind == TokenKind.NumericLiteral)
        {
            //Consume();
            literal = new NumericLiteralNode { Tokens = [token], Value = token.Value };
            return true;
        }
        else if (kind == TokenKind.StringLiteral || kind == TokenKind.InterpolatedStringLiteral)
        {
            literal = new StringLiteralNode { Tokens = [token], Value = ParseStringLiteral(token.Lexeme) };
            return true;
        }
        else if (kind == TokenKind.CharLiteral)
        {
            throw new NotImplementedException();
        }
        else if (kind == TokenKind.TrueKeyword || kind == TokenKind.FalseKeyword)
        {
            var value = kind == TokenKind.TrueKeyword;
            literal = new BooleanLiteralNode { Tokens = [token], Value = value };
            return true;
        }

        return false;
    }

    private static bool IsUnaryOperator(TokenKind kind) => kind switch
    {
        TokenKind.PlusPlus or TokenKind.MinusMinus or TokenKind.Exclamation or TokenKind.Minus or TokenKind.Tilde => true,
        _ => false
    };

    private bool IsIdentifierOrLiteral(Token token)
    {
        if (token.Kind == TokenKind.Identifier)
            return true;

        if (PeekLiteralExpression(out _, token))
            return true;

        return false;
    }

    private ExpressionNode ParseIdentifierOrLiteral()
    {
        var token = Consume();

        if (token.Kind == TokenKind.Identifier)
            return new IdentifierExpression { Identifier = token.Lexeme };
        else
        {
            if (PeekLiteralExpression(out var expr, token))
                return expr;

            throw new Exception("Neither identifier nor literal was found");
        }
    }

    private bool IsUnaryOperator(out bool isPrefix, int peekStart = 0)
    {
        var current = Peek(peekStart + 0);
        bool currentIsOpenParen = current.Kind == TokenKind.OpenParen;
        isPrefix = !IsIdentifierOrLiteral(current) && !currentIsOpenParen;

        return (!isPrefix && currentIsOpenParen) || IsUnaryOperator(current.Kind);
    }

    private UnaryExpressionNode ParseUnaryExpression(bool isPrefix)
    {
        if (isPrefix)
        {
            var op = Consume();

            bool isIncrementOrDecrement = op.Kind == TokenKind.PlusPlus || op.Kind == TokenKind.MinusMinus; // @todo: parse this earlier to an actual op like with binary expressions
            var expr = isIncrementOrDecrement ? ParseIdentifierOrLiteral() : ParseExpression()!; // Either ParseExpression or ParseIdentifierOrLiteral depending on unary operator (generic vs increment/decrement)
            var newNegation = new UnaryNegationNode(expr, true);
            return newNegation;
        }

        return null!;
    }

    private bool IsBinaryOperator(int peekStart=0)
    {
        var kind = Peek(peekStart + 0).Kind;

        switch (kind)
        {
            case TokenKind.Plus:
            case TokenKind.Minus:
            case TokenKind.Asterisk:
            case TokenKind.Slash:
            case TokenKind.Percent:
                // ...
                return true;
            default: return false;
        }
    }

    private BinaryExpressionNode ParseBinaryExpression(ExpressionNode lhs)
    {
        var binaryOperator = Consume(); // @fixme: deal with multi-token tokens (left shift for example is token LessThan & LessThan)

        var rhs = ParseExpression();

        var operators = new Dictionary<TokenKind, Func<BinaryExpressionNode>>
        {
            { TokenKind.Plus,     () => new AddExpressionNode(lhs, rhs) },
            { TokenKind.Minus,    () => new SubtractExpressionNode(lhs, rhs) },
            { TokenKind.Asterisk, () => new MultiplyExpressionNode(lhs, rhs) },
            { TokenKind.Slash,    () => new DivideExpressionNode(lhs, rhs) },
            { TokenKind.Percent,  () => new ModulusExpressionNode(lhs, rhs) },

            { TokenKind.EqualsEquals, () => new EqualsExpressionNode(lhs, rhs) },
            { TokenKind.ExclamationEquals, () => new NotEqualsExpressionNode(lhs, rhs) },
        };

        return operators[binaryOperator.Kind]();
    }

    // @note: pretty much everything in C# is an expression so we probably want to split this up
    private ExpressionNode? ParseExpression(ExpressionNode? possibleLHS=null)
    {
        var token = PeekCurrent();
        var next = Peek(1);
        ExpressionNode? expression = null;

        // 1. Check unary pre- and postfix operators
        // 1a. If found, group them into an ExpressionNode and store them temporarily
        // 2. Check (peek past LHS) for binary operators

        // --or maybe
        // a) An expression can consist out of 1, 2, or 3 parts
        // (Unary, binary, ternary), each operand may be nested
        // for example add (a, b) may be add (4, add (2, 1

        // @todo: check for identifier- and literalexpressions first
        // @todo when finished with an unary operator group the expr and check if there's still an expr available

        if (token.Kind == TokenKind.OpenParen)
        {
            Consume();
            var expr = new ParenthesizedExpression(ParseExpression()!);
            Consume();
            return expr;
        }

        var isLiteral = PeekLiteralExpression(out var literal);
        bool isCurrentTokenIdentifier = token.Kind == TokenKind.Identifier; // maybe? to help with LHS?

        bool isUnary = IsUnaryOperator(out var isPrefix);

        bool isBinary = !isUnary && IsBinaryOperator(1);
        bool isTernary = false;
        int a = 0;
        int b = 0;
        if (isUnary)
        {
            var unaryExpr = ParseUnaryExpression(isPrefix); // may be the final symbol in the expr
            var groupExpr = ParseExpression(unaryExpr);
            return groupExpr ?? unaryExpr;
        }
        else if (isBinary)
        {
            // Does nullability not work in ternary subexpressions?
            ExpressionNode lhs = possibleLHS ?? (ExpressionNode?)literal ?? new IdentifierExpression { Identifier = token.Lexeme };
            Consume();
            return ParseBinaryExpression(lhs);
        }
        else if (isCurrentTokenIdentifier)
        {
            return new IdentifierExpression { Identifier = token.Lexeme };
        }
        else if (isLiteral)
        {
            return literal!;
        }

        return null;
    }

    private AST ParseInternal(Token[] tokens)
    {
        var ast = new AST { Root = new() };

        if (tokens.Length == 0)
            return ast;

        _input = tokens;

        var expr = ParseExpression();

        ast.Root.GlobalStatements.Add(new GlobalStatementNode
        {
            Statement = new ExpressionStatementNode { Expression = expr }
        });

        /*

        while (!IsAtEnd())
        {
            var token = PeekCurrent();

            switch (token.Kind)
            {
                default:
            }

            break;
        }
        */

        return ast;
    }

    public static AST Parse(Token[] tokens)
    {
        var parser = new Parser();
        return parser.ParseInternal(tokens);
    }

    public static AST Parse(List<Token> tokens)
    {
        var parser = new Parser();
        return parser.ParseInternal([.. tokens]); // self parsing will be a pain if I keep using C# 12 features
    }
}
