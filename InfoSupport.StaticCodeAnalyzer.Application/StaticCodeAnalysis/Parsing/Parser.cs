using System;
using System.Collections;
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
using System.Transactions;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing.Exceptions;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

readonly struct MemberData(Token token, TypeArgumentsNode? typeArguments = null, bool isConditional = false, bool isNullForgiving = false)
{
    public readonly Token Token = token;
    public readonly TypeArgumentsNode? TypeArguments = typeArguments;
    public readonly bool IsConditional = isConditional;
    public readonly bool IsNullForgiving = isNullForgiving;
}

readonly struct StringLiteralData(string content, bool isInterpolated, List<StringInterpolationNode> interpolations, int consumed, int quoteCount)
{
    public readonly string Content = content;
    public readonly bool IsInterpolated = isInterpolated;
    public readonly List<StringInterpolationNode> Interpolations = interpolations;
    public readonly int Consumed = consumed;
    public readonly int QuoteCount = quoteCount;
}

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
    public bool Matches(TokenKind kind, int peekOffset = 0)
    {
        return PeekSafe(peekOffset).Kind == kind;
    }

    [DebuggerHidden]
    public bool ConsumeIfMatchSequence(params TokenKind[] tokenKinds)
    {
        for (int i = 0; i < tokenKinds.Length; i++)
            if (!Matches(tokenKinds[i], i))
                return false;

        Consume(tokenKinds.Length);

        return true;
    }

    [DebuggerHidden]
    public bool MatchesLexeme(string lexeme, TokenKind? kind = null, int peekOffset = 0)
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

    private static ExpressionNode ParseExpressionFromTokens(List<Token> tokens)
    {
        var subParser = new Parser
        {
            _input = [.. tokens]
        };
        return subParser.ParseExpression()!;
    }

    private static StringInterpolationNode ParseInterpolation(string str, out int read, int expectedBraces = 1)
    {
        read = 0;

        int i = 0;
        int b = 0;

        var interpolationBuilder = new StringBuilder(); // Call lexer on result

        while (i < str.Length)
        {
            char c = str[i];

            if (c == '"')
            {
                int j;
                int leading = 0;

                for (j = i - 1; j >= 0; j--) // iterate backwards to ignore $/@
                {
                    if (str[j] != '$' && str[j] != '@')
                        break;

                    leading++;
                }

                // Parse string starting from beginning
                // (i may be after $/@ already which is why we look back for j)
                var inner = ParseStringLiteral(str[(j + 1)..]);

                interpolationBuilder.Append(str[i..(j + 1 + inner.Consumed - inner.QuoteCount)]);
                interpolationBuilder.Append('"', inner.QuoteCount);

                // Skip over consumed content besides the leading values which we've
                // already consumed
                i += inner.Consumed - leading;
                continue;
            }

            if (c == '}')
                b++;
            else
                b = 0;

            if (b == expectedBraces)
                break;

            interpolationBuilder.Append(str[i]);

            i++;
        }

        var tokens = Lexer.Lex(interpolationBuilder.ToString());
        var expr = ParseExpressionFromTokens(tokens);

        read = i + 1; // include trailing }

        return new StringInterpolationNode(expr);
    }

    private static StringLiteralData ParseStringLiteral(string str)
    {
        int i = 0;
        int dollarSigns = 0;
        int quotes = 0;
        bool isVerbatim = false;

        List<StringInterpolationNode> interpolations = [];

        while (str[i] != '"')
        {
            char c = str[i];
            if (c == '$')
                dollarSigns++;
            else if (c == '@')
                isVerbatim = true;

            i += 1;
        }

        bool isInterpolated = dollarSigns > 0;

        while (str[i] == '"')
        {
            quotes++;
            i++;

            if (isVerbatim || i >= str.Length)
                break;
        }

        if (quotes == 2) // immediately closing string
        {
            return new StringLiteralData("", isInterpolated, interpolations, i, 1);
        }

        bool isRaw = quotes >= 3;

        var sb = new StringBuilder();

        int bracesSeen = 0;
        int quotesSeen = 1;

        while (i < str.Length)
        {
            var c = str[i];
            var n = i + 1 < str.Length ? str[i + 1] : '\0';

            if (isInterpolated)
            {
                if (c == '{')
                {
                    if (n == '{' && !isRaw)
                    {
                        i += 2;
                        sb.Append(c);
                        continue;
                    }
                    else
                    {
                        bracesSeen++;

                        i += 1;
                        sb.Append(c);

                        if (isInterpolated && bracesSeen == dollarSigns)
                        {
                            var target = str[i..];
                            interpolations.Add(ParseInterpolation(target, out var read, expectedBraces: 1));
                            i += read;
                            sb.Append(target[..read]);
                            bracesSeen = 0;
                        }

                        continue;
                    }
                }
                else
                {
                    bracesSeen = 0;
                }
            }

            // Double "" in verbatim string is ignored
            if (c == '"' && isVerbatim && n == '"')
            {
                sb.Append('"');
                i += 2;
                continue;
            }

            if (c == '"' && !isRaw)
                break;

            if (c == '"' && isRaw)
            {
                quotesSeen = 1;
                int j;

                for (j = i + 1; j < str.Length; j++)
                {
                    if (str[j] == '"' && quotesSeen <= quotes)
                        quotesSeen++;
                    else
                        break;
                }

                if (quotesSeen == quotes)
                    break;
            }

            // Escape sequences in non-verbatim/raw strings
            if (c != '\\' || isVerbatim)
                sb.Append(str[i]);
            else
                sb.Append(ResolveEscapeSequence(str[++i]));

            i += 1;
        }

        i += quotesSeen;

        return new StringLiteralData(sb.ToString(), isInterpolated, interpolations, i, quotesSeen);
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
            var data = ParseStringLiteral(token.Lexeme);

            literal = data.IsInterpolated
                ? new InterpolatedStringLiteralNode(data.Content, data.Interpolations)
                : new StringLiteralNode(data.Content);

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
        else if (kind == TokenKind.NullKeyword)
        {
            literal = new NullLiteralNode();
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
        var next = PeekSafe(peekStart + 1).Kind;
        var nextNext = PeekSafe(peekStart + 2).Kind;

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
            case TokenKind.QuestionQuestion:
            case TokenKind.QuestionQuestionEquals:


                // ...
                return true;
            default: return false;
        }
    }

    private BinaryExpressionNode ParseBinaryExpression(ExpressionNode lhs)
    {
        if (ConsumeIfMatchSequence(TokenKind.LessThan, TokenKind.LessThanEquals))
            return new LeftShiftAssignExpressionNode(lhs, ParseExpression()!);

        if (ConsumeIfMatchSequence(TokenKind.LessThan, TokenKind.LessThan))
            return new LeftShiftExpressionNode(lhs, ParseExpression()!);

        if (ConsumeIfMatchSequence(TokenKind.GreaterThan, TokenKind.GreaterThanEquals))
            return new RightShiftAssignExpressionNode(lhs, ParseExpression()!);

        if (ConsumeIfMatchSequence(TokenKind.GreaterThan, TokenKind.GreaterThan))
            return new RightShiftExpressionNode(lhs, ParseExpression()!);


        var binaryOperator = Consume();

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
            { TokenKind.QuestionQuestion,    () => new NullCoalescingExpressionNode(lhs, rhs) },

            { TokenKind.QuestionQuestionEquals, () => new NullCoalescingAssignmentExpressionNode(lhs, rhs) },
        };


        return operators[binaryOperator.Kind]();
    }

    private static ExpressionNode ResolveMemberAccess(List<MemberData> members, ExpressionNode? lhsExpr = null)
    {
        var member = members[^1];
        ExpressionNode identifier = new IdentifierExpression(member.Token.Lexeme);

        identifier = member.TypeArguments is null
            ? identifier
            : new GenericNameNode(identifier, member.TypeArguments);

        identifier = member.IsNullForgiving
            ? new NullForgivingExpressionNode(identifier)
            : identifier;

        if (members.Count == 1 && lhsExpr is null)
            return identifier;
        else if (members.Count == 1 && lhsExpr is not null)
            return new MemberAccessExpressionNode(lhsExpr, identifier);

        members.Remove(member);

        var prev = members[^1];

        var lhs = ResolveMemberAccess(members);

        return prev.IsConditional
            ? new ConditionalMemberAccessExpressionNode(lhs, identifier)
            : new MemberAccessExpressionNode(
                lhs: lhs,
                identifier: identifier
            );
    }

    private ExpressionNode ResolveIdentifier(bool isMaybeGeneric = false, bool isInNamespaceOrType = false, ExpressionNode? lhs = null)
    {
        var isGlobal = MatchesLexeme("global") && Matches(TokenKind.ColonColon, 1);

        if (isGlobal)
        {
            Consume();
            Expect(TokenKind.ColonColon);
        }

        List<MemberData> members = [];

        do
        {
            var token = Consume();

            TypeArgumentsNode? typeArguments = null;
            bool isConditional = false;

            if (isMaybeGeneric && Matches(TokenKind.LessThan) && PossiblyParseTypeArgumentList(out typeArguments, isInNamespaceOrType))
            {

            }

            if (Matches(TokenKind.Question) && Matches(TokenKind.Dot, 1))
            {
                Consume();
                isConditional = true;
            }

            bool isForgiving = ConsumeIfMatch(TokenKind.Exclamation);

            members.Add(new MemberData(token, typeArguments, isConditional, isForgiving));

        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Dot) && Matches(TokenKind.Identifier));

        var memberAccess = ResolveMemberAccess(members, lhs);

        if (isGlobal)
            memberAccess = new GlobalNamespaceQualifierNode(memberAccess);

        return memberAccess;
    }

    [Obsolete]
    private ExpressionNode ResolveMaybeGenericIdentifier(bool isInNamespaceOrType)
    {
        var identifier = ResolveIdentifier();

        if (Matches(TokenKind.LessThan) && PossiblyParseTypeArgumentList(out var typeArguments, isInNamespaceOrType))
        {
            return new GenericNameNode(identifier, typeArguments);
        }

        return identifier;
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
        bool isConditional = ConsumeIfMatch(TokenKind.Question);
        Expect(TokenKind.OpenBracket);

        var expr = ParseExpression();
        expr = expr is RangeExpressionNode
            ? expr
            : ToIndexExpression(expr)!;

        var args = new BracketedArgumentList([new ArgumentNode(expr)]);

        Expect(TokenKind.CloseBracket);

        return isConditional
            ? new ConditionalElementAccessExpressionNode(lhs, args)
            : new ElementAccessExpressionNode(lhs, args);
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
            bool isArrayCreation = false;
            if (ConsumeIfMatch(TokenKind.OpenBracket))
            {
                isArrayCreation = true;
                Expect(TokenKind.CloseBracket);
            }
            TypeNode? type = null;

            bool _ = IsMaybeType(PeekCurrent(), true) && TryParseType(out type);

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

            return new ObjectCreationExpressionNode(type, isArrayCreation, args, initializer);
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

        if (ConsumeIfMatch(TokenKind.TypeofKeyword))
        {
            Expect(TokenKind.OpenParen);

            var type = ParseType()!;

            Expect(TokenKind.CloseParen);

            return new TypeofExpressionNode(type);
        }

        if (MatchesLexeme("nameof"))
        {
            Consume();

            Expect(TokenKind.OpenParen);

            AstNode value = (AstNode?)ParseExpression() ?? ParseType()!;

            Expect(TokenKind.CloseParen);

            return new NameofExpressionNode(value);
        }

        if (ConsumeIfMatch(TokenKind.SizeofKeyword))
        {
            Expect(TokenKind.OpenParen);

            var type = ParseType();

            Expect(TokenKind.CloseParen);

            return new SizeofExpressionNode(type!);
        }

        if (ConsumeIfMatch(TokenKind.DefaultKeyword))
        {
            // Default operator | default(int)
            if (ConsumeIfMatch(TokenKind.OpenParen))
            {
                var type = ParseType();

                Expect(TokenKind.CloseParen);

                return new DefaultOperatorExpressionNode(type);
            }

            return new DefaultLiteralNode();
        }

        if (MatchesLexeme("await"))
        {
            Consume();

            var expression = ParseExpression()!;
            return new AwaitExpressionNode(expression);
        }

        if (Matches(TokenKind.Caret))
        {
            return ParseIndexExpressionFromEnd();
        }

        if (Matches(TokenKind.ThrowKeyword))
        {
            return ParseThrowExpression();
        }

        return null;
    }

    private LambdaExpressionNode ParseLambdaExpressionSingleParam(ExpressionNode param)
    {
        Expect(TokenKind.EqualsGreaterThan);

        var paramIdentifier = ((IdentifierExpression)param).Identifier;

        return new LambdaExpressionNode([new LambdaParameterNode(paramIdentifier)], ParseLambdaBody());
    }

    private NullForgivingExpressionNode ParseNullForgivingExpression(ExpressionNode lhs)
    {
        Expect(TokenKind.Exclamation);
        return new NullForgivingExpressionNode(lhs);
    }

    private ExpressionNode? TryParsePrimaryPostfixExpression(ExpressionNode resolvedIdentifier, bool isParsingPattern)
    {
        // Invocation
        if (Matches(TokenKind.OpenParen))
            return ParseInvocation(resolvedIdentifier);

        // Element access
        if (Matches(TokenKind.OpenBracket))
            return ParseElementAccess(resolvedIdentifier);

        if (Matches(TokenKind.Question) && Matches(TokenKind.OpenBracket, 1))
            return ParseElementAccess(resolvedIdentifier);

        if (Matches(TokenKind.EqualsGreaterThan) && !isParsingPattern)
            return ParseLambdaExpressionSingleParam(resolvedIdentifier);

        return null;
    }

    private ExpressionNode? ParseIsPatternExpression(ExpressionNode lhs)
    {
        Expect(TokenKind.IsKeyword);

        var pattern = ParsePattern()!;

        return new IsExpressionNode(pattern);
    }

    private bool TryParseTupleExpression([NotNullWhen(true)] out TupleExpressionNode? tupleExpression)
    {
        var start = Tell();

        List<TupleArgumentNode> tupleArguments = [];

        tupleExpression = null;

        do
        {
            var isNamed = Matches(TokenKind.Colon, 1);

            if (isNamed && !Matches(TokenKind.Identifier))
            {
                Seek(start);
                return false;
            }

            string? name = null;

            if (isNamed)
            {
                name = Consume().Lexeme;
                Expect(TokenKind.Colon);
            }

            var expr = ParseExpression();

            if (expr is null)
            {
                Seek(start);
                return false;
            }

            tupleArguments.Add(new TupleArgumentNode(expression: expr, name: name));

        } while (ConsumeIfMatch(TokenKind.Comma));

        if (!ConsumeIfMatch(TokenKind.CloseParen) || tupleArguments.Count < 2)
        {
            Seek(start);
            return false;
        }

        tupleExpression = new TupleExpressionNode(tupleArguments);
        return true;
    }

    private ExpressionNode? ParseStartParenthesisExpression()
    {
        Expect(TokenKind.OpenParen);

        var start = Tell();

        bool maybeLambda = true;

        bool isFirst = true;
        bool isImplicit = true;

        List<LambdaParameterNode> parameters = new();

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
            // @note: assumes parenthesized expr or cast, may also be tuple?
            Seek(start);

            var maybeCast = true;
            {
                var type = ParseType();

                maybeCast &= type is not null && ConsumeIfMatch(TokenKind.CloseParen);
                var rhs = ParseExpression(null, true);
                maybeCast &= rhs is not null;

                if (maybeCast)
                    return new CastExpressionNode(type!, rhs!);
                
                Seek(start);
            }

            if (TryParseTupleExpression(out var tupleExpr))
            {
                return tupleExpr;
            }

            var expr = new ParenthesizedExpressionNode(ParseExpression()!);
            Expect(TokenKind.CloseParen);
            return expr;
        }

        return new LambdaExpressionNode(parameters, ParseLambdaBody());
    }

    private SwitchExpressionNode ParseSwitchExpression(ExpressionNode expr)
    {
        Expect(TokenKind.SwitchKeyword);

        Expect(TokenKind.OpenBrace);

        List<SwitchExpressionArmNode> arms = [];

        do
        {
            if (Matches(TokenKind.CloseBrace))
                break;

            PatternNode caseValue = ParsePattern()!;
            ExpressionNode? whenClause = null;

            if (MatchesLexeme("when", TokenKind.Identifier))
            {
                Expect(TokenKind.Identifier);
                whenClause = ParseExpression()!; // binary expr
            }

            Expect(TokenKind.EqualsGreaterThan);

            var resultExpr = ParseExpression()!;

            arms.Add(new SwitchExpressionArmNode(caseValue, resultExpr, whenClause));

        } while (ConsumeIfMatch(TokenKind.Comma));

        Expect(TokenKind.CloseBrace);

        return new SwitchExpressionNode(expr, arms);
    }

    private bool IsTernaryOperator()
    {
        // Unary/Binary operators like ?. and ?? should be handled already so don't check for them
        return Matches(TokenKind.Question);
    }

    private readonly List<string> _contextualKeywords = [
        "nameof",
        "await"
    ];

    private ExpressionNode ParseTernaryExpression(ExpressionNode lhs)
    {
        Expect(TokenKind.Question);
        var trueExpr = ParseExpression()!;
        Expect(TokenKind.Colon);
        var falseExpr = ParseExpression()!;

        return new TernaryExpressionNode(lhs, trueExpr, falseExpr);
    }

    private IndexExpressionNode ParseIndexExpressionFromEnd()
    {
        Expect(TokenKind.Caret);
        return new IndexExpressionNode(expression: ParseExpression(isParsingIndex: true)!, fromEnd: true);
    }

    private static IndexExpressionNode? ToIndexExpression(ExpressionNode? expression)
    {
        return expression is IndexExpressionNode indexExpression
            ? indexExpression
            : expression is not null
                ? new IndexExpressionNode(expression)
                : null;
    }

    private RangeExpressionNode? ParseRangeExpression(ExpressionNode? lhs)
    {
        Expect(TokenKind.DotDot);

        var rhs = ParseExpression();

        var lhsIndex = ToIndexExpression(lhs);
        var rhsIndex = ToIndexExpression(rhs);

        return new RangeExpressionNode(lhsIndex, rhsIndex);
    }

    private AsExpressionNode ParseAsExpression(ExpressionNode lhs)
    {
        Expect(TokenKind.AsKeyword);
        var type = ParseType();

        return new AsExpressionNode(lhs, type);
    }

    // @note: pretty much everything in C# is an expression so we probably want to split this up
    private ExpressionNode? ParseExpression(ExpressionNode? possibleLHS = null, bool onlyParseSingle = false, bool isParsingIndex = false, bool isParsingPattern = false)
    {
        var token = PeekCurrent();

        if (token.Kind == TokenKind.OpenParen && possibleLHS is null)
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

        bool isCurrentTokenIdentifier = (token.Kind == TokenKind.Identifier || TypeList.Contains(token.Kind)) && !_contextualKeywords.Contains(token.Lexeme);
        ExpressionNode? resolvedIdentifier = null;

        if (isCurrentTokenIdentifier && possibleLHS is null)
        {
            resolvedIdentifier = ResolveIdentifier(true, false);
            possibleLHS = resolvedIdentifier;
        }

        ExpressionNode? primaryExpression = resolvedIdentifier is null
            ? TryParsePrimaryExpression()
            : null;

        if (primaryExpression is not null)
        {
            possibleLHS = primaryExpression;
        }
        else if (possibleLHS is not null)
        {
            var primaryPostfixExpression = TryParsePrimaryPostfixExpression(possibleLHS, isParsingPattern);
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

        if (isCurrentTokenIdentifier && possibleLHS is null)
        {
            Consume();
            possibleLHS = new IdentifierExpression(token.Lexeme);
        }
        else if (isLiteral && possibleLHS is null)
        {
            Consume();
            possibleLHS = literal!;
        }

        if (Matches(TokenKind.IsKeyword) && possibleLHS is not null)
        {
            possibleLHS = ParseIsPatternExpression(possibleLHS!);
        }

        if (Matches(TokenKind.AsKeyword) && possibleLHS is not null)
        {
            possibleLHS = ParseAsExpression(possibleLHS!);
        }

        bool isBinary = !onlyParseSingle && IsBinaryOperator(/*(possibleLHS is null && !isCurrentTokenIdentifier) ? 1 :*/ 0);
        //bool isTernary = false;

        if (possibleLHS is not null && Matches(TokenKind.Exclamation) && !isBinary)
        {
            possibleLHS = ParseNullForgivingExpression(possibleLHS!);
        }

        if (isBinary)
        {
            // Does nullability not work in ternary subexpressions?
            ExpressionNode lhs = possibleLHS ?? (ExpressionNode?)literal ?? new IdentifierExpression(token.Lexeme);
            if (possibleLHS is null)
                Consume();
            possibleLHS = ParseBinaryExpression(lhs);
        }

        bool isTernary = possibleLHS is not null && IsTernaryOperator();

        if (isTernary)
        {
            possibleLHS = ParseTernaryExpression(possibleLHS!);
        }

        if (possibleLHS is not null && Matches(TokenKind.Dot) && Matches(TokenKind.Identifier, 1))
        {
            // Resolve member access
            Expect(TokenKind.Dot);
            possibleLHS = ResolveIdentifier(isMaybeGeneric: true, lhs: possibleLHS);
            return ParseExpression(possibleLHS);
        }

        if (Matches(TokenKind.SwitchKeyword))
        {
            return ParseSwitchExpression(possibleLHS!);
        }

        if (Matches(TokenKind.DotDot) && !isParsingIndex)
        {
            return ParseRangeExpression(possibleLHS);
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
        TokenKind.VoidKeyword,
        TokenKind.ObjectKeyword
    ];

    private bool IsMaybeType(Token token, bool excludeVar)
    {
        bool maybeType = false;

        maybeType |= !excludeVar && (token.Kind == TokenKind.Identifier && token.Lexeme == "var");
        maybeType |= TypeList.Contains(token.Kind);
        maybeType |= token.Kind == TokenKind.Identifier && !_contextualKeywords.Contains(token.Lexeme);
        maybeType |= token.Kind == TokenKind.OpenParen; // tuples, this will get a lot of false positives though

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
            if (TryParseType(out _) && ConsumeIfMatch(TokenKind.Identifier) && ConsumeIfMatch(TokenKind.Equals))
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

            if (ConsumeIfMatch(TokenKind.OpenParen))
            {
                if (!TryParseTupleType(out var tupleType))
                {
                    Seek(startPosition);
                    return false;
                }
                temp.Add(new TypeNode(tupleType));
            }
            else
            {
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
            }


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

    private bool TryParseTupleType([NotNullWhen(true)] out TupleTypeNode? tupleType)
    {
        List<TupleTypeElementNode> elements = [];
        tupleType = null;

        do
        {
            if (!TryParseType(out TypeNode? elementType))
            {
                return false;
            }

            string? identifier = null;

            if (Matches(TokenKind.Identifier))
            {
                identifier = Consume().Lexeme;
            }

            elements.Add(new TupleTypeElementNode(elementType, identifier));

        } while (ConsumeIfMatch(TokenKind.Comma));

        if (!ConsumeIfMatch(TokenKind.CloseParen))
            return false;

        tupleType = new TupleTypeNode(elements);
        return true;
    }

    /**
     * Attempts to parse a type
     * Does NOT backtrack in case of failure, it will only return false
     */
    private bool TryParseType([NotNullWhen(true)] out TypeNode? type)
    {
        type = null;

        if (ConsumeIfMatch(TokenKind.OpenParen))
        {
            if (TryParseTupleType(out var tuple))
            {
                type = new TypeNode(tuple);
                return true;
            }

            return false;
        }

        var baseType = ResolveIdentifier();

        type = null;

        var maybeGeneric = Matches(TokenKind.LessThan);

        TypeArgumentsNode? typeArguments = null;

        if (maybeGeneric && PossiblyParseTypeArgumentList(out typeArguments, true))
        {

        }

        bool maybeArrayNullable = ConsumeIfMatch(TokenKind.Question);

        var arrayData = new ArrayTypeData();

        // Array type
        if (ConsumeIfMatch(TokenKind.OpenBracket))
        {
            arrayData.IsArray = true;
            arrayData.IsInnerTypeNullable = maybeArrayNullable;

            if (Matches(TokenKind.NumericLiteral))
            {
                var literal = Consume();
                arrayData.ArrayRank = (int)literal.Value!;
                arrayData.RankOmitted = false;

                if (Matches(TokenKind.Comma))
                {
                    throw new NotImplementedException("Multidimensional arrays aren't implemented yet");
                }
            }

            if (!ConsumeIfMatch(TokenKind.CloseBracket))
            {
                return false;
            }

            if (Matches(TokenKind.OpenBracket))
            {
                throw new NotImplementedException("Jagged arrays aren't implemented yet");
            }
        }

        bool isNullable = ConsumeIfMatch(TokenKind.Question);

        type = new TypeNode(baseType, typeArguments, arrayData, isNullable || (maybeArrayNullable && !arrayData.IsArray));
        return true;
    }

    private bool TryParseTypeOrBacktrack([NotNullWhen(true)] out TypeNode? type)
    {
        var start = Tell();
        var success = TryParseType(out type);

        if (!success)
            Seek(start);

        return success;
    }

    private TypeNode ParseType()
    {
        return !TryParseType(out var type) 
            ? throw new ParseException("Failed to parse expected type") 
            : type;
    }

    private StatementNode ParseDeclarationStatement(bool onlyParseDeclarator=false)
    {
        var type = ParseType();
        var isDeconstructing = Matches(TokenKind.OpenParen);
        var identifier = Consume();
        Expect(TokenKind.Equals);
        var expr = ParseExpression();

        if (!onlyParseDeclarator)
            Expect(TokenKind.Semicolon);

        return new VariableDeclarationStatement(type, identifier.Lexeme, expr!);
    }

    private bool TryParseTupleDeconstruction([NotNullWhen(true)] out TupleDeconstructStatementNode? tupleDeconstruction)
    {
        tupleDeconstruction = null;

        var start = Tell();

        // Type may be null here, that's valid syntax
        // Like (string a, int b) = QueryData();
        // Or even (a, b) = SomeFunc(); assuming *a* and *b* have already been declared
        TryParseType(out var type);

        if (!ConsumeIfMatch(TokenKind.OpenParen))
        {
            Seek(start);
            return false;
        }

        List<TupleElementNode> designations = [];

        do
        {
            bool hasType = !Matches(TokenKind.Comma, 1) && !Matches(TokenKind.CloseParen, 1);

            TypeNode? elementType = null;

            if (hasType && !TryParseType(out elementType))
            {
                Seek(start);
                return false;
            }

            if (!Matches(TokenKind.Identifier))
            {
                Seek(start);
                return false;
            }

            var identifier = Consume().Lexeme;

            designations.Add(new TupleElementNode(identifier, elementType));

        } while (ConsumeIfMatch(TokenKind.Comma));

        if (!ConsumeIfMatch(TokenKind.CloseParen) || !ConsumeIfMatch(TokenKind.Equals))
        {
            Seek(start);
            return false;
        }

        var expr = ParseExpression();

        if (expr is null)
        {
            Seek(start);
            return false;
        }

        Expect(TokenKind.Semicolon); // should be tuple at this point

        tupleDeconstruction = new TupleDeconstructStatementNode(designations, expr, specifiedType: type);
        return true;
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

            ParameterType parameterType = ParameterType.Regular;
            TypeNode? targetType = null;

            if (ConsumeIfMatchSequence(TokenKind.RefKeyword, TokenKind.ReadonlyKeyword))
            {
                parameterType = ParameterType.RefReadonly;
            }
            else if (_parameterTypes.TryGetValue(PeekCurrent().Lexeme, out var value))
            {
                Consume();
                parameterType = value;
            }

            if (
                parameterType == ParameterType.Out && 
                IsMaybeType(PeekCurrent(), false) && 
                !Matches(TokenKind.Comma, 1) &&
                TryParseType(out var type))
            {
                // Try parse a type
                targetType = type;
            }

            var expr = ParseExpression();

            if (expr is not null)
                expressions.Add(new ArgumentNode(expr, parameterType, targetType, name));

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
        bool isGlobal = MatchesLexeme("global");

        if (isGlobal)
        {
            Expect(TokenKind.Identifier); // global
        }

        Expect(TokenKind.UsingKeyword);

        var hasAlias = PeekSafe().Kind == TokenKind.Equals;
        string? alias = null;

        if (hasAlias)
        {
            alias = Consume().Lexeme;
            Expect(TokenKind.Equals);
        }

        bool isNamespaceGlobal = MatchesLexeme("global");

        if (isNamespaceGlobal)
        {
            Expect(TokenKind.Identifier); // global
            Expect(TokenKind.ColonColon);
        }

        bool isNamespace = true;

        if (hasAlias && !isNamespaceGlobal)
        {
            bool expectsIdentifier = true;
            int i = 0;

            do
            {
                isNamespace &= expectsIdentifier
                    ? Matches(TokenKind.Identifier, i++)
                    : Matches(TokenKind.Dot, i++);

                expectsIdentifier = !expectsIdentifier;

                if (!isNamespace)
                    break;

            } while (!Matches(TokenKind.Semicolon, i));
        }

        if (hasAlias && !isNamespace)
        {
            var type = ParseType();
            Expect(TokenKind.Semicolon);
            return new UsingDirectiveNode(type, alias, isGlobal, isNamespaceGlobal);
        }

        var ns = ParseQualifiedName();
        Expect(TokenKind.Semicolon);

        return new UsingDirectiveNode(ns, alias, isGlobal, isNamespaceGlobal);
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
        var identifier = ResolveIdentifier(isMaybeGeneric: true, isInNamespaceOrType: true);
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
        var value = ParseExpression(isParsingPattern: true)!;

        return new ConstantPatternNode(value);
    }

    private DiscardPatternNode ParseDiscardPattern()
    {
        Consume();
        return new DiscardPatternNode();
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

        if (MatchesLexeme("_"))
        {
            return ParseDiscardPattern();
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

    private ContinueStatementNode ParseContinueStatement()
    {
        Expect(TokenKind.ContinueKeyword);
        Expect(TokenKind.Semicolon);
        return new ContinueStatementNode();
    }

    private ThrowExpressionNode ParseThrowExpression()
    {
        Expect(TokenKind.ThrowKeyword);
        var expression = ParseExpression();
        return new ThrowExpressionNode(expression);
    }

    private TryStatementNode ParseTryStatement()
    {
        Expect(TokenKind.TryKeyword);
        var block = ParseBlock();

        List<CatchClauseNode> catchClauses = [];
        FinallyClauseNode? finallyClause = null;
        ExpressionNode? whenClause = null;

        while (ConsumeIfMatch(TokenKind.CatchKeyword))
        {
            TypeNode? type = null;
            string? identifier = null;
            if (ConsumeIfMatch(TokenKind.OpenParen))
            {
                type = ParseType();
                identifier = Consume().Lexeme;

                Expect(TokenKind.CloseParen);

                if (MatchesLexeme("when"))
                {
                    Consume();
                    Expect(TokenKind.OpenParen);
                    whenClause = ParseExpression();
                    Expect(TokenKind.CloseParen);
                }
            }

            var catchBlock = ParseBlock();

            catchClauses.Add(new CatchClauseNode(type, identifier, catchBlock, whenClause));
        }

        if (ConsumeIfMatch(TokenKind.FinallyKeyword))
        {
            var finallyBlock = ParseBlock();
            finallyClause = new FinallyClauseNode(finallyBlock);
        }

        return new TryStatementNode(block, catchClauses, finallyClause);
    }

    private UsingStatementNode ParseUsingStatement()
    {
        Expect(TokenKind.UsingKeyword);
        var isDeclaration = !Matches(TokenKind.OpenParen);

        if (!isDeclaration)
            Expect(TokenKind.OpenParen);

        bool isVariableDeclarator = IsDeclarationStatement();

        VariableDeclarationStatement? declarator = null;
        ExpressionNode? expression = null;
        AstNode? body = null;

        if (isVariableDeclarator)
            declarator = (VariableDeclarationStatement)ParseDeclarationStatement(onlyParseDeclarator: true);
        else
            expression = ParseExpression();

        if (!isDeclaration)
            Expect(TokenKind.CloseParen);
        else
            Expect(TokenKind.Semicolon);

        if (!isDeclaration)
            body = ParseBody();

        return new UsingStatementNode(declarator, expression, body);
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

        if (TryParseTupleDeconstruction(out var tupleDeconstruct))
            return tupleDeconstruct;

        if (!isEmbeddedStatement && IsDeclarationStatement())
            return ParseDeclarationStatement();

        if (!isEmbeddedStatement && Matches(TokenKind.OpenBrace))
            return ParseBlock();

        if (IsLocalFunctionDeclaration())
            return ParseLocalFunction();

        var token = PeekCurrent();

        return token.Kind switch
        {
            // Selection statements
            TokenKind.IfKeyword => ParseIfStatement(),
            TokenKind.DoKeyword => ParseDoStatement(),
            TokenKind.ForKeyword => ParseForStatement(),
            TokenKind.ForeachKeyword => ParseForEachStatement(),
            TokenKind.WhileKeyword => ParseWhileStatement(),
            TokenKind.SwitchKeyword => ParseSwitchStatement(),
            TokenKind.BreakKeyword => ParseBreakStatement(),
            TokenKind.ContinueKeyword => ParseContinueStatement(),
            TokenKind.ReturnKeyword => ParseReturnStatement(),
            TokenKind.Semicolon => ParseEmptyStatement(),
            TokenKind.TryKeyword => ParseTryStatement(),
            TokenKind.UsingKeyword => ParseUsingStatement(),
            // If no matches parse as an expression statement
            _ => ParseExpressionStatement(),
        };
    }

    // Try not to accidentally parse using statements
    private bool IsUsingDirective()
    {
        if (MatchesLexeme("global") && Matches(TokenKind.UsingKeyword, 1))
            return true;

        if (Matches(TokenKind.UsingKeyword) && MatchesLexeme("global", peekOffset: 1))
            return true;

        bool isUsingDirective = true;

        isUsingDirective &= Matches(TokenKind.UsingKeyword);
        isUsingDirective &= Matches(TokenKind.Identifier, 1);
        isUsingDirective &= Matches(TokenKind.Equals, 2) || Matches(TokenKind.Dot, 2);

        return isUsingDirective;
    }

    private List<UsingDirectiveNode> ParseUsingDirectives()
    {
        List<UsingDirectiveNode> directives = [];

        while (!IsAtEnd() && IsUsingDirective())
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

        // Skip over possible attribute
        if (Matches(TokenKind.OpenBracket))
            TryParseAttributes(out idx, true);

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

        while (IsValidLocalFunctionModifier(PeekCurrent()))
            Consume();

        if (!IsMaybeType(PeekCurrent(), true) || !TryParseType(out _))
        {
            Seek(startPos);
            return false;
        }

        var identifier = Matches(TokenKind.Identifier)
            ? ResolveIdentifier(isMaybeGeneric: true, isInNamespaceOrType: true)
            : null;

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

    private readonly string[] _attributeTargets = [
        "assembly",
        "module",
        "field",
        "event",
        "method",
        "param",
        "property",
        "return",
        "type"
    ];

    private bool TryParseSingleAttribute([NotNullWhen(true)] out AttributeNode? attribute)
    {
        Expect(TokenKind.OpenBracket);

        List<AttributeArgumentNode> arguments = [];
        string? target = null;

        bool maybeAttribute = true;
        attribute = null;

        // At this point it's no longer safe to expect anything

        // Argument target (like assembly: ..., module: ...)
        // see https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/
        if (_attributeTargets.Contains(PeekCurrent().Lexeme) && Matches(TokenKind.Colon, 1))
        {
            target = Consume().Lexeme;
            Expect(TokenKind.Colon);
        }

        do
        {
            var expr = ParseExpression();

            if (expr is null)
            {
                maybeAttribute = false;
                break;
            }

            arguments.Add(new AttributeArgumentNode(expr));

        } while (ConsumeIfMatch(TokenKind.Comma));

        if (maybeAttribute && ConsumeIfMatch(TokenKind.CloseBracket))
        {
            attribute = new AttributeNode(arguments, target);
            return true;
        }

        return false;
    }

    private List<AttributeNode> TryParseAttributes(out int consumed, bool alwaysBacktrack = false)
    {
        var start = Tell();
        var attributes = new List<AttributeNode>();

        consumed = 0;

        while (Matches(TokenKind.OpenBracket))
        {
            if (TryParseSingleAttribute(out var attribute))
            {
                attributes.Add(attribute);
            }
            else
            {
                // If malformed backtrack
                Seek(start);
                return [];
            }
        }

        consumed = Tell() - start;

        if (alwaysBacktrack)
        {
            Seek(start);
        }

        return attributes;
    }

    private List<StatementNode> ParseTopLevelStatements()
    {
        List<StatementNode> statements = [];

        while (!IsAtEnd() && !IsTypeDeclaration() && !Matches(TokenKind.NamespaceKeyword))
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

    private static readonly List<string> ValidLocalFunctionModifiers = [
        "static",
        "async"
    ];

    private static bool IsValidTypeModifier(Token token)
    {
        return ValidClassModifiers.Contains(token.Lexeme);
    }

    private static bool IsValidLocalFunctionModifier(Token token)
    {
        return ValidLocalFunctionModifiers.Contains(token.Lexeme);
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

    private MemberNode ParseProperty(string propertyName, AccessModifier accessModifier, List<OptionalModifier> modifiers, TypeNode propertyType, List<AttributeNode> attributes)
    {
        ConsumeIfMatch(TokenKind.OpenBrace);

        bool isOnlyGetter = ConsumeIfMatch(TokenKind.EqualsGreaterThan);

        if (isOnlyGetter)
        {
            var expr = ParseExpression();
            Expect(TokenKind.Semicolon);

            var autoGetter = new PropertyAccessorNode(
                PropertyAccessorType.ExpressionBodied,
                AccessModifier.Public,
                expr,
                null,
                false
            );

            return new PropertyMemberNode(accessModifier, modifiers, propertyName, propertyType, autoGetter, null, null, attributes);
        }

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

        return new PropertyMemberNode(accessModifier, modifiers, propertyName, propertyType, getter, setter, value, attributes);
    }

    private readonly Dictionary<string, ParameterType> _parameterTypes = new()
    {
        { "ref", ParameterType.Ref },
        { "in", ParameterType.In },
        { "out", ParameterType.Out },
        { "this", ParameterType.This },
        { "params", ParameterType.Params }
    };

    private ParameterListNode ParseParameterList()
    {
        var parameters = new List<ParameterNode>();

        // @todo: support ref, out, params, optional, ...

        do
        {
            if (Matches(TokenKind.CloseParen))
                return new ParameterListNode(parameters);

            List<AttributeNode> attributes = [];

            if (Matches(TokenKind.OpenBracket))
                attributes = TryParseAttributes(out _);

            ParameterType parameterType = ParameterType.Regular;

            if (ConsumeIfMatchSequence(TokenKind.RefKeyword, TokenKind.ReadonlyKeyword))
            {
                parameterType = ParameterType.RefReadonly;
            }
            else if (_parameterTypes.TryGetValue(PeekCurrent().Lexeme, out var value))
            {
                Consume();
                parameterType = value;
            }

            var type = ParseType();
            var identifier = Consume();

            ExpressionNode? defaultValue = null;

            if (ConsumeIfMatch(TokenKind.Equals))
            {
                defaultValue = ParseExpression();
            }

            parameters.Add(new ParameterNode(type, identifier.Lexeme, defaultValue, attributes, parameterType));

        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Comma));

        return new ParameterListNode(parameters);
    }

    private ConstructorNode ParseConstructor(AccessModifier accessModifier, List<AttributeNode> attributes)
    {
        Expect(TokenKind.Identifier); // ctor name
        Expect(TokenKind.OpenParen);  // parms
        var parms = ParseParameterList();
        Expect(TokenKind.CloseParen);

        ArgumentListNode? baseArguments = null;
        ConstructorArgumentsType type = ConstructorArgumentsType.None;

        if (ConsumeIfMatch(TokenKind.Colon))
        {
            var isBase = ConsumeIfMatch(TokenKind.BaseKeyword);
            var isThis = !isBase && ConsumeIfMatch(TokenKind.ThisKeyword);

            if (!isBase && !isThis)
                throw new ParseException("Expected base or this keywords after constructor :");

            Expect(TokenKind.OpenParen);
            baseArguments = ParseArgumentList();
            Expect(TokenKind.CloseParen);

            type = isBase ? ConstructorArgumentsType.Base : ConstructorArgumentsType.This;
        }

        var body = ParseMethodBody();

        return new ConstructorNode(accessModifier, parms, baseArguments, body, type, attributes);
    }

    private MethodNode ParseMethod(AccessModifier accessModifier, List<OptionalModifier> modifiers, TypeNode returnType, AstNode methodName, List<AttributeNode> attributes)
    {
        Expect(TokenKind.OpenParen);
        var parms = ParseParameterList();
        Expect(TokenKind.CloseParen);
        AstNode? body = null;

        if (!ConsumeIfMatch(TokenKind.Semicolon))
            body = ParseMethodBody();

        return new MethodNode(accessModifier, modifiers, returnType, methodName, parms, body, attributes);
    }

    private EnumMemberNode ParseEnumMember(List<AttributeNode> attributes)
    {
        var identifier = Consume();
        ExpressionNode? value = null;

        if (ConsumeIfMatch(TokenKind.Equals))
        {
            value = ParseExpression();
        }

        ConsumeIfMatch(TokenKind.Comma);

        return new EnumMemberNode(identifier.Lexeme, value, attributes);
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
        List<AttributeNode> attributes = [];

        if (Matches(TokenKind.OpenBracket))
            attributes = TryParseAttributes(out _);

        if (kind == TypeKind.Enum)
            return ParseEnumMember(attributes);

        var name = ResolveNameFromAstNode(typeName);

        ParseModifiers(out var accessModifier, out var modifiers);
        var isCtor = PeekCurrent().Lexeme == name && PeekSafe().Kind == TokenKind.OpenParen;

        if (isCtor)
            return ParseConstructor(accessModifier ?? AccessModifier.Private, attributes);

        var type = ParseType();
        var identifier = ResolveIdentifier(isMaybeGeneric: true, isInNamespaceOrType: true);
        var isMethod = Matches(TokenKind.OpenParen);
        var isProperty = Matches(TokenKind.OpenBrace) || Matches(TokenKind.EqualsGreaterThan);
        var isField = !isMethod && !isProperty; // @todo: events?

        if (isField)
        {
            var hasValue = ConsumeIfMatch(TokenKind.Equals);
            var value = hasValue ? ParseExpression() : null;
            Expect(TokenKind.Semicolon);
            return new FieldMemberNode(accessModifier ?? AccessModifier.Private, modifiers, ResolveNameFromAstNode(identifier), type, value, attributes);
        }
        else if (isProperty)
        {
            return ParseProperty(ResolveNameFromAstNode(identifier), accessModifier ?? AccessModifier.Private, modifiers, type, attributes);
        }
        else if (isMethod)
        {
            return ParseMethod(accessModifier ?? AccessModifier.Private, modifiers, type, identifier, attributes);
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
        List<AttributeNode> attributes = [];

        if (Matches(TokenKind.OpenBracket))
            attributes = TryParseAttributes(out _);
            
        ParseModifiers(out var accessModifier, out var modifiers);

        var type = Consume().Kind;

        var identifier = ResolveIdentifier(isMaybeGeneric: true, isInNamespaceOrType: true);
        AstNode? parentName = null;

        ParameterListNode? parameters = null;
        ArgumentListNode? baseArguments = null;

        if (ConsumeIfMatch(TokenKind.OpenParen))
        {
            parameters = ParseParameterList();
            Expect(TokenKind.CloseParen);
        }

        if (ConsumeIfMatch(TokenKind.Colon))
        {
            parentName = ResolveIdentifier(isMaybeGeneric: true, isInNamespaceOrType: true);

            if (ConsumeIfMatch(TokenKind.OpenParen))
            {
                baseArguments = ParseArgumentList();
                Expect(TokenKind.CloseParen);
            }
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
            TokenKind.ClassKeyword => new ClassDeclarationNode(identifier, members, parentName, accessModifier, modifiers, attributes, parameters, baseArguments),
            TokenKind.EnumKeyword => new EnumDeclarationNode(identifier, members.Cast<EnumMemberNode>().ToList(), parentName, accessModifier, modifiers, attributes),
            TokenKind.InterfaceKeyword => new InterfaceDeclarationNode(identifier, members, parentName, accessModifier, modifiers, attributes),
            TokenKind.StructKeyword => new StructDeclarationNode(identifier, members, parentName, accessModifier, modifiers, attributes, parameters, baseArguments),
            _ => throw new NotImplementedException(),
        };
    }

    private List<AstNode> ParseTypeDeclarationsAndNamespaces()
    {
        var declarationsAndNamespaces = new List<AstNode>();

        while (!IsAtEnd())
        {
            if (IsTypeDeclaration())
                declarationsAndNamespaces.Add(ParseTypeDeclaration());
            else if (Matches(TokenKind.NamespaceKeyword))
                declarationsAndNamespaces.Add(ParseNamespace());
            else break;
        }

        return declarationsAndNamespaces;
    }

    private string QualifiedNameToString(AstNode qualifiedName)
    {
        if (qualifiedName is IdentifierExpression identifierExpression)
            return identifierExpression.Identifier;

        var qn = (QualifiedNameNode)qualifiedName;

        return $"{QualifiedNameToString(qn.LHS)}.{qn.Identifier}";
    }

    private NamespaceNode ParseNamespace(bool isGlobal = false, bool allowTopLevelStatements = false)
    {
        Expect(TokenKind.NamespaceKeyword);
        var name = ParseQualifiedName(); // @fixme: qualified name or member access?
        var isFileScoped = ConsumeIfMatch(TokenKind.Semicolon);

        if (!isFileScoped)
            Expect(TokenKind.OpenBrace);

        var ns = ParseNamespaceContent(QualifiedNameToString(name), isFileScoped, isGlobal, allowTopLevelStatements);

        if (!isFileScoped)
            Expect(TokenKind.CloseBrace);

        return ns;
    }

    private readonly List<string> _topLevelAttributeTargets = [
        "module",
        "assembly"
    ];

    private List<AttributeNode> ParseTopLevelAttributes()
    {
        var attributes = new List<AttributeNode>();

        while (Matches(TokenKind.OpenBracket))
        {
            var marker = Tell();

            if (!TryParseSingleAttribute(out var attribute) || 
                attribute.Target is null ||
                !_topLevelAttributeTargets.Contains(attribute.Target))
            {
                Seek(marker);
                break;
            }

            attributes.Add(attribute);
        }

        return attributes;
    }

    private NamespaceNode ParseNamespaceContent(string name, bool isFileScoped, bool isGlobal, bool allowTopLevelStatements = false)
    {
        var ns = isGlobal ? new GlobalNamespaceNode() : new NamespaceNode(name, isFileScoped);
        var directives = ParseUsingDirectives();

        // try parse assembly/module

        var attributes = ParseTopLevelAttributes();

        // the 'rule' in C# is that top-level statements must precede any type declarations and namespaces
        // this method stops the moment it encounters a type declaration
        if (ns is GlobalNamespaceNode globalNamespace && allowTopLevelStatements)
        {
            var statements = ParseTopLevelStatements()
                .Select(s => new GlobalStatementNode(s)).ToList();

            globalNamespace.GlobalStatements.AddRange(statements!);
        }

        var declarationsAndNamespaces = ParseTypeDeclarationsAndNamespaces();

        var typeDeclarations = declarationsAndNamespaces.OfType<TypeDeclarationNode>().ToList();
        var namespaces = declarationsAndNamespaces.OfType<NamespaceNode>().ToList();

        ns.UsingDirectives.AddRange(directives);
        ns.Attributes.AddRange(attributes);
        ns.TypeDeclarations.AddRange(typeDeclarations);
        ns.Namespaces.AddRange(namespaces);

        return ns;
    }

    private AST ParseInternal(Token[] tokens)
    {
        var ast = new AST { Root = new() };

        if (tokens.Length == 0)
            return ast;

        _input = tokens;

        ast.Root = (GlobalNamespaceNode)ParseNamespaceContent(string.Empty, true, true, true);

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