using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
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

    [DebuggerHidden]
    public bool CanPeek(int count = 1)
        => _index + count < _input.Length;

    [DebuggerHidden]
    public bool IsAtEnd()
        => PeekCurrent().Kind == TokenKind.EndOfFile || _index >= _input.Length;

    public int Tell()
            => _index;

    public void Seek(int pos)
    {
        _index = pos;
    }

    [DebuggerHidden]
    public Token Consume(int count = 1)
    {
        var oldIndex = _index;
        Seek(_index + count);
        return _input[oldIndex];
    }

    [DebuggerHidden]
    public void Expect(TokenKind expected)
    {
        var actual = Consume().Kind;
        if (actual != expected)
            throw new Exception($"Expected {expected} but got {actual}");
    }

    [DebuggerHidden]
    [Pure]
    public Token PeekCurrent()
    {
        return CanPeek(0) ? _input[_index] : new Token();
    }

    [DebuggerHidden]
    public Token Peek(int count = 1)
    {
        return CanPeek(count) ? _input[_index + count] : throw new ArgumentOutOfRangeException();
    }

    [DebuggerHidden]
    public Token PeekSafe(int count = 1)
    {
        const int invalidTokenKind = 9999999; // To create an invalid token that will never match any case
                                              // but prevent needing dozens of null-checks
        return CanPeek(count) ? _input[_index + count] : new Token { Kind = (TokenKind)invalidTokenKind };
    }

    [DebuggerHidden]
    public bool Matches(TokenKind kind)
    {
        return PeekCurrent().Kind == kind;
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

        var next = CanPeek(1) ? Peek(peekStart + 1) : new Token();
        var isPostfix = !isPrefix && (next.Kind == TokenKind.PlusPlus || next.Kind == TokenKind.MinusMinus);

        return (isPrefix && IsUnaryOperator(current.Kind)) || isPostfix;
    }

    private UnaryExpressionNode ParseUnaryExpression(bool isPrefix)
    {
        if (isPrefix)
        {
            var op = Consume();

            bool isIncrementOrDecrement = op.Kind == TokenKind.PlusPlus || op.Kind == TokenKind.MinusMinus; // @todo: parse this earlier to an actual op like with binary expressions
            var expr = isIncrementOrDecrement ? ParseIdentifierOrLiteral() : ParseExpression(null, true)!; // Either ParseExpression or ParseIdentifierOrLiteral depending on unary operator (generic vs increment/decrement)
            UnaryExpressionNode? result = null;

            switch (op.Kind)
            {
                case TokenKind.PlusPlus:
                    result = new UnaryIncrementNode(expr, isPrefix);
                    break;
                case TokenKind.MinusMinus:
                    result = new UnaryDecrementNode(expr, isPrefix);
                    break;
                case TokenKind.Minus:
                    result = new UnaryNegationNode(expr);
                    break;
                case TokenKind.Exclamation:
                    result = new UnaryLogicalNotNode(expr);
                    break;
            }

            return result ?? throw new Exception("Couldn't resolve unary prefix expression");
        }
        else
        {
            var identifierOrLiteral = ParseIdentifierOrLiteral();
            var op = Consume();

            UnaryExpressionNode? result = null;

            switch (op.Kind)
            {
                case TokenKind.PlusPlus:
                    result = new UnaryIncrementNode(identifierOrLiteral, false);
                    break;
                case TokenKind.MinusMinus:
                    result = new UnaryDecrementNode(identifierOrLiteral, false);
                    break;
            }

            return result ?? throw new Exception("Couldn't resolve unary postfix expression");
        }
    }

    private bool IsBinaryOperator(int peekStart=0)
    {
        var kind = PeekSafe(peekStart + 0).Kind;

        switch (kind)
        {
            // Arithmetic operators
            case TokenKind.Plus:
            case TokenKind.Minus:
            case TokenKind.Asterisk:
            case TokenKind.Slash:
            case TokenKind.Percent:

            // Boolean/comparison operators
            case TokenKind.EqualsEquals:
            case TokenKind.ExclamationEquals:
            case TokenKind.GreaterThan:
            case TokenKind.GreaterThanEquals:
            case TokenKind.LessThan:
            case TokenKind.LessThanEquals:
            case TokenKind.AmpersandAmpersand:
            case TokenKind.BarBar:
                // ...
                return true;
            default: return false;
        }
    }

    private BinaryExpressionNode ParseBinaryExpression(ExpressionNode lhs)
    {
        var binaryOperator = Consume(); // @fixme: deal with multi-token tokens (left shift for example is token LessThan & LessThan)

        var rhs = ParseExpression()!;

        var operators = new Dictionary<TokenKind, Func<BinaryExpressionNode>>
        {
            { TokenKind.Plus,                () => new AddExpressionNode(lhs, rhs) },
            { TokenKind.Minus,               () => new SubtractExpressionNode(lhs, rhs) },
            { TokenKind.Asterisk,            () => new MultiplyExpressionNode(lhs, rhs) },
            { TokenKind.Slash,               () => new DivideExpressionNode(lhs, rhs) },
            { TokenKind.Percent,             () => new ModulusExpressionNode(lhs, rhs) },

            { TokenKind.EqualsEquals,        () => new EqualsExpressionNode(lhs, rhs) },
            { TokenKind.ExclamationEquals,   () => new NotEqualsExpressionNode(lhs, rhs) },
            { TokenKind.GreaterThan,         () => new GreaterThanExpressionNode(lhs, rhs) },
            { TokenKind.GreaterThanEquals,   () => new GreaterThanEqualsExpressionNode(lhs, rhs) },
            { TokenKind.LessThan,            () => new LessThanExpressionNode(lhs, rhs) },
            { TokenKind.LessThanEquals,      () => new LessThanEqualsExpressionNode(lhs, rhs) },
            { TokenKind.AmpersandAmpersand,  () => new LogicalAndExpressionNode(lhs, rhs) },
            { TokenKind.BarBar,              () => new LogicalOrExpressionNode(lhs, rhs) },
        };


        return operators[binaryOperator.Kind]();
    }

    // @note: pretty much everything in C# is an expression so we probably want to split this up
    private ExpressionNode? ParseExpression(ExpressionNode? possibleLHS=null, bool onlyParseSingle=false)
    {
        var token = PeekCurrent();

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
            possibleLHS = expr;
        }

        var isLiteral = PeekLiteralExpression(out var literal);
        bool isCurrentTokenIdentifier = token.Kind == TokenKind.Identifier; // maybe? to help with LHS?

        bool isUnary = IsUnaryOperator(out var isPrefix);


        if (isUnary)
        {
            var unaryExpr = ParseUnaryExpression(isPrefix); // may be the final symbol in the expr
            //var groupExpr = ParseExpression(unaryExpr);

            //return unaryExpr;
            possibleLHS = unaryExpr; // try to see if we're the LHS of a binary or ternary expression
        }
        
        bool isBinary = !onlyParseSingle && IsBinaryOperator(possibleLHS is null ? 1 : 0);
        bool isTernary = false;

        if (isBinary)
        {
            // Does nullability not work in ternary subexpressions?
            ExpressionNode lhs = possibleLHS ?? (ExpressionNode?)literal ?? new IdentifierExpression { Identifier = token.Lexeme };
            if (possibleLHS is null)
                Consume();
            return ParseBinaryExpression(lhs);
        }
        else if (isCurrentTokenIdentifier && possibleLHS is null)
        {
            Consume();
            return new IdentifierExpression { Identifier = token.Lexeme };
        }
        else if (isLiteral && possibleLHS is null)
        {
            Consume();
            return literal!;
        }

        return possibleLHS;
    }

    private bool IsDeclarationStatement()
    {
        var token = PeekCurrent();

        bool maybeType = false;

        var typeList = new List<TokenKind>
        {
            TokenKind.ByteKeyword,
            TokenKind.SbyteKeyword,
            TokenKind.ShortKeyword,
            TokenKind.UshortKeyword,
            TokenKind.IntKeyword,
            TokenKind.UintKeyword,
            TokenKind.LongKeyword,
            TokenKind.UlongKeyword,
            TokenKind.FloatKeyword,
            TokenKind.DoubleKeyword,
            TokenKind.DecimalKeyword,
            TokenKind.BoolKeyword,
            TokenKind.StringKeyword,
            TokenKind.CharKeyword,
        };

        if (token.Kind == TokenKind.Identifier && token.Lexeme == "var")
            return true;

        if (typeList.Contains(token.Kind))
            maybeType = true;

        if (token.Kind == TokenKind.Identifier)
            maybeType = true;

        return maybeType && PeekSafe(2).Kind == TokenKind.Equals;
    }

    private StatementNode ParseDeclarationStatement()
    {
        var type = Consume();
        var identifier = Consume();
        Expect(TokenKind.Equals);
        var expr = ParseExpression();
        Expect(TokenKind.Semicolon);

        return new VariableDeclarationStatement(type.Lexeme, identifier.Lexeme, expr!);
    }

    private ExpressionStatementNode ParseExpressionStatement()
    {
        var expr = ParseExpression();
        Expect(TokenKind.Semicolon);

        return new ExpressionStatementNode
        {
            Expression = expr!
        };
    }

    private BlockNode ParseBlock()
    {
        Expect(TokenKind.OpenBrace);
        var statements = ParseStatementList();
        Expect(TokenKind.CloseBrace);

        return new BlockNode(statements);
    }

    private AstNode ParseBody()
    {
        // Could be either an embedded statement or a block?
        return PeekCurrent().Kind == TokenKind.OpenBrace ? ParseBlock() : ParseStatement(isEmbeddedStatement: true);
    }

    private IfStatementNode ParseIfStatement()
    {
        Expect(TokenKind.IfKeyword);
        Expect(TokenKind.OpenParen);
        var expr = ParseExpression()!;
        Expect(TokenKind.CloseParen);

        var body = ParseBody();

        AstNode? elseBody = null;

        // @note: 'else if' doesn't really exist, it's just an embedded if statement in an else clause ;)

        if (ConsumeIfMatch(TokenKind.ElseKeyword))
        {
            elseBody = ParseBody();
        }

        return new IfStatementNode(expr, body, elseBody);
    }

    private DoStatementNode ParseDoStatement()
    {
        Expect(TokenKind.DoKeyword);
        var body = ParseBody()!;
        Expect(TokenKind.WhileKeyword);
        Expect(TokenKind.OpenParen);
        var expr = ParseExpression()!;
        Expect(TokenKind.CloseParen);
        Expect(TokenKind.Semicolon);

        return new DoStatementNode(expr, body);
    }

    private EmptyStatementNode ParseEmptyStatement()
    {
        Expect(TokenKind.Semicolon);
        return new EmptyStatementNode();
    }

    private StatementNode ParseStatement(bool isEmbeddedStatement=false)
    {
        /*
         * C# has the following statement types according to
         * https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/statements
         * 
         * Declaration Statements
         * Expression Statements (expressions that store the result in a variable)
         * Selection Statements (if, switch)
         * Iteration Statements (do, for, foreach, while)
         * Jump Statements (break, continue, goto, return, yield)
         * Exception-Handling Statements (throw, try-catch, try-finally, try-catch-finally)
         * Checked/unchecked Statements
         * Await statement
         * Yield return statement
         * Fixed statement
         * Lock statement
         * Labaled statements (for goto)
         * Empty statement (;)
         */

        if (!isEmbeddedStatement && IsDeclarationStatement())
            return ParseDeclarationStatement();

        var token = PeekCurrent();

        switch (token.Kind)
        {
            // Selection statements
            case TokenKind.IfKeyword:
                return ParseIfStatement();

            case TokenKind.DoKeyword:
                return ParseDoStatement(); 

            case TokenKind.SwitchKeyword:
                throw new NotImplementedException();
                break;

            case TokenKind.Semicolon:
                return ParseEmptyStatement();
        }

        // If no matches parse as an expression statement
        return ParseExpressionStatement();
    }

    private List<StatementNode> ParseStatementList()
    {
        List<StatementNode> statements = [];

        while (!IsAtEnd() && !Matches(TokenKind.CloseBrace))
            statements.Add(ParseStatement());

        return statements;
    }

    private AST ParseInternal(Token[] tokens)
    {
        var ast = new AST { Root = new() };

        if (tokens.Length == 0)
            return ast;

        _input = tokens;

        var statements = ParseStatementList();

        //var expr = ParseExpression()!;
        foreach (var statement in statements)
        {
            ast.Root.GlobalStatements.Add(new GlobalStatementNode
            {
                Statement = statement
            });
        }

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
