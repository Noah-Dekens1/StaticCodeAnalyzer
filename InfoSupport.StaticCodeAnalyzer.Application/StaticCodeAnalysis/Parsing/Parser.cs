using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
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
        { '\'', '\'' },
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

    [DebuggerHidden]
    public int Tell()
            => _index;

    [DebuggerHidden]
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
    public bool Matches(TokenKind kind, int peekOffset=0)
    {
        return PeekSafe(peekOffset).Kind == kind;
    }

    [DebuggerHidden]
    public bool MatchesLexeme(string lexeme, TokenKind? kind = null, int peekOffset=0)
    {
        var token = PeekSafe(peekOffset);

        return token.Lexeme == lexeme && (kind is null || token.Kind == kind);
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

    private static char ResolveEscapeSequence(char c)
    {
        return EscapeSequences.TryGetValue(c, out var resolved) ? resolved : c;
    }

    private static char ParseCharLiteral(string str)
    {
        // @note: 1 & 2 because 0 (and 2/3) will be single quotes
        char c = str[1];

        if (str[1] == '\\')
            c = ResolveEscapeSequence(str[2]);

        return c;
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
            literal = new CharLiteralNode(ParseCharLiteral(token.Lexeme));
            return true;
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
                case TokenKind.Tilde:
                    result = new UnaryBitwiseComplementNode(expr);
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
        Expect(TokenKind.OpenParen);
        var arguments = ParseArgumentList();
        Expect(TokenKind.CloseParen);
        return new InvocationExpressionNode(lhs, arguments);
    }

    private ElementAccessExpressionNode ParseElementAccess(ExpressionNode lhs)
    {
        Expect(TokenKind.OpenBracket);

        var expr = ParseExpression();
        var indexExpr = new IndexExpressionNode(expr!);

        var args = new BracketedArgumentList([new ArgumentNode(indexExpr, null)]);

        Expect(TokenKind.CloseBracket);

        return new ElementAccessExpressionNode(lhs, args);
    }

    private IndexedCollectionInitializerNode ParseIndexedCollectionInitializerElement()
    {
        // @todo: support multiple expressions in bracket? For example for jagged arrays
        bool isBracketedIndexer = ConsumeIfMatch(TokenKind.OpenBracket);

        var indexer = isBracketedIndexer ? ParseExpression()! : ResolveIdentifier();

        if (isBracketedIndexer)
            Expect(TokenKind.CloseBracket);

        Expect(TokenKind.Equals);

        var value = ParseExpression()!;

        return new IndexedCollectionInitializerNode(indexer, value);
    }

    private ComplexCollectionInitializerNode ParseComplexCollectionInitializerElement()
    {
        var values = new List<ExpressionNode>();

        Expect(TokenKind.OpenBrace);

        do
        {
            if (Matches(TokenKind.CloseBrace))
                break;

            values.Add(ParseExpression()!);
        } while (ConsumeIfMatch(TokenKind.Comma));

        Expect(TokenKind.CloseBrace);

        return new ComplexCollectionInitializerNode(values);
    }

    private RegularCollectionInitializerNode ParseRegularCollectionInitializerElement()
    {
        return new RegularCollectionInitializerNode(ParseExpression()!);
    }

    private ExpressionNode? TryParsePrimaryExpression()
    {
        // Object creation expression
        if (ConsumeIfMatch(TokenKind.NewKeyword))
        {
            TypeNode? type = null;

            if (IsMaybeType(PeekCurrent(), true))
                type = ParseType();

            ArgumentListNode? args = null;

            if (ConsumeIfMatch(TokenKind.OpenParen))
            {
                args = ParseArgumentList();
                Expect(TokenKind.CloseParen);
            }

            CollectionInitializerNode? initializer = null;

            // Could be collection initializer
            if (ConsumeIfMatch(TokenKind.OpenBrace))
            {
                // What kind is it? List, indexed, grouped?

                // If the current token is { again we can parse as a (list of?) complex initializer expressions
                // { "a", "b" } and ["a"] = "b" syntax may not be mixed in a collection initializer expression
                // Indexed can either be Name = "Value" or ["Str"] = "Value"

                var isIndexed = Matches(TokenKind.OpenBracket) ||
                    (Matches(TokenKind.Identifier) && Matches(TokenKind.Equals, 1));
                var isGrouped = Matches(TokenKind.OpenBrace);
                var isRegular = !isIndexed && !isGrouped;

                var values = new List<CollectionInitializerElementNode>();

                do
                {
                    if (Matches(TokenKind.CloseBrace))
                        break;

                    if (isIndexed)
                    {
                        values.Add(ParseIndexedCollectionInitializerElement());
                    }

                    if (isGrouped)
                    {
                        values.Add(ParseComplexCollectionInitializerElement());
                    }

                    if (isRegular)
                    {
                        values.Add(ParseRegularCollectionInitializerElement());
                    }
                } while (ConsumeIfMatch(TokenKind.Comma));

                initializer = new CollectionInitializerNode(values);

                Expect(TokenKind.CloseBrace);
            }

            return new NewExpressionNode(type, args, initializer);
        }
        else if (ConsumeIfMatch(TokenKind.OpenBracket))
        {
            // collection expression like [1, 2, 3] or [ .. Param, 2, 3, .. SomeList ]

            List<ElementNode> elements = [];

            do
            {
                if (Matches(TokenKind.CloseBracket))
                    break;

                elements.Add(
                    ConsumeIfMatch(TokenKind.DotDot)
                        ? new SpreadElementNode(ParseExpression()!)
                        : new ExpressionElementNode(ParseExpression()!)
                );

            } while (ConsumeIfMatch(TokenKind.Comma));

            Expect(TokenKind.CloseBracket);

            return new CollectionExpressionNode(elements);
        }

        return null;
    }

    private LambdaExpressionNode ParseLambdaExpressionSingleParam(ExpressionNode param)
    {
        Expect(TokenKind.EqualsGreaterThan);

        var paramIdentifier = ((IdentifierExpression)param).Identifier;

        return new LambdaExpressionNode([new LambdaParameterNode(paramIdentifier)], ParseLambdaBody());
    }

    private ExpressionNode? TryParsePrimaryPostfixExpression(ExpressionNode resolvedIdentifier)
    {
        // Invocation
        if (Matches(TokenKind.OpenParen))
            return ParseInvocation(resolvedIdentifier);

        // Element access
        if (Matches(TokenKind.OpenBracket))
            return ParseElementAccess(resolvedIdentifier);

        if (Matches(TokenKind.EqualsGreaterThan))
            return ParseLambdaExpressionSingleParam(resolvedIdentifier);

        return null;
    }

    private ExpressionNode? ParseStartParenthesisExpression()
    {
        Expect(TokenKind.OpenParen);

        var start = Tell();

        bool maybeLambda = true;

        bool isFirst = true;
        bool isImplicit = true;

        List<LambdaParameterNode> parameters = new List<LambdaParameterNode>();

        do
        {
            if (Matches(TokenKind.CloseParen))
                break;

            // @note: besides checking whether it may be a type it also checks if the next token is an identifier
            // because we don't have knowledge about which identifiers may be types
            var type = IsMaybeType(PeekCurrent(), true) && Matches(TokenKind.Identifier, 1) 
                ? ParseType() 
                : null;

            if (isFirst)
                isImplicit = type is null;

            if (type is null != isImplicit) // must all be implicit/explicit, exit as early as possible if not the case
            {
                maybeLambda = false;
                break;
            }    

            if (!Matches(TokenKind.Identifier))
            {
                maybeLambda = false;
                break;
            }

            var identifier = Consume();
            parameters.Add(new LambdaParameterNode(identifier.Lexeme, type));
            isFirst = false;
        } while (ConsumeIfMatch(TokenKind.Comma) && maybeLambda);

        maybeLambda &= ConsumeIfMatch(TokenKind.CloseParen);

        if (maybeLambda)
            maybeLambda = ConsumeIfMatch(TokenKind.EqualsGreaterThan);

        if (!maybeLambda)
        {
            // @note: assumes parenthesized expr, may also be tuple?
            Seek(start);
            var expr = new ParenthesizedExpressionNode(ParseExpression()!);
            Expect(TokenKind.CloseParen);
            return expr;
        }

        return new LambdaExpressionNode(parameters, ParseLambdaBody());
    }

    // @note: pretty much everything in C# is an expression so we probably want to split this up
    private ExpressionNode? ParseExpression(ExpressionNode? possibleLHS = null, bool onlyParseSingle = false)
    {
        var token = PeekCurrent();

        if (token.Kind == TokenKind.OpenParen)
        {
            // Could be plain parenthesis around a subexpression, a lambda's argument list, or a tuple
            /*
            Consume();
            var expr = new ParenthesizedExpressionNode(ParseExpression()!);
            Consume();
            possibleLHS = expr;
            */
            var expr = ParseStartParenthesisExpression();

            possibleLHS = expr; // @todo: only if it isn't a lambda
        }

        bool isCurrentTokenIdentifier = token.Kind == TokenKind.Identifier;
        ExpressionNode? resolvedIdentifier = null;

        if (isCurrentTokenIdentifier && possibleLHS is null)
        {
            resolvedIdentifier = ResolveMaybeGenericIdentifier(false);
            possibleLHS = resolvedIdentifier;
        }

        ExpressionNode? primaryExpression = resolvedIdentifier is null 
            ? TryParsePrimaryExpression() 
            : null;

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
            return literal!;
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

    private static bool IsMaybeType(Token token, bool excludeVar)
    {
        bool maybeType = false;

        maybeType |= !excludeVar && (token.Kind == TokenKind.Identifier && token.Lexeme == "var");
        maybeType |= TypeList.Contains(token.Kind);
        maybeType |= token.Kind == TokenKind.Identifier;

        return maybeType;
    }

    private bool IsDeclarationStatement()
    {
        var token = PeekCurrent();

        bool maybeType = IsMaybeType(token, false);
        bool maybeDeclaration = false;

        int pos = Tell();

        if (maybeType)
        {
            ParseType();
            if (ConsumeIfMatch(TokenKind.Identifier) && ConsumeIfMatch(TokenKind.Equals))
            {
                maybeDeclaration = true;
            }
        }

        Seek(pos);

        return maybeDeclaration;
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

    private bool PossiblyParseTypeArgumentList(out TypeArgumentsNode? typeArguments, bool isInNamespaceOrTypeName, bool precededByDisambiguatingToken=false)
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
        Expect(TokenKind.Equals);
        var expr = ParseExpression();
        Expect(TokenKind.Semicolon);

        return new VariableDeclarationStatement(type, identifier.Lexeme, expr!);
    }

    private ExpressionStatementNode ParseExpressionStatement(bool expectSemicolon = true)
    {
        var expr = ParseExpression();
        if (expectSemicolon)
            Expect(TokenKind.Semicolon);

        return new ExpressionStatementNode(expr!);
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

    private AstNode ParseLambdaBody()
    {
        if (Matches(TokenKind.OpenBrace))
        {
            return ParseBlock();
        }

        return ParseExpression()!;
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
        var type = ParseType();
        var identifier = Consume();
        Expect(TokenKind.InKeyword);
        var collection = ParseExpression()!;
        Expect(TokenKind.CloseParen);
        var body = ParseBody();

        return new ForEachStatementNode(type, identifier.Lexeme, collection, body);
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

    private LocalFunctionDeclarationNode ParseLocalFunction()
    {
        ParseModifiers(out var accessModifier, out var modifiers);
        Debug.Assert(accessModifier is null);

        var type = ParseType();
        var identifier = ResolveMaybeGenericIdentifier(true);
        Expect(TokenKind.OpenParen);
        var parms = ParseParameterList();
        Expect(TokenKind.CloseParen);
        var body = ParseMethodBody();

        Debug.Assert(body is not null);

        return new LocalFunctionDeclarationNode(modifiers, identifier, type, parms, body);
    }

    private ParenthesizedPatternNode ParseParenthesizedPattern()
    {
        Expect(TokenKind.OpenParen);
        var innerPattern = ParsePattern()!;
        Expect(TokenKind.CloseParen);
        return new ParenthesizedPatternNode(innerPattern);
    }

    private readonly Dictionary<TokenKind, RelationalPatternOperator> _relationalOperators = new()
    {
        { TokenKind.GreaterThan, RelationalPatternOperator.GreaterThan },
        { TokenKind.GreaterThanEquals, RelationalPatternOperator.GreaterThanOrEqual },
        { TokenKind.LessThan, RelationalPatternOperator.LessThan },
        { TokenKind.LessThanEquals, RelationalPatternOperator.LessThanOrEqual },
    };

    private bool IsRelationalPattern(Token token)
    {
        return _relationalOperators.ContainsKey(token.Kind);
    }

    private RelationalPatternNode ParseRelationalPattern()
    {
        var token = Consume();

        if (_relationalOperators.TryGetValue(token.Kind, out var value))
        {
            return new RelationalPatternNode(value, ParseExpression()!);
        }

        throw new Exception("Expected relational pattern");
    }

    private ConstantPatternNode ParseConstantPattern()
    {
        var value = ParseExpression()!;

        return new ConstantPatternNode(value);
    }

    // Parsing patterns is quite similar to parsing expressions
    private PatternNode? ParsePattern()
    {
        PatternNode? possibleLHS = null;

        var isIdentifier = Matches(TokenKind.Identifier);
        var isLiteral = PeekLiteralExpression(out var literal);

        if (Matches(TokenKind.OpenParen))
        {
            possibleLHS = ParseParenthesizedPattern();
        }

        if (possibleLHS is null && IsRelationalPattern(PeekCurrent()))
        {
            possibleLHS = ParseRelationalPattern();
        }

        var token = PeekCurrent();

        bool isLogicalNot = token.Lexeme == "not";
        bool isLogicalAnd = token.Lexeme == "and";
        bool isLogicalOr = token.Lexeme == "or";

        if (isLogicalNot)
        {
            Debug.Assert(possibleLHS is null);
            Consume();
            return new NotPatternNode(ParsePattern()!);
        }

        if (isLogicalAnd)
        {
            Debug.Assert(possibleLHS is not null);
            Consume();
            return new AndPatternNode(possibleLHS!, ParsePattern()!);
        }

        if (isLogicalOr)
        {
            Debug.Assert(possibleLHS is not null);
            Consume();
            return new OrPatternNode(possibleLHS!, ParsePattern()!);
        }

        if (isIdentifier || isLiteral)
        {
            // @note: Roslyn doesn't parse this as a constant pattern so I assume this is 'technically wrong'
            // but for the analyzer this makes things more simple and AFAIK there's little difference between
            // an old switch case/constant pattern anyways, might need refactoring if this proves to be wrong
            // in the future
            return ParseConstantPattern();
        }

        return possibleLHS;
    }

    private SwitchStatementNode ParseSwitchStatement()
    {
        Expect(TokenKind.SwitchKeyword);

        Expect(TokenKind.OpenParen);

        var expr = ParseExpression()!;

        Expect(TokenKind.CloseParen);

        Expect(TokenKind.OpenBrace);

        var sections = new List<SwitchSectionNode>();

        while (!Matches(TokenKind.CloseBrace))
        {
            bool isCase = Matches(TokenKind.CaseKeyword);
            bool isDefault = Matches(TokenKind.DefaultKeyword);

            if (!isCase && !isDefault)
            {
                throw new NotImplementedException();
            }

            Consume();

            PatternNode? caseValue = isCase ? ParsePattern() : null;
            ExpressionNode? whenClause = null;

            if (MatchesLexeme("when", TokenKind.Identifier))
            {
                Expect(TokenKind.Identifier);
                whenClause = ParseExpression()!; // binary expr
            }

            Expect(TokenKind.Colon);

            List<StatementNode> statements = [];

            while (!Matches(TokenKind.CaseKeyword) && !Matches(TokenKind.DefaultKeyword) && !Matches(TokenKind.CloseBrace))
            {
                statements.Add(ParseStatement());
            }

            SwitchSectionNode newSection = isCase
                ? new SwitchCaseNode(caseValue!, statements, whenClause)
                : new SwitchDefaultCaseNode(statements);

            sections.Add(newSection);
        }

        Expect(TokenKind.CloseBrace);
        return new SwitchStatementNode(expr, sections);
    }

    private BreakStatementNode ParseBreakStatement()
    {
        Expect(TokenKind.BreakKeyword);
        Expect(TokenKind.Semicolon);
        return new BreakStatementNode();
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
         * Labeled statements (for goto)
         * Empty statement (;)
         */

        if (!isEmbeddedStatement && IsDeclarationStatement())
            return ParseDeclarationStatement();

        if (!isEmbeddedStatement && Matches(TokenKind.OpenBrace))
            return ParseBlock();

        if (IsLocalFunctionDeclaration())
            return ParseLocalFunction();

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
                return ParseSwitchStatement();

            case TokenKind.BreakKeyword:
                return ParseBreakStatement();

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

            if (current.Kind == TokenKind.Identifier && current.Lexeme == "required")
            {
                modifiers.Add(OptionalModifier.Required);
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

    private MemberNode ParseProperty(string propertyName, TypeNode propertyType)
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

            var type = ParseType();
            var identifier = Consume();

            parameters.Add(new ParameterNode(type, identifier.Lexeme));

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

    private MemberNode ParseMethod(AccessModifier accessModifier, List<OptionalModifier> modifiers, TypeNode returnType, AstNode methodName)
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
            Expect(TokenKind.Semicolon);
            return new FieldMemberNode(accessModifier ?? AccessModifier.Private, modifiers,ResolveNameFromAstNode(identifier), type, value);
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

        return type switch
        {
            TokenKind.ClassKeyword     => new ClassDeclarationNode(identifier, members, parentName, accessModifier, modifiers),
            TokenKind.EnumKeyword      => new EnumDeclarationNode(identifier, members.Cast<EnumMemberNode>().ToList(), parentName, accessModifier, modifiers),
            TokenKind.InterfaceKeyword => new InterfaceDeclarationNode(identifier, members, parentName, accessModifier, modifiers),
            TokenKind.StructKeyword    => new StructDeclarationNode(identifier, members, parentName, accessModifier, modifiers),
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

    public static AST Parse(List<Token> tokens)
    {
        var parser = new Parser();
        return parser.ParseInternal([.. tokens]); // self parsing will be a pain if I keep using C# 12 features
    }
}
