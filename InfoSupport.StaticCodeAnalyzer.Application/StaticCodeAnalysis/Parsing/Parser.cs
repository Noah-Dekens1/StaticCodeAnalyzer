﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

/// <summary>
/// https://refactoring.guru/replace-conditional-with-polymorphism 
/// You're doing a lot with tokenkinds and have a lot of ifs around it
/// Perhaps you can find a way to have e.g. a "UnaryExpressionToken".
/// abstract ExpressionToken will be able t have a "ParseExpression" where the unary expresiion token
/// will be having an implementation based on it's type
/// </summary>
public class Parser
{
    /// <summary>
    /// Wouldn't a queue fulfill your needs better?
    /// This way you won't have to write your own seek, consume, peek etc. logic
    /// 
    /// Also perhaps a LinkedList would help. 
    /// (You'll have a "currentToken" or such, have a pointer to Next and to Previous and be able
    /// to iterate over your tokens like that)
    /// </summary>
    private Token[] _input = [];

    private int _index = 0;

    /// <summary>
    /// This is for example something you might want to move to a static class.
    /// This way when I want to know if the list is complete; I know where to look.
    /// For looking at the logic of the parser I do not need to know the exact contents.
    /// </summary>
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
        { '\'', '\'' },
        { '"', '\"' },
        { '0', '\0' },
        // \x and \u \U have been excluded as they have trailing values!
        // not a major issue though as nobody uses them ;)
    };

    [DebuggerHidden]
    public bool CanPeek(int count)
        => _index + count < _input.Length;

    [DebuggerHidden]
    public bool IsAtEnd()
        => PeekCurrent().Kind == TokenKind.EndOfFile || _index >= _input.Length;

    [DebuggerHidden]
    public int Tell()
            => _index;

    [DebuggerHidden]
    public void Seek(int pos)
    {
        _index = pos;
    }

    /// <summary>
    /// You currently always consume one token. 
    /// You can always make it more complex later.
    /// </summary>
    [DebuggerHidden]
    public Token Consume()
    {
        return _input[_index++];
    }

    [DebuggerHidden]
    public void ConsumeAndThrowIfNextTokenIsNot(TokenKind expected)
    {
        // Consuming the token here gives you side effects on the check.
        // Looking at the name (expect(..)) I wouldn't .. expect the token to be consumed
        var actual = Consume().Kind;
        if (actual != expected) // Use brackets to avoid (future) bugs
            throw new Exception($"Expected {expected} but got {actual}");
            // Never throw "Exception". Throw a specific exception instead.
            // It would probably be a good idea to create your own exception here.
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
        return CanPeek(count) ? _input[_index + count] : throw new ArgumentOutOfRangeException(nameof(count));
    }

    [DebuggerHidden]
    public Token PeekSafe(int count = 1)
    {
        const int invalidTokenKind = 9999999; // To create an invalid token that will never match any case
                                              // but prevent needing dozens of null-checks
        return CanPeek(count) ? _input[_index + count] : new Token { Kind = (TokenKind)invalidTokenKind };
    }

    [DebuggerHidden]
    public bool Matches(TokenKind kind, int peekOffset = 0)
    {
        return PeekSafe(peekOffset).Kind == kind;
    }

    public bool ConsumeIfMatch(TokenKind c, bool includeConsumed = false)
    {
        // includeConsumed is not really what's happening here I believe?
        // It more looks to determine if you wish to match the previous (or is it current?) token.
        // Negative search; same story
        int negativeSearch = includeConsumed ? 1 : 0;

        // Give c a more cleare name
        if (Peek(negativeSearch).Kind == c)
        {
            Consume();
            return true;
        }

        return false;
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
            else // Wouldn't just adding a backslash to every.. backslah you find suffice? 
                sb.Append(ResolveEscapeSequence(str[++i]));
        }

        return sb.ToString();
    }

    private bool PeekLiteralExpression([MaybeNullWhen(false)] out LiteralExpressionNode literal, Token? providedToken = null)
    {
        // Why not let your calling function always provide the token?
        // And perhaps you can rename it to something like "TryGetLiteralExpression"
        // To have it more standard (like .TryGetValue(..))
        var token = providedToken ?? PeekCurrent();
        var kind = token.Kind;

        literal = null;

        if (kind == TokenKind.NumericLiteral)
        {
            //Consume();
            literal = new NumericLiteralNode(token.Value);
            return true;
        }
        else if (kind == TokenKind.StringLiteral || kind == TokenKind.InterpolatedStringLiteral)
        {
            literal = new StringLiteralNode(ParseStringLiteral(token.Lexeme));
            return true;
        }
        else if (kind == TokenKind.CharLiteral)
        {
            throw new NotImplementedException();
        }
        else if (kind == TokenKind.TrueKeyword || kind == TokenKind.FalseKeyword)
        {
            var value = kind == TokenKind.TrueKeyword;
            literal = new BooleanLiteralNode(value);
            return true;
        }

        return false;
    }

    private static bool IsUnaryOperator(TokenKind kind) => kind switch
    {
        // I'd put this in a "unary operator" list. and use a contains
        TokenKind.PlusPlus or TokenKind.MinusMinus or TokenKind.Exclamation or TokenKind.Minus or TokenKind.Tilde => true,
        _ => false
    };

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

            // No throw exceptions
            // Also best to put this outside your if/else. This isn't necesary a condition for the else
            throw new Exception("Neither identifier nor literal was found");
        }
    }

    private bool IsUnaryOperator(bool hasReadIdentifier, out bool isPrefix, int peekStart = 0)
    {
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

           //Just return in your switch and put the exception as default
            return result ?? throw new Exception("Couldn't resolve unary prefix expression");
        }
        else
        {
            // This code is now somewhat doubled. Perhaps you can first try parse this
            // Then Do the prefix specific. And end up with an exception if all else fails
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

        // once again I personally prefer a list called "BinaryOperators"
        // Also you'll be able to let that live outside of this file.
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

            // Compound operators
            case TokenKind.PlusEquals:
            case TokenKind.MinusEquals:
            case TokenKind.AsteriskEquals:
            case TokenKind.SlashEquals:
            case TokenKind.PercentEquals:

            case TokenKind.AmpersandEquals:
            case TokenKind.BarEquals:

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

            { TokenKind.PlusEquals,          () => new AddAssignExpressionNode(lhs, rhs) },
            { TokenKind.MinusEquals,         () => new SubtractAssignExpressionNode(lhs, rhs) },
            { TokenKind.AsteriskEquals,      () => new MultiplyAssignExpressionNode(lhs, rhs) },
            { TokenKind.SlashEquals,         () => new DivideAssignExpressionNode(lhs, rhs) },
            { TokenKind.PercentEquals,       () => new ModulusAssignExpressionNode(lhs, rhs) },
            { TokenKind.AmpersandEquals,     () => new AndAssignExpressionNode(lhs, rhs) },
            { TokenKind.BarEquals,           () => new OrAssignExpressionNode(lhs, rhs) },

            { TokenKind.Equals,              () => new AssignmentExpressionNode(lhs, rhs) },
        };


        return operators[binaryOperator.Kind]();
    }

    private static ExpressionNode ResolveMemberAccess(List<Token> members)
    {
        var member = members[^1];
        var identifier = new IdentifierExpression(member.Lexeme);

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
        // I've seen you do this before (usings)?

        return ResolveMemberAccess(members);
    }

    private ExpressionNode ResolveMaybeGenericIdentifier(bool isInNamespaceOrType)
    {
        var identifier = ResolveIdentifier();

        if (Matches(TokenKind.LessThan) && PossiblyParseTypeArgumentList(out var typeArguments, isInNamespaceOrType))
        {
            return new GenericNameNode(identifier, typeArguments);
        }

        return identifier;
    }

    private ExpressionNode PeekIdentifier(ref int peekIndex)
    {
        List<Token> members = [];

        do
        {
            members.Add(Peek(peekIndex++));
        } while (!IsAtEnd() && Matches(TokenKind.DoKeyword, peekIndex) && Matches(TokenKind.Identifier, ++peekIndex));

        return ResolveMemberAccess(members);
    }

    private InvocationExpressionNode ParseInvocation(ExpressionNode lhs)
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
        var arguments = ParseArgumentList();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);
        return new InvocationExpressionNode(lhs, arguments);
    }

    private ElementAccessExpressionNode ParseElementAccess(ExpressionNode lhs)
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenBracket);

        var expr = ParseExpression();
        var indexExpr = new IndexExpressionNode(expr!);

        var args = new BracketedArgumentList([new ArgumentNode(indexExpr, null)]);

        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseBracket);

        return new ElementAccessExpressionNode(lhs, args);
    }

    private ExpressionNode? TryParsePrimaryExpression()
    {
        if (ConsumeIfMatch(TokenKind.NewKeyword))
        {
            var type = ParseType();
            ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
            var args = ParseArgumentList();
            ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);
            return new NewExpressionNode(type, args);
        }

        return null;
    }

    private ExpressionNode? TryParsePrimaryPostfixExpression(ExpressionNode resolvedIdentifier)
    {
        // Invocation
        if (Matches(TokenKind.OpenParen))
            return ParseInvocation(resolvedIdentifier);

        // Element access
        if (Matches(TokenKind.OpenBracket))
            return ParseElementAccess(resolvedIdentifier);

        return null;
    }

    // @note: pretty much everything in C# is an expression so we probably want to split this up
    // One method to parseExpression would be fine, as long as you split up the calls done inside
    private ExpressionNode? ParseExpression(ExpressionNode? possibleLHS = null, bool onlyParseSingle = false)
    {
        var token = PeekCurrent();

        if (token.Kind == TokenKind.OpenParen)
        {
            Consume();
            var expr = new ParenthesizedExpressionNode(ParseExpression()!);
            Consume();
            possibleLHS = expr;
        }

        bool isCurrentTokenIdentifier = token.Kind == TokenKind.Identifier;
        ExpressionNode? resolvedIdentifier = null;

        if (isCurrentTokenIdentifier && possibleLHS is null)
        {
            resolvedIdentifier = ResolveMaybeGenericIdentifier(false);
            possibleLHS = resolvedIdentifier;
        }

        var primaryExpression = TryParsePrimaryExpression();

        if (primaryExpression is not null)
        {
            possibleLHS = primaryExpression;
        }
        else if (resolvedIdentifier is not null)
        {
            var primaryPostfixExpression = TryParsePrimaryPostfixExpression(resolvedIdentifier);
            if (primaryPostfixExpression is not null)
            {
                possibleLHS = primaryPostfixExpression;
            }
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
        //bool isTernary = false;

        if (isBinary)
        {
            // Does nullability not work in ternary subexpressions?
            ExpressionNode lhs = possibleLHS ?? (ExpressionNode?)literal ?? new IdentifierExpression(token.Lexeme);
            if (possibleLHS is null)
                Consume();
            return ParseBinaryExpression(lhs);
        }
        else if (isCurrentTokenIdentifier && possibleLHS is null)
        {
            Consume();
            return new IdentifierExpression(token.Lexeme);
        }
        else if (isLiteral && possibleLHS is null)
        {
            Consume();
            return literal!; // Don't think the `!` is needed? Also generally using the `!` is evil as it'll just disable null checking
        }

        return possibleLHS;
    }

    private static readonly List<TokenKind> TypeList =
    [
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
        TokenKind.VoidKeyword
    ];

    /// <summary>
    /// This sounds a little strange. don't you want to be sure it's a type?
    /// </summary>
    private static bool IsMaybeType(Token token, bool excludeVar)
    {
        bool maybeType = false;

        maybeType |= !excludeVar && (token.Kind == TokenKind.Identifier && token.Lexeme == "var");
        maybeType |= TypeList.Contains(token.Kind);
        maybeType |= token.Kind == TokenKind.Identifier;

        return maybeType;
    }

    /// <summary>
    /// Also if were to use a linked list; then perhaps you could just create these checks as extensions
    /// e.g. Token.IsDeclarationStatement(). 
    /// And then inside the "IsDeclarationStatement" you can just look at the e.g. Token.Next or Token.Next.Next
    /// </summary>
    private bool IsDeclarationStatement()
    {
        var token = PeekCurrent();

        bool maybeType = IsMaybeType(token, false);

        return maybeType && PeekSafe(2).Kind == TokenKind.Equals;
    }

    private readonly List<TokenKind> _disambiguatingTokenList = [
        TokenKind.OpenParen,
        TokenKind.CloseParen,
        TokenKind.CloseBracket,
        TokenKind.CloseBrace,
        TokenKind.Colon,
        TokenKind.Semicolon,
        TokenKind.Comma,
        TokenKind.Dot,
        TokenKind.Question,
        TokenKind.EqualsEquals,
        TokenKind.ExclamationEquals,
        TokenKind.Bar,
        TokenKind.Caret,
        TokenKind.AmpersandAmpersand,
        TokenKind.BarBar,
        TokenKind.Ampersand,
        TokenKind.OpenBracket,

        // Relational
        TokenKind.LessThan,
        TokenKind.GreaterThan,
        TokenKind.LessThanEquals,
        TokenKind.GreaterThanEquals,
        TokenKind.IsKeyword,
        TokenKind.AsKeyword
    ];

    private bool PossiblyParseTypeArgumentList(out TypeArgumentsNode? typeArguments, bool isInNamespaceOrTypeName, bool precededByDisambiguatingToken = false)
    {
        // See section 6.2.5 of C#'s lexical structure specification
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure

        // We want to avoid backtracking so we have to peek a lot

        var startPosition = Tell();

        typeArguments = null;
        var temp = new List<TypeNode>();

        if (!ConsumeIfMatch(TokenKind.LessThan))
        {
            Seek(startPosition);
            return false;
        }

        do
        {
            if (!IsMaybeType(PeekCurrent(), true))
            {
                Seek(startPosition);
                return false;
            }

            var identifier = ResolveIdentifier();

            TypeArgumentsNode? nestedTypes = null;

            if (Matches(TokenKind.LessThan))
            {
                if (!PossiblyParseTypeArgumentList(out nestedTypes, isInNamespaceOrTypeName))
                {
                    Seek(startPosition);
                    return false;
                }
            }

            temp.Add(new TypeNode(baseType: identifier, typeArguments: nestedTypes));

            if (!ConsumeIfMatch(TokenKind.Comma))
            {
                if (ConsumeIfMatch(TokenKind.GreaterThan))
                {
                    break;
                }

                // If neither , nor > we're probably not parsing a generic type

                Seek(startPosition);
                return false;
            }

        } while (!IsAtEnd());

        bool isTypeArgumentList = isInNamespaceOrTypeName;

        isTypeArgumentList |= _disambiguatingTokenList.Contains(PeekCurrent().Kind);
        isTypeArgumentList |= precededByDisambiguatingToken;
        // @todo: contextual query keywords/contextual disambiguating identifiers

        if (!isTypeArgumentList)
        {
            Seek(startPosition);
            return false;
        }

        typeArguments = new TypeArgumentsNode(temp);

        return true;
    }

    private TypeNode ParseType()
    {
        var baseType = ResolveIdentifier();

        var maybeGeneric = Matches(TokenKind.LessThan);

        TypeArgumentsNode? typeArguments = null;

        if (maybeGeneric && PossiblyParseTypeArgumentList(out typeArguments, true))
        {

        }

        return new TypeNode(baseType, typeArguments);
    }

    private StatementNode ParseDeclarationStatement()
    {
        var type = ParseType();
        var identifier = Consume();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Equals);
        var expr = ParseExpression();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);

        return new VariableDeclarationStatement(type, identifier.Lexeme, expr!);
    }

    private ExpressionStatementNode ParseExpressionStatement(bool expectSemicolon = true)
    {
        var expr = ParseExpression();
        if (expectSemicolon)
            ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);

        return new ExpressionStatementNode(expr!);
    }

    private BlockNode ParseBlock()
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenBrace);
        var statements = ParseStatementList();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseBrace);

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
            ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);
            return expr;
        }

        return ParseBlock();
    }

    private IfStatementNode ParseIfStatement()
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.IfKeyword);
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
        var expr = ParseExpression()!;
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);

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
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.DoKeyword);
        var body = ParseBody()!;
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.WhileKeyword);
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
        var expr = ParseExpression()!;
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);

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

    private ArgumentListNode ParseArgumentList()
    {
        var expressions = new List<ArgumentNode>();

        do
        {
            if (Matches(TokenKind.CloseParen))
                return new ArgumentListNode(expressions);

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

        return new ArgumentListNode(expressions);
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
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);
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
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.ForKeyword);
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
        var initializer = ParseForInitializer();
        //Expect(TokenKind.Semicolon);
        var expression = ParseExpression()!;
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);
        var iterationStatement = ParseForIteration()!;
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);

        var body = ParseBody();

        return new ForStatementNode(initializer, expression, iterationStatement, body);
    }

    private ForEachStatementNode ParseForEachStatement()
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.ForeachKeyword);
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
        var type = ParseType();
        var identifier = Consume();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.InKeyword);
        var collection = ParseExpression()!;
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);
        var body = ParseBody();

        return new ForEachStatementNode(type, identifier.Lexeme, collection, body);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.WhileKeyword);
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
        var expr = ParseExpression()!;
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);
        var body = ParseBody();

        return new WhileStatementNode(expr, body);
    }

    private static AstNode ResolveQualifiedNameRecursive(List<Token> members)
    {
        // Why use a list here? You acn use a stack and pop every time.
        // If the initial list is empty you'll get an expcetion
        var member = members[^1];
        var identifier = new IdentifierExpression(member.Lexeme);

        if (members.Count == 1)
            return identifier;

        members.Remove(member);

        return new QualifiedNameNode(
            lhs: ResolveQualifiedNameRecursive(members),
            identifier: identifier
        );
    }

    private AstNode ParseQualifiedName()
    {
        List<Token> members = [];
        do
        {
            members.Add(Consume());
        } 
        // It can be a little difficult to follow what is happening in the while loop.
        // If it would be an incorrect using (e.g. using Foo.Bar. you won't throw an exception)
        // Wouldn't it make sense to just keep on going untill the semicolumn?
        while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Dot) && Matches(TokenKind.Identifier));

        return ResolveQualifiedNameRecursive(members);
    }

    private UsingDirectiveNode ParseUsingDirective()
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.UsingKeyword);

        var hasAlias = PeekSafe().Kind == TokenKind.Equals;
        string? alias = null;

        if (hasAlias)
        {
            alias = Consume().Lexeme;
            ConsumeAndThrowIfNextTokenIsNot(TokenKind.Equals);
        }

        var ns = ParseQualifiedName();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);

        return new UsingDirectiveNode(ns, alias);
    }

    private ReturnStatementNode ParseReturnStatement()
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.ReturnKeyword);

        if (ConsumeIfMatch(TokenKind.Semicolon))
            return new ReturnStatementNode(null);

        var expression = ParseExpression();

        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);

        return new ReturnStatementNode(expression);
    }

    private LocalFunctionDeclarationNode ParseLocalFunction()
    {
        ParseModifiers(out var accessModifier, out var modifiers);
        Debug.Assert(accessModifier is null);

        var type = ParseType();
        var identifier = ResolveMaybeGenericIdentifier(true);
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
        var parms = ParseParameterList();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);
        var body = ParseMethodBody();

        Debug.Assert(body is not null);

        return new LocalFunctionDeclarationNode(modifiers, identifier, type, parms, body);
    }

    private EmptyStatementNode ParseEmptyStatement()
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);
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
         * Labeled statements (for goto)
         * Empty statement (;)
         */

        if (!isEmbeddedStatement && IsDeclarationStatement())
            return ParseDeclarationStatement();

        if (IsLocalFunctionDeclaration())
            return ParseLocalFunction();

        var token = PeekCurrent();

        //Personally not a fan of big switches. More fan of a factory
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

            case TokenKind.ReturnKeyword:
                return ParseReturnStatement();

            case TokenKind.Semicolon:
                return ParseEmptyStatement();
        }

        // If no matches parse as an expression statement
        return ParseExpressionStatement(); // So basically the default of your switch?
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
        var idx = 0;

        // Skip over all (access) modifiers
        while (IsValidTypeModifier(PeekSafe(idx)))
            idx++;

        return IsTypeKeyword(PeekSafe(idx));
    }

    private bool IsLocalFunctionDeclaration()
    {
        /*
        var idx = 0;

        // Skip over all (access) modifiers
        while (IsValidTypeModifier(PeekSafe(idx)))
            idx++;

        bool maybeFunction = true;

        // @note: this doesn't short circuit so we may be wasting a few function calls here
        maybeFunction &= IsMaybeType(PeekSafe(idx++));
        maybeFunction &= PeekSafe(idx++).Kind == TokenKind.Identifier;
        maybeFunction &= PeekSafe(idx++).Kind == TokenKind.OpenParen;

        return maybeFunction;
        */

        var startPos = Tell();

        while (IsValidTypeModifier(PeekCurrent()))
            Consume();

        if (!IsMaybeType(PeekCurrent(), true))
        {
            Seek(startPos);
            return false;
        }

        ParseType();

        var identifier = ResolveMaybeGenericIdentifier(true);

        if (identifier is null)
        {
            Seek(startPos);
            return false;
        }

        if (!Matches(TokenKind.OpenParen))
        {
            Seek(startPos);
            return false;
        }

        Seek(startPos);
        return true;

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

            if (current.Kind == TokenKind.Identifier && current.Lexeme == "async")
            {
                modifiers.Add(OptionalModifier.Async);
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
            ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);

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

    private MemberNode ParseProperty(string propertyName, TypeNode propertyType)
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenBrace);

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

        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseBrace);

        var hasValue = ConsumeIfMatch(TokenKind.Equals);
        var value = hasValue ? ParseExpression() : null;

        if (hasValue)
        {
            ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);
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

            var type = ParseType();
            var identifier = Consume();

            parameters.Add(new ParameterNode(type, identifier.Lexeme));

        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Comma));

        return new ParameterListNode(parameters);
    }

    private MemberNode ParseConstructor(AccessModifier accessModifier)
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.Identifier); // ctor name
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);  // parms
        var parms = ParseParameterList();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);
        var body = ParseMethodBody();

        return new ConstructorNode(accessModifier, parms, body);
    }

    private MemberNode ParseMethod(AccessModifier accessModifier, List<OptionalModifier> modifiers, TypeNode returnType, AstNode methodName)
    {
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenParen);
        var parms = ParseParameterList();
        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseParen);
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

    private static string ResolveNameFromAstNode(AstNode node)
    {
        var identifierNode = node is GenericNameNode nameNode
                    ? (IdentifierExpression)nameNode.Identifier
                    : ((IdentifierExpression)node);

        return identifierNode.Identifier;
    }

    private MemberNode ParseMember(TypeKind kind, AstNode typeName)
    {
        if (kind == TypeKind.Enum)
            return ParseEnumMember();

        var name = ResolveNameFromAstNode(typeName);

        ParseModifiers(out var accessModifier, out var modifiers);
        var isCtor = PeekCurrent().Lexeme == name && PeekSafe().Kind == TokenKind.OpenParen;

        if (isCtor)
            return ParseConstructor(accessModifier ?? AccessModifier.Private);

        var type = ParseType();
        var identifier = ResolveMaybeGenericIdentifier(true);
        var isMethod = Matches(TokenKind.OpenParen);
        var isProperty = Matches(TokenKind.OpenBrace);
        var isField = !isMethod && !isProperty; // @todo: events?

        if (isField)
        {
            var hasValue = ConsumeIfMatch(TokenKind.Equals);
            var value = hasValue ? ParseExpression() : null;
            ConsumeAndThrowIfNextTokenIsNot(TokenKind.Semicolon);
            return new FieldMemberNode(accessModifier ?? AccessModifier.Private, modifiers, ResolveNameFromAstNode(identifier), type, value);
        }
        else if (isProperty)
        {
            return ParseProperty(ResolveNameFromAstNode(identifier), type);
        }
        else if (isMethod)
        {
            return ParseMethod(accessModifier ?? AccessModifier.Private, modifiers, type, identifier);
        }

        throw new NotImplementedException();
    }

    private List<MemberNode> ParseMembers(TypeKind kind, AstNode typeName)
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

        var identifier = ResolveMaybeGenericIdentifier(true);
        AstNode? parentName = null;

        if (ConsumeIfMatch(TokenKind.Colon))
        {
            parentName = ResolveMaybeGenericIdentifier(true);
        }

        ConsumeAndThrowIfNextTokenIsNot(TokenKind.OpenBrace);

        var kind = type switch
        {
            TokenKind.ClassKeyword => TypeKind.Class,
            TokenKind.StructKeyword => TypeKind.Struct,
            TokenKind.InterfaceKeyword => TypeKind.Interface,
            TokenKind.EnumKeyword => TypeKind.Enum,
            _ => throw new Exception()
        };

        var members = ParseMembers(kind, identifier);

        ConsumeAndThrowIfNextTokenIsNot(TokenKind.CloseBrace);

        return type switch
        {
            TokenKind.ClassKeyword => new ClassDeclarationNode(identifier, members, parentName, accessModifier, modifiers),
            TokenKind.EnumKeyword => new EnumDeclarationNode(identifier, members.Cast<EnumMemberNode>().ToList(), parentName, accessModifier, modifiers),
            TokenKind.InterfaceKeyword => new InterfaceDeclarationNode(identifier, members, parentName, accessModifier, modifiers),
            TokenKind.StructKeyword => new StructDeclarationNode(identifier, members, parentName, accessModifier, modifiers),
            _ => throw new NotImplementedException(),
        };
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
            ast.Root.GlobalStatements.Add(new GlobalStatementNode(statement));
        }

        var typeDeclarations = ParseTypeDeclarations();

        ast.Root.TypeDeclarations.AddRange(typeDeclarations);

        return ast;
    }

    public static AST Parse(Token[] tokens)
    {
        var parser = new Parser();
        return parser.ParseInternal(tokens);
    }

    // Why ask for a list if you actually want an array?
    public static AST Parse(List<Token> tokens)
    {
        // Why did you choose for this instead of directly
        // constructing a parser with the tokens?
        var parser = new Parser();
        return parser.ParseInternal([.. tokens]); // self parsing will be a pain if I keep using C# 12 features
    }
}
