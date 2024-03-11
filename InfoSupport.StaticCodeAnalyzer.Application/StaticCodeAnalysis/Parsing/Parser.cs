using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Principal;
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

    private bool PeekLiteralExpression([MaybeNullWhen(false)] out LiteralExpressionNode literal, Token? providedToken = null)
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
        var token = PeekCurrent();

        if (token.Kind == TokenKind.Identifier)
        {
            return ResolveIdentifier();
        }
        else
        {
            Consume();
            if (PeekLiteralExpression(out var expr, token))
                return expr;

            throw new Exception("Neither identifier nor literal was found");
        }
    }

    private bool IsUnaryOperator(bool hasReadIdentifier, out bool isPrefix, int peekStart = 0)
    {
        /*
        var current = Peek(peekStart + 0);
        bool currentIsOpenParen = current.Kind == TokenKind.OpenParen;
        isPrefix = !hasReadIdentifier && !IsIdentifierOrLiteral(current) && !currentIsOpenParen;

        var next = CanPeek(1) ? Peek(peekStart + 1) : new Token();
        var isPostfix = !isPrefix && (next.Kind == TokenKind.PlusPlus || next.Kind == TokenKind.MinusMinus);

        return (isPrefix && IsUnaryOperator(current.Kind)) || isPostfix;
        */

        var current = PeekSafe(peekStart);

        if (hasReadIdentifier && (current.Kind == TokenKind.PlusPlus || current.Kind == TokenKind.MinusMinus))
        {
            isPrefix = false;
            return true;
        }
        else if (!hasReadIdentifier && IsUnaryOperator(current.Kind))
        {
            isPrefix = true;
            return true;
        }

        isPrefix = false;
        return false;
    }

    private UnaryExpressionNode ParseUnaryExpression(bool isPrefix, ExpressionNode? identifier = null)
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
            var identifierOrLiteral = identifier ?? ParseIdentifierOrLiteral();
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

    private bool IsBinaryOperator(int peekStart = 0)
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

            // Special cases
            case TokenKind.Equals: // Assignment expression is technically also a binary expression (LHS/RHS)

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

            { TokenKind.Equals,              () => new AssignmentExpressionNode(lhs, rhs) },
        };


        return operators[binaryOperator.Kind]();
    }

    private static ExpressionNode ResolveMemberAccess(List<Token> members)
    {
        var member = members[^1];
        var identifier = new IdentifierExpression() { Identifier = member.Lexeme };

        if (members.Count == 1)
            return identifier;

        members.Remove(member);

        return new MemberAccessExpressionNode(
            lhs: ResolveMemberAccess(members),
            identifier: identifier
        );
    }

    private ExpressionNode ResolveIdentifier()
    {
        List<Token> members = [];

        do
        {
            members.Add(Consume());
        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Dot) && Matches(TokenKind.Identifier));

        return ResolveMemberAccess(members);
    }

    private InvocationExpressionNode ParseInvocation(ExpressionNode lhs)
    {
        Expect(TokenKind.OpenParen);
        var arguments = ParseArgumentList();
        Expect(TokenKind.CloseParen);
        return new InvocationExpressionNode(lhs, arguments);
    }

    // @note: pretty much everything in C# is an expression so we probably want to split this up
    private ExpressionNode? ParseExpression(ExpressionNode? possibleLHS = null, bool onlyParseSingle = false)
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

        bool isCurrentTokenIdentifier = token.Kind == TokenKind.Identifier;
        ExpressionNode? resolvedIdentifier = null;

        if (isCurrentTokenIdentifier && possibleLHS is null)
        {
            resolvedIdentifier = ResolveIdentifier();
            possibleLHS = resolvedIdentifier;
        }

        bool isMethodCall = PeekSafe(0).Kind == TokenKind.OpenParen && resolvedIdentifier is not null;

        if (isMethodCall)
        {
            possibleLHS = ParseInvocation(resolvedIdentifier!);
        }

        var isLiteral = PeekLiteralExpression(out var literal);

        bool isUnary = IsUnaryOperator(isCurrentTokenIdentifier, out var isPrefix);


        if (isUnary)
        {
            var unaryExpr = ParseUnaryExpression(isPrefix, resolvedIdentifier); // may be the final symbol in the expr
            //var groupExpr = ParseExpression(unaryExpr);

            //return unaryExpr;
            possibleLHS = unaryExpr; // try to see if we're the LHS of a binary or ternary expression
        }

        bool isBinary = !onlyParseSingle && IsBinaryOperator((possibleLHS is null && !isCurrentTokenIdentifier) ? 1 : 0);
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

    private ExpressionStatementNode ParseExpressionStatement(bool expectSemicolon = true)
    {
        var expr = ParseExpression();
        if (expectSemicolon)
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

    private AstNode ParseMethodBody()
    {
        // Could be either an expression bodied member or a block
        if (ConsumeIfMatch(TokenKind.EqualsGreaterThan))
        {
            var expr = ParseExpression()!;
            Expect(TokenKind.Semicolon);
            return expr;
        }

        return ParseBlock();
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

    private AstNode ParseCommaSeperatedExpressionStatements()
    {
        var statements = new List<ExpressionStatementNode>();

        if (Matches(TokenKind.Semicolon) || Matches(TokenKind.CloseParen))
            return new ExpressionStatementListNode(statements);

        do
        {
            var expr = ParseExpressionStatement(false);
            if (expr is not null)
                statements.Add(expr);
        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Comma));

        return new ExpressionStatementListNode(statements);
    }

    private ArgumentList ParseArgumentList()
    {
        var expressions = new List<ArgumentNode>();

        do
        {
            if (Matches(TokenKind.CloseParen))
                return new ArgumentList(expressions);

            var current = PeekSafe(0);
            var next = PeekSafe(1);

            string? name = null;

            if (
                current.Kind == TokenKind.Identifier &&
                next.Kind == TokenKind.Colon)
            {
                Consume();
                Consume();

                name = current.Lexeme;
            }

            var expr = ParseExpression();

            if (expr is not null)
                expressions.Add(new ArgumentNode(expr, name));

        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Comma));

        return new ArgumentList(expressions);
    }

    private AstNode ParseForInitializer()
    {
        // The initializer may either be a variable declaration statement
        // Like int i = 0; or a comma-seperated list of expression statements seperated by commas
        // Only the following are allowed: increment/decrement, assignment,
        // method invocation, await expression, object creation (new keyword)
        // Like "for (i = 3, Console.WriteLine("test"), i++; i < 10; i++)"

        // "Sane" path of variable declaration
        if (IsDeclarationStatement())
        {
            return ParseDeclarationStatement();
        }

        var result = ParseCommaSeperatedExpressionStatements();
        Expect(TokenKind.Semicolon);
        return result;
    }

    private AstNode ParseForIteration()
    {
        // Comma seperated list of expression statements

        var result = ParseCommaSeperatedExpressionStatements();
        return result;
    }

    private ForStatementNode ParseForStatement()
    {
        Expect(TokenKind.ForKeyword);
        Expect(TokenKind.OpenParen);
        var initializer = ParseForInitializer();
        //Expect(TokenKind.Semicolon);
        var expression = ParseExpression()!;
        Expect(TokenKind.Semicolon);
        var iterationStatement = ParseForIteration()!;
        Expect(TokenKind.CloseParen);

        var body = ParseBody();

        return new ForStatementNode(initializer, expression, iterationStatement, body);
    }

    private ForEachStatementNode ParseForEachStatement()
    {
        Expect(TokenKind.ForeachKeyword);
        Expect(TokenKind.OpenParen);
        var type = Consume();
        var identifier = Consume();
        Expect(TokenKind.InKeyword);
        var collection = ParseExpression()!;
        Expect(TokenKind.CloseParen);
        var body = ParseBody();

        return new ForEachStatementNode(type.Lexeme, identifier.Lexeme, collection, body);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        Expect(TokenKind.WhileKeyword);
        Expect(TokenKind.OpenParen);
        var expr = ParseExpression()!;
        Expect(TokenKind.CloseParen);
        var body = ParseBody();

        return new WhileStatementNode(expr, body);
    }

    private static AstNode ResolveQualifiedNameRecursive(List<Token> members)
    {
        var member = members[^1];
        var identifier = new IdentifierExpression() { Identifier = member.Lexeme };

        if (members.Count == 1)
            return identifier;

        members.Remove(member);

        return new QualifiedNameNode(
            lhs: ResolveMemberAccess(members),
            identifier: identifier
        );
    }

    private AstNode ParseQualifiedName()
    {
        List<Token> members = [];

        do
        {
            members.Add(Consume());
        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Dot) && Matches(TokenKind.Identifier));

        return ResolveQualifiedNameRecursive(members);
    }

    private UsingDirectiveNode ParseUsingDirective()
    {
        Expect(TokenKind.UsingKeyword);

        var hasAlias = PeekSafe().Kind == TokenKind.Equals;
        string? alias = null;

        if (hasAlias)
        {
            alias = Consume().Lexeme;
            Expect(TokenKind.Equals);
        }

        var ns = ParseQualifiedName();
        Expect(TokenKind.Semicolon);

        return new UsingDirectiveNode(ns, alias);
    }

    private ReturnStatementNode ParseReturnStatement()
    {
        Expect(TokenKind.ReturnKeyword);

        if (ConsumeIfMatch(TokenKind.Semicolon))
            return new ReturnStatementNode(null);

        var expression = ParseExpression();

        Expect(TokenKind.Semicolon);

        return new ReturnStatementNode(expression);
    }

    private EmptyStatementNode ParseEmptyStatement()
    {
        Expect(TokenKind.Semicolon);
        return new EmptyStatementNode();
    }

    private StatementNode ParseStatement(bool isEmbeddedStatement = false)
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

            case TokenKind.ForKeyword:
                return ParseForStatement();

            case TokenKind.ForeachKeyword:
                return ParseForEachStatement();

            case TokenKind.WhileKeyword:
                return ParseWhileStatement();

            case TokenKind.SwitchKeyword:
                throw new NotImplementedException();
                break;

            case TokenKind.ReturnKeyword:
                return ParseReturnStatement();

            case TokenKind.Semicolon:
                return ParseEmptyStatement();
        }

        // If no matches parse as an expression statement
        return ParseExpressionStatement();
    }

    private List<UsingDirectiveNode> ParseUsingDirectives()
    {
        List<UsingDirectiveNode> directives = [];

        while (!IsAtEnd() && Matches(TokenKind.UsingKeyword))
        {
            directives.Add(ParseUsingDirective());
        }

        return directives;
    }

    private List<StatementNode> ParseStatementList()
    {
        List<StatementNode> statements = [];

        while (!IsAtEnd() && !Matches(TokenKind.CloseBrace))
            statements.Add(ParseStatement());

        return statements;
    }

    private bool IsAccessModifier()
    {
        var kind = PeekSafe(0).Kind;
        return kind == TokenKind.PrivateKeyword ||
               kind == TokenKind.InternalKeyword ||
               kind == TokenKind.ProtectedKeyword ||
               kind == TokenKind.PublicKeyword;
    }

    private AccessModifier ParseAccessModifier()
    {
        var kind = Consume().Kind;
        var next = PeekSafe(0).Kind;

        if (kind == TokenKind.PrivateKeyword)
        {
            if (next == TokenKind.ProtectedKeyword)
            {
                Consume();
                return AccessModifier.PrivateProtected;
            }

            return AccessModifier.Private;
        }

        if (kind == TokenKind.ProtectedKeyword)
        {
            if (next == TokenKind.InternalKeyword)
            {
                Consume();
                return AccessModifier.ProtectedInternal;
            }

            return AccessModifier.Protected;
        }

        if (kind == TokenKind.InternalKeyword)
            return AccessModifier.Internal;

        if (kind == TokenKind.PublicKeyword)
            return AccessModifier.Public;

        throw new Exception($"Invalid access modifier {kind}");
    }

    private static bool IsTypeKeyword(Token token)
    {
        var kind = token.Kind;

        return kind == TokenKind.ClassKeyword ||
               kind == TokenKind.InterfaceKeyword ||
               kind == TokenKind.EnumKeyword ||
               kind == TokenKind.StructKeyword;
    }

    private bool IsTypeDeclaration()
    {
        var idx = -1;

        // Skip over all (access) modifiers
        while (IsValidTypeModifier(PeekSafe(++idx)))
            ;

        return IsTypeKeyword(PeekSafe(idx));
    }

    private List<StatementNode> ParseTopLevelStatements()
    {
        List<StatementNode> statements = [];

        while (!IsAtEnd() && !IsTypeDeclaration())
            statements.Add(ParseStatement());

        return statements;
    }

    private static readonly Dictionary<TokenKind, OptionalModifier> Modifiers = new()
    {
        { TokenKind.StaticKeyword, OptionalModifier.Static },
        { TokenKind.VirtualKeyword, OptionalModifier.Virtual },
        { TokenKind.OverrideKeyword, OptionalModifier.Override },
        { TokenKind.AbstractKeyword, OptionalModifier.Abstract },
        { TokenKind.SealedKeyword, OptionalModifier.Sealed },
        { TokenKind.ExternKeyword, OptionalModifier.Extern },
        { TokenKind.NewKeyword, OptionalModifier.New },
        { TokenKind.ReadonlyKeyword, OptionalModifier.Readonly },
        { TokenKind.ConstKeyword, OptionalModifier.Const },
        { TokenKind.VolatileKeyword, OptionalModifier.Volatile },
    };

    private static readonly List<string> ValidClassModifiers = [
        "abstract",
        "sealed",
        "static",
        "partial",
        "internal",
        "private",
        "public",
        "protected"
    ];

    private static bool IsValidTypeModifier(Token token)
    {
        return ValidClassModifiers.Contains(token.Lexeme);
    }

    private void ParseModifiers(out AccessModifier? accessModifier, out List<OptionalModifier> modifiers)
    {
        accessModifier = null;
        modifiers = [];

        while (!IsAtEnd())
        {
            var current = PeekSafe(0);

            if (IsAccessModifier())
            {
                if (accessModifier is not null)
                    throw new Exception("Multiple access modifiers found");

                accessModifier = ParseAccessModifier();
                continue;
            }

            if (Modifiers.TryGetValue(current.Kind, out var value))
            {
                modifiers.Add(value);
                Consume();
                continue;
            }

            if (current.Kind == TokenKind.Identifier && current.Lexeme == "partial")
            {
                modifiers.Add(OptionalModifier.Partial);
                Consume();
                continue;
            }

            break;
        }
    }

    private PropertyAccessorNode? ParseAccessor(out bool isGetter)
    {

        AccessModifier? accessModifier = IsAccessModifier()
            ? ParseAccessModifier()
            : null;

        var keyword = PeekCurrent();

        isGetter = false;

        bool isAccessor = keyword.Lexeme == "get" || keyword.Lexeme == "set" || keyword.Lexeme == "init";

        if (!isAccessor)
            return null;

        bool initOnly = keyword.Lexeme == "init";
        isGetter = keyword.Lexeme == "get";

        Consume();

        // Auto-implemented property { get; }
        if (ConsumeIfMatch(TokenKind.Semicolon))
            return new PropertyAccessorNode(
                PropertyAccessorType.Auto,
                accessModifier ?? AccessModifier.Public,
                null,
                null,
                initOnly
            );

        // Expression bodied member { get => true; }
        if (ConsumeIfMatch(TokenKind.EqualsGreaterThan))
        {
            var expr = ParseExpression();
            Expect(TokenKind.Semicolon);

            return new PropertyAccessorNode(
                PropertyAccessorType.ExpressionBodied,
                accessModifier ?? AccessModifier.Public,
                expr,
                null,
                initOnly
            );
        }

        // Block bodied member { get { return _field } }
        if (Matches(TokenKind.OpenBrace))
        {
            var block = ParseBlock();

            return new PropertyAccessorNode(
                PropertyAccessorType.BlockBodied,
                accessModifier ?? AccessModifier.Public,
                null,
                block,
                initOnly
            );
        }

        return null;
    }

    private MemberNode ParseProperty(string propertyName, string propertyType)
    {
        Expect(TokenKind.OpenBrace);

        PropertyAccessorNode? getter = null;
        PropertyAccessorNode? setter = null;

        PropertyAccessorNode? accessor;
        do
        {
            accessor = ParseAccessor(out bool isGetter);

            if (accessor is not null)
            {
                if (isGetter)
                    getter = accessor;
                else
                    setter = accessor;
            }
        } while (!IsAtEnd() && accessor is not null);

        Expect(TokenKind.CloseBrace);

        var hasValue = ConsumeIfMatch(TokenKind.Equals);
        var value = hasValue ? ParseExpression() : null;

        if (hasValue)
        {
            Expect(TokenKind.Semicolon);
        }

        return new PropertyMemberNode(propertyName, propertyType, getter, setter, value);
    }

    private ParameterListNode ParseParameterList()
    {
        var parameters = new List<ParameterNode>();

        // @todo: support ref, out, params, optional, ...

        do
        {
            if (Matches(TokenKind.CloseParen))
                return new ParameterListNode(parameters);

            var type = Consume();
            var identifier = Consume();

            parameters.Add(new ParameterNode(type.Lexeme, identifier.Lexeme));

        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Comma));

        return new ParameterListNode(parameters);
    }

    private MemberNode ParseConstructor(AccessModifier accessModifier)
    {
        Expect(TokenKind.Identifier); // ctor name
        Expect(TokenKind.OpenParen);  // parms
        var parms = ParseParameterList();
        Expect(TokenKind.CloseParen);
        var body = ParseMethodBody();

        return new ConstructorNode(accessModifier, parms, body);
    }

    private MemberNode ParseMethod(AccessModifier accessModifier, List<OptionalModifier> modifiers, string returnType, string methodName)
    {
        Expect(TokenKind.OpenParen);
        var parms = ParseParameterList();
        Expect(TokenKind.CloseParen);
        AstNode? body = null;
            
        if (!ConsumeIfMatch(TokenKind.Semicolon))
            body = ParseMethodBody();

        return new MethodNode(accessModifier, modifiers, returnType, methodName, parms, body);
    }

    private EnumMemberNode ParseEnumMember()
    {
        var identifier = Consume();
        ExpressionNode? value = null;

        if (ConsumeIfMatch(TokenKind.Equals))
        {
            value = ParseExpression();
        }

        ConsumeIfMatch(TokenKind.Comma);

        return new EnumMemberNode(identifier.Lexeme, value);
    }

    private MemberNode ParseMember(TypeKind kind, string typeName)
    {
        if (kind == TypeKind.Enum)
            return ParseEnumMember();

        ParseModifiers(out var accessModifier, out var modifiers);
        var isCtor = PeekCurrent().Lexeme == typeName;

        if (isCtor)
            return ParseConstructor(accessModifier ?? AccessModifier.Private);

        var type = Consume();
        var identifier = Consume();
        var isMethod = Matches(TokenKind.OpenParen);
        var isProperty = Matches(TokenKind.OpenBrace);
        var isField = !isMethod && !isProperty; // @todo: events?

        if (isField)
        {
            var hasValue = ConsumeIfMatch(TokenKind.Equals);
            var value = hasValue ? ParseExpression() : null;
            Expect(TokenKind.Semicolon);
            return new FieldMemberNode(accessModifier ?? AccessModifier.Private, modifiers, identifier.Lexeme, type.Lexeme, value);
        }
        else if (isProperty)
        {
            return ParseProperty(identifier.Lexeme, type.Lexeme);
        }
        else if (isMethod)
        {
            return ParseMethod(accessModifier ?? AccessModifier.Private, modifiers, type.Lexeme, identifier.Lexeme);
        }

        throw new NotImplementedException();
    }

    private List<MemberNode> ParseMembers(TypeKind kind, string typeName)
    {
        var members = new List<MemberNode>();

        while (!IsAtEnd() && !Matches(TokenKind.CloseBrace))
        {
            members.Add(ParseMember(kind, typeName));
        }
        
        return members;
    }

    private TypeDeclarationNode ParseTypeDeclaration()
    {
        ParseModifiers(out var accessModifier, out var modifiers);

        var type = Consume().Kind;

        var identifier = Consume().Lexeme;
        string? parentName = null;

        if (ConsumeIfMatch(TokenKind.Colon))
        {
            parentName = Consume().Lexeme;
        }

        Expect(TokenKind.OpenBrace);

        var kind = type switch
        {
            TokenKind.ClassKeyword => TypeKind.Class,
            TokenKind.StructKeyword => TypeKind.Struct,
            TokenKind.InterfaceKeyword => TypeKind.Interface,
            TokenKind.EnumKeyword => TypeKind.Enum,
            _ => throw new Exception()
        };

        var members = ParseMembers(kind, identifier);

        Expect(TokenKind.CloseBrace);

        switch (type)
        {
            case TokenKind.ClassKeyword:
                return new ClassDeclarationNode(identifier, members, parentName, accessModifier);
            case TokenKind.EnumKeyword:
                return new EnumDeclarationNode(identifier, members.Cast<EnumMemberNode>().ToList(), parentName, accessModifier);
            default:
                throw new NotImplementedException();
        }
    }

    private List<TypeDeclarationNode> ParseTypeDeclarations()
    {
        var declarations = new List<TypeDeclarationNode>();

        while (!IsAtEnd() && IsTypeDeclaration())
            declarations.Add(ParseTypeDeclaration());

        return declarations;
    }

    private AST ParseInternal(Token[] tokens)
    {
        var ast = new AST { Root = new() };

        if (tokens.Length == 0)
            return ast;

        _input = tokens;

        var directives = ParseUsingDirectives();

        ast.Root.UsingDirectives.AddRange(directives);

        // the 'rule' in C# is that top-level statements must precede any type declarations and namespaces
        // this method stops the moment it encounters a type declaration
        var statements = ParseTopLevelStatements();

        //var expr = ParseExpression()!;
        foreach (var statement in statements)
        {
            ast.Root.GlobalStatements.Add(new GlobalStatementNode
            {
                Statement = statement
            });
        }

        var typeDeclarations = ParseTypeDeclarations();

        ast.Root.TypeDeclarations.AddRange(typeDeclarations);

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
