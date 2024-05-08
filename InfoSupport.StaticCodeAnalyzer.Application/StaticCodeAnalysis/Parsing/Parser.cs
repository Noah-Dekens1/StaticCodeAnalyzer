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
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

readonly struct MemberData(
    Token token, 
    CodeLocation location,
    TypeArgumentsNode? typeArguments = null, 
    bool isConditional = false, 
    bool isNullForgiving = false
    )
{
    public readonly Token Token = token;
    public readonly TypeArgumentsNode? TypeArguments = typeArguments;
    public readonly bool IsConditional = isConditional;
    public readonly bool IsNullForgiving = isNullForgiving;
    public readonly CodeLocation Location = location;
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

    private Position GetStartPosition()
    {
        return _input[_index].Start;
    }

    private Position GetEndPosition()
    {
        return _input[_index - 1].End;
    }

    private static T EmitInternal<T>(T node, Position start, Position end) where T : AstNode
    {
        node.Location = new CodeLocation(start, end);
#if DEBUG
        node.ConstructedInEmit = true;
#endif
        return node;
    }


    private static T EmitStatic<T>(T node, Position start, Position end) where T : AstNode
    {
        return EmitInternal(node, start, end);
    }

    private static T EmitStatic<T>(T node, Token token) where T : AstNode
    {
        return EmitInternal(node, token.Start, token.End);
    }

    private static T EmitStatic<T>(T node, CodeLocation location) where T : AstNode
    {
        node.Location = location;
#if DEBUG
        node.ConstructedInEmit = true;
#endif
        return node;
    }


    private T Emit<T>(T node, Position start, Position? end = null) where T : AstNode
    {
        return EmitInternal(node, start, end ?? GetEndPosition());
    }

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

    [DebuggerHidden]
    public bool ConsumeIfMatchLexeme(string lexeme, TokenKind? kind = null)
    {
        if (MatchesLexeme(lexeme, kind))
        {
            Consume();
            return true;
        }

        return false;
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

    private static StringInterpolationNode ParseInterpolation(string str, out int read, Position locationStart, int expectedBraces = 1)
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
                var inner = ParseStringLiteral(str[(j + 1)..], new CodeLocation());

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

        var tokens = Lexer.Lex(interpolationBuilder.ToString(), locationStart);
        var expr = ParseExpressionFromTokens(tokens);

        read = i + 1; // include trailing }

        // @fixme: this will break on multiline interpolated strings
        return EmitStatic(new StringInterpolationNode(expr), locationStart, new Position(locationStart.Line, locationStart.Column + (ulong)i - 1));
    }

    private static StringLiteralData ParseStringLiteral(string str, CodeLocation location)
    {
        int i = 0;
        int dollarSigns = 0;
        int quotes = 0;
        bool isVerbatim = false;

        int lineOffset = 0;
        int columnOffset = 0;

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

        columnOffset = i;

        int lastIndex = i - 1;

        while (i < str.Length)
        {
            var c = str[i];
            var n = i + 1 < str.Length ? str[i + 1] : '\0';

            if (c == '\n')
            {
                lineOffset++;
                columnOffset = 0;
            }
            else
            {
                columnOffset += i - lastIndex;
            }

            lastIndex = i;

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
                            var interpolationStart = new Position(location.Start.Line, location.Start.Column);
                            interpolationStart.Add(new Position((ulong)lineOffset, (ulong)columnOffset));

                            interpolations.Add(ParseInterpolation(target, out var read, interpolationStart, expectedBraces: 1));
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

        var start = token.Start;
        var end = token.End;

        literal = null;

        if (kind == TokenKind.NumericLiteral)
        {
            //Consume();
            literal = EmitStatic(new NumericLiteralNode(token.Value), token);
            return true;
        }
        else if (kind == TokenKind.StringLiteral || kind == TokenKind.InterpolatedStringLiteral)
        {
            var data = ParseStringLiteral(token.Lexeme, new CodeLocation(token.Start, token.End));

            literal = data.IsInterpolated
                ? Emit(new InterpolatedStringLiteralNode(data.Content, data.Interpolations), start, end)
                : Emit(new StringLiteralNode(data.Content), start, end);

            return true;
        }
        else if (kind == TokenKind.CharLiteral)
        {
            literal = Emit(new CharLiteralNode(ParseCharLiteral(token.Lexeme)), start, end);
            return true;
        }
        else if (kind == TokenKind.TrueKeyword || kind == TokenKind.FalseKeyword)
        {
            var value = kind == TokenKind.TrueKeyword;
            literal = Emit(new BooleanLiteralNode(value), start, end);
            return true;
        }
        else if (kind == TokenKind.NullKeyword)
        {
            literal = Emit(new NullLiteralNode(), start, end);
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
            var start = op.Start;

            bool isIncrementOrDecrement = op.Kind == TokenKind.PlusPlus || op.Kind == TokenKind.MinusMinus; // @todo: parse this earlier to an actual op like with binary expressions
            var expr = isIncrementOrDecrement ? ParseIdentifierOrLiteral() : ParseExpression(null, true)!; // Either ParseExpression or ParseIdentifierOrLiteral depending on unary operator (generic vs increment/decrement)
            UnaryExpressionNode? result = null;

            switch (op.Kind)
            {
                case TokenKind.PlusPlus:
                    result = Emit(new UnaryIncrementNode(expr, isPrefix), start);
                    break;
                case TokenKind.MinusMinus:
                    result = Emit(new UnaryDecrementNode(expr, isPrefix), start);
                    break;
                case TokenKind.Minus:
                    result = Emit(new UnaryNegationNode(expr), start);
                    break;
                case TokenKind.Exclamation:
                    result = Emit(new UnaryLogicalNotNode(expr), start);
                    break;
                case TokenKind.Tilde:
                    result = Emit(new UnaryBitwiseComplementNode(expr), start);
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
                    result = Emit(new UnaryIncrementNode(identifierOrLiteral, false), identifierOrLiteral.Location.Start);
                    break;
                case TokenKind.MinusMinus:
                    result = Emit(new UnaryDecrementNode(identifierOrLiteral, false), identifierOrLiteral.Location.Start);
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

            // Binary
            case TokenKind.Caret:
            case TokenKind.CaretEquals:
            case TokenKind.Ampersand:
            case TokenKind.Bar:


                // ...
                return true;
            default: return false;
        }
    }

    private BinaryExpressionNode ParseBinaryExpression(ExpressionNode lhs)
    {
        if (ConsumeIfMatchSequence(TokenKind.LessThan, TokenKind.LessThanEquals))
            return Emit(new LeftShiftAssignExpressionNode(lhs, ParseExpression()!), lhs.Location.Start);

        if (ConsumeIfMatchSequence(TokenKind.LessThan, TokenKind.LessThan))
            return Emit(new LeftShiftExpressionNode(lhs, ParseExpression()!), lhs.Location.Start);

        if (ConsumeIfMatchSequence(TokenKind.GreaterThan, TokenKind.GreaterThanEquals))
            return Emit(new RightShiftAssignExpressionNode(lhs, ParseExpression()!), lhs.Location.Start);

        if (ConsumeIfMatchSequence(TokenKind.GreaterThan, TokenKind.GreaterThan))
            return Emit(new RightShiftExpressionNode(lhs, ParseExpression()!), lhs.Location.Start);


        var binaryOperator = Consume();

        var rhs = ParseExpression()!;

        if (rhs is null)
        {
            throw new InvalidOperationException();
        }

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
            { TokenKind.AmpersandAmpersand,  () => new ConditionalAndExpressionNode(lhs, rhs) },
            { TokenKind.BarBar,              () => new ConditionalOrExpressionNode(lhs, rhs) },

            { TokenKind.PlusEquals,          () => new AddAssignExpressionNode(lhs, rhs) },
            { TokenKind.MinusEquals,         () => new SubtractAssignExpressionNode(lhs, rhs) },
            { TokenKind.AsteriskEquals,      () => new MultiplyAssignExpressionNode(lhs, rhs) },
            { TokenKind.SlashEquals,         () => new DivideAssignExpressionNode(lhs, rhs) },
            { TokenKind.PercentEquals,       () => new ModulusAssignExpressionNode(lhs, rhs) },
            { TokenKind.AmpersandEquals,     () => new AndAssignExpressionNode(lhs, rhs) },
            { TokenKind.BarEquals,           () => new OrAssignExpressionNode(lhs, rhs) },
            { TokenKind.CaretEquals,         () => new LogicalXorAssignExpressionNode(lhs, rhs) },
            
            { TokenKind.Ampersand,           () => new LogicalAndExpressionNode(lhs, rhs) },
            { TokenKind.Bar,                 () => new LogicalOrExpressionNode(lhs, rhs) },
            { TokenKind.Caret,               () => new LogicalXorExpressionNode(lhs, rhs) },

            { TokenKind.Equals,              () => new AssignmentExpressionNode(lhs, rhs) },
            { TokenKind.QuestionQuestion,    () => new NullCoalescingExpressionNode(lhs, rhs) },

            { TokenKind.QuestionQuestionEquals, () => new NullCoalescingAssignmentExpressionNode(lhs, rhs) },
        };


        return Emit(operators[binaryOperator.Kind](), lhs.Location.Start, rhs.Location.End);
    }

    private static ExpressionNode ResolveMemberAccess(List<MemberData> members, ExpressionNode? lhsExpr = null, bool lhsConditional = false)
    {
        var member = members[^1];
        ExpressionNode identifier = EmitStatic(new IdentifierExpression(member.Token.Lexeme), member.Token);

        identifier = member.TypeArguments is null
            ? identifier
            : EmitStatic(new GenericNameNode(identifier, member.TypeArguments), member.Token);

        identifier = member.IsNullForgiving
            ? EmitStatic(new NullForgivingExpressionNode(identifier), member.Location)
            : identifier;

        if (members.Count == 1 && lhsExpr is null)   
            return identifier;
        else if (members.Count == 1 && lhsExpr is not null)
            return lhsConditional
                ? EmitStatic(new ConditionalMemberAccessExpressionNode(lhsExpr, identifier), lhsExpr.Location.Start, identifier.Location.End)
                : EmitStatic(new MemberAccessExpressionNode(lhsExpr, identifier), lhsExpr.Location.Start, identifier.Location.End);

        members.Remove(member);

        var prev = members[^1];

        var lhs = ResolveMemberAccess(members, lhsExpr, lhsConditional);

        return prev.IsConditional
            ? EmitStatic(new ConditionalMemberAccessExpressionNode(lhs, identifier), lhs.Location.Start, identifier.Location.End)
            : EmitStatic(new MemberAccessExpressionNode(
                lhs: lhs,
                identifier: identifier
            ), lhs.Location.Start, identifier.Location.End);
    }

    private ExpressionNode ResolveIdentifier(bool isMaybeGeneric = false, bool isInNamespaceOrType = false, ExpressionNode? lhs = null, bool lhsConditional = false)
    {
        var isGlobal = MatchesLexeme("global") && Matches(TokenKind.ColonColon, 1);

        var start = GetStartPosition();

        if (isGlobal)
        {
            Consume();
            Expect(TokenKind.ColonColon);
        }

        List<MemberData> members = [];

        do
        {
            var memberStart = GetStartPosition();
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
            var location = new CodeLocation(memberStart, GetEndPosition());

            members.Add(new MemberData(token, location, typeArguments, isConditional, isForgiving));

        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Dot) && Matches(TokenKind.Identifier));

        var memberAccess = ResolveMemberAccess(members, lhs, lhsConditional);

        if (isGlobal)
            memberAccess = Emit(new GlobalNamespaceQualifierNode(memberAccess), start);

        return memberAccess;
    }

    private InvocationExpressionNode ParseInvocation(ExpressionNode lhs)
    {
        var start = GetStartPosition(); // @note: maybe it makes more sense to include the LHS?
        Expect(TokenKind.OpenParen);
        var arguments = ParseArgumentList();
        Expect(TokenKind.CloseParen);
        return Emit(new InvocationExpressionNode(lhs, arguments), start);
    }

    private ElementAccessExpressionNode ParseElementAccess(ExpressionNode lhs)
    {
        var start = GetStartPosition(); // use lhs maybe?
        bool isConditional = ConsumeIfMatch(TokenKind.Question);
        Expect(TokenKind.OpenBracket);

        var expr = ParseExpression();
        expr = expr is RangeExpressionNode
            ? expr
            : ToIndexExpression(expr)!;

        var args = Emit(new BracketedArgumentList([EmitStatic(new ArgumentNode(expr), expr.Location)]), start);

        Expect(TokenKind.CloseBracket);

        return isConditional
            ? Emit(new ConditionalElementAccessExpressionNode(lhs, args), start)
            : Emit(new ElementAccessExpressionNode(lhs, args), start);
    }

    private IndexedCollectionInitializerNode ParseIndexedCollectionInitializerElement()
    {
        var start = GetStartPosition();
        // @todo: support multiple expressions in bracket? For example for jagged arrays
        bool isBracketedIndexer = ConsumeIfMatch(TokenKind.OpenBracket);

        var indexer = isBracketedIndexer ? ParseExpression()! : ResolveIdentifier();

        if (isBracketedIndexer)
            Expect(TokenKind.CloseBracket);

        Expect(TokenKind.Equals);

        var value = ParseExpression()!;

        return Emit(new IndexedCollectionInitializerNode(indexer, value), start);
    }

    private ComplexCollectionInitializerNode ParseComplexCollectionInitializerElement()
    {
        var start = GetStartPosition();
        var values = new List<ExpressionNode>();

        Expect(TokenKind.OpenBrace);

        do
        {
            if (Matches(TokenKind.CloseBrace))
                break;

            values.Add(ParseExpression()!);
        } while (ConsumeIfMatch(TokenKind.Comma));

        Expect(TokenKind.CloseBrace);

        return Emit(new ComplexCollectionInitializerNode(values), start);
    }

    private RegularCollectionInitializerNode ParseRegularCollectionInitializerElement()
    {
        var start = GetStartPosition();
        return Emit(new RegularCollectionInitializerNode(ParseExpression()!), start);
    }

    private ExpressionNode? TryParsePrimaryExpression()
    {
        var start = GetStartPosition();

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

            bool _ = IsMaybeType(PeekCurrent(), true) && TryParseTypeOrBacktrack(out type);

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
                var initializerStart = GetStartPosition();
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

                initializer = Emit(new CollectionInitializerNode(values), initializerStart);

                Expect(TokenKind.CloseBrace);
            }

            return Emit(new ObjectCreationExpressionNode(type, isArrayCreation, args, initializer), start);
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
                        ? Emit(new SpreadElementNode(ParseExpression()!), start)
                        : Emit(new ExpressionElementNode(ParseExpression()!), start)
                );

            } while (ConsumeIfMatch(TokenKind.Comma));

            Expect(TokenKind.CloseBracket);

            return Emit(new CollectionExpressionNode(elements), start);
        }

        if (ConsumeIfMatch(TokenKind.TypeofKeyword))
        {
            Expect(TokenKind.OpenParen);

            var type = ParseType()!;

            Expect(TokenKind.CloseParen);

            return Emit(new TypeofExpressionNode(type), start);
        }

        if (MatchesLexeme("nameof"))
        {
            Consume();

            Expect(TokenKind.OpenParen);

            AstNode value = (AstNode?)ParseExpression() ?? ParseType()!;

            Expect(TokenKind.CloseParen);

            return Emit(new NameofExpressionNode(value), start);
        }

        if (ConsumeIfMatch(TokenKind.SizeofKeyword))
        {
            Expect(TokenKind.OpenParen);

            var type = ParseType();

            Expect(TokenKind.CloseParen);

            return Emit(new SizeofExpressionNode(type!), start);
        }

        if (ConsumeIfMatch(TokenKind.DefaultKeyword))
        {
            // Default operator | default(int)
            if (ConsumeIfMatch(TokenKind.OpenParen))
            {
                var type = ParseType();

                Expect(TokenKind.CloseParen);

                return Emit(new DefaultOperatorExpressionNode(type), start);
            }

            return Emit(new DefaultLiteralNode(), start);
        }

        if (MatchesLexeme("await"))
        {
            Consume();

            var expression = ParseExpression()!;
            return Emit(new AwaitExpressionNode(expression), start);
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

        return Emit(new LambdaExpressionNode([EmitStatic(new LambdaParameterNode(paramIdentifier), param.Location)], ParseLambdaBody()), param.Location.Start);
    }

    private NullForgivingExpressionNode ParseNullForgivingExpression(ExpressionNode lhs)
    {
        Expect(TokenKind.Exclamation);
        return Emit(new NullForgivingExpressionNode(lhs), lhs.Location.Start);
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

        if (Matches(TokenKind.EqualsGreaterThan) && !isParsingPattern && resolvedIdentifier is IdentifierExpression)
            return ParseLambdaExpressionSingleParam(resolvedIdentifier);

        return null;
    }

    private IsExpressionNode? ParseIsPatternExpression(ExpressionNode lhs)
    {
        var start = GetStartPosition();
        Expect(TokenKind.IsKeyword);

        var pattern = ParsePattern()!;

        return Emit(new IsExpressionNode(lhs, pattern), start);
    }

    private bool TryParseTupleExpression([NotNullWhen(true)] out TupleExpressionNode? tupleExpression, Position startPosition)
    {
        var start = Tell();

        List<TupleArgumentNode> tupleArguments = [];

        tupleExpression = null;

        do
        {
            var argumentStart = GetStartPosition();
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

            tupleArguments.Add(Emit(new TupleArgumentNode(expression: expr, name: name), argumentStart));

        } while (ConsumeIfMatch(TokenKind.Comma));

        if (!ConsumeIfMatch(TokenKind.CloseParen) || tupleArguments.Count < 2)
        {
            Seek(start);
            return false;
        }

        tupleExpression = Emit(new TupleExpressionNode(tupleArguments), startPosition);
        return true;
    }

    private bool TryParseLambdaExpression([NotNullWhen(true)] out LambdaExpressionNode? lambdaExpression)
    {
        var startPosition = GetStartPosition();
        var start = Tell();

        List<LambdaParameterNode> parameters = [];

        bool isFirst = true;
        bool isImplicit = true;
        bool isAsync = false;

        lambdaExpression = null;

        if (MatchesLexeme("async"))
        {
            Consume();
            isAsync = true;
        }

        if (!ConsumeIfMatch(TokenKind.OpenParen))
        {
            if (!isAsync) // non-async single param lambda's are handled elsewhere
            {
                Seek(start);
                return false;
            }

            var identifierExpr = Matches(TokenKind.Identifier) ? ResolveIdentifier() : null;

            if (identifierExpr is null || !ConsumeIfMatch(TokenKind.EqualsGreaterThan))
            {
                Seek(start);
                return false;
            }

            var identifier = ((IdentifierExpression)identifierExpr).Identifier;

            lambdaExpression = Emit(new LambdaExpressionNode([EmitStatic(new LambdaParameterNode(identifier), identifierExpr.Location)], ParseLambdaBody(), isAsync: true), startPosition);
            return true;
        }

        do
        {
            if (Matches(TokenKind.CloseParen))
                break;

            var paramStart = GetStartPosition();

            // check for a,b => or
            var hasType = !Matches(TokenKind.Comma, 1) &&
                !Matches(TokenKind.EqualsGreaterThan, 1) &&
                !Matches(TokenKind.CloseParen, 1);

            TypeNode? type = null;

            if (hasType)
                hasType = TryParseType(out type);


            if (isFirst)
                isImplicit = !hasType;

            if (!hasType != isImplicit) // must all be implicit/explicit, exit as early as possible if not the case
            {
                Seek(start);
                return false;
            }

            if (!Matches(TokenKind.Identifier))
            {
                Seek(start);
                return false;
            }

            var identifier = Consume();
            parameters.Add(Emit(new LambdaParameterNode(identifier.Lexeme, type), paramStart));
            isFirst = false;
        } while (ConsumeIfMatch(TokenKind.Comma));

        if (!ConsumeIfMatch(TokenKind.CloseParen))
        {
            Seek(start);
            return false;
        }

        if (!ConsumeIfMatch(TokenKind.EqualsGreaterThan))
        {
            Seek(start);
            return false;
        }

        lambdaExpression = Emit(new LambdaExpressionNode(parameters, ParseLambdaBody(), isAsync), startPosition);
        return true;
    }

    private ExpressionNode? ParseStartParenthesisExpression()
    {
        var startPosition = GetStartPosition();

        Expect(TokenKind.OpenParen);

        var start = Tell();

        var maybeCast = true;
        {
            bool isType = TryParseType(out var type);

            maybeCast &= isType && ConsumeIfMatch(TokenKind.CloseParen);
            var rhs = ParseExpression(null, true);
            maybeCast &= rhs is not null;

            if (maybeCast)
                return Emit(new CastExpressionNode(type!, rhs!), startPosition);
                
            Seek(start);
        }

        if (TryParseTupleExpression(out var tupleExpr, startPosition))
        {
            return tupleExpr;
        }

        var parenthesizedExpr = ParseExpression()!;
        Expect(TokenKind.CloseParen);
        return Emit(new ParenthesizedExpressionNode(parenthesizedExpr), startPosition);
    }

    private SwitchExpressionNode ParseSwitchExpression(ExpressionNode expr)
    {
        var start = GetStartPosition();
        Expect(TokenKind.SwitchKeyword);

        Expect(TokenKind.OpenBrace);

        List<SwitchExpressionArmNode> arms = [];

        do
        {
            if (Matches(TokenKind.CloseBrace))
                break;

            var armStart = GetStartPosition();

            PatternNode caseValue = ParsePattern()!;
            ExpressionNode? whenClause = null;

            if (MatchesLexeme("when", TokenKind.Identifier))
            {
                Expect(TokenKind.Identifier);
                whenClause = ParseExpression()!; // binary expr
            }

            Expect(TokenKind.EqualsGreaterThan);

            var resultExpr = ParseExpression()!;

            arms.Add(Emit(new SwitchExpressionArmNode(caseValue, resultExpr, whenClause), armStart));

        } while (ConsumeIfMatch(TokenKind.Comma));

        Expect(TokenKind.CloseBrace);

        return Emit(new SwitchExpressionNode(expr, arms), start);
    }

    private bool IsTernaryOperator()
    {
        return Matches(TokenKind.Question) && !Matches(TokenKind.Dot, 1) && !Matches(TokenKind.Question, 1);
    }

    private readonly List<string> _contextualKeywords = [
        "nameof",
        "await"
    ];

    private TernaryExpressionNode ParseTernaryExpression(ExpressionNode lhs)
    {
        Expect(TokenKind.Question);
        var trueExpr = ParseExpression()!;
        Expect(TokenKind.Colon);
        var falseExpr = ParseExpression()!;

        return Emit(new TernaryExpressionNode(lhs, trueExpr, falseExpr), lhs.Location.Start);
    }

    private IndexExpressionNode ParseIndexExpressionFromEnd()
    {
        var start = GetStartPosition();
        Expect(TokenKind.Caret);
        return Emit(new IndexExpressionNode(expression: ParseExpression(isParsingIndex: true)!, fromEnd: true), start);
    }

    private static IndexExpressionNode? ToIndexExpression(ExpressionNode? expression)
    {
        return expression is IndexExpressionNode indexExpression
            ? indexExpression
            : expression is not null
                ? EmitStatic(new IndexExpressionNode(expression), expression.Location)
                : null;
    }

    private RangeExpressionNode? ParseRangeExpression(ExpressionNode? lhs)
    {
        var start = lhs?.Location.Start ?? GetStartPosition();
        Expect(TokenKind.DotDot);

        var rhs = ParseExpression();

        var lhsIndex = ToIndexExpression(lhs);
        var rhsIndex = ToIndexExpression(rhs);

        return Emit(new RangeExpressionNode(lhsIndex, rhsIndex), start);
    }

    private AsExpressionNode ParseAsExpression(ExpressionNode lhs)
    {
        Expect(TokenKind.AsKeyword);
        var type = ParseType();

        return Emit(new AsExpressionNode(lhs, type), lhs.Location.Start);
    }

    // @note: pretty much everything in C# is an expression so we probably want to split this up
    private ExpressionNode? ParseExpression(ExpressionNode? possibleLHS = null, bool onlyParseSingle = false, bool isParsingIndex = false, bool isParsingPattern = false)
    {
        var start = possibleLHS?.Location.Start ?? GetStartPosition();
        var token = PeekCurrent();

        if (TryParseLambdaExpression(out var lambda))
        {
            return lambda;
        }

        if (ConsumeIfMatch(TokenKind.ThisKeyword))
        {
            possibleLHS = Emit(new ThisExpressionNode(), start);
        }
        else if (ConsumeIfMatch(TokenKind.BaseKeyword))
        {
            possibleLHS = Emit(new BaseExpressionNode(), start);
        }

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

        ExpressionNode? primaryExpression = possibleLHS is null
            ? TryParsePrimaryExpression()
            : null;

        if (primaryExpression is not null)
        {
            possibleLHS = primaryExpression;
            return ParseExpression(possibleLHS);
        }
        else if (possibleLHS is not null)
        {
            var primaryPostfixExpression = TryParsePrimaryPostfixExpression(possibleLHS, isParsingPattern);
            if (primaryPostfixExpression is not null)
            {
                possibleLHS = primaryPostfixExpression;
                return ParseExpression(possibleLHS);
            }

        }

        var isLiteral = PeekLiteralExpression(out var literal);

        bool isUnary = IsUnaryOperator(possibleLHS is not null, out var isPrefix);


        if (isUnary)
        {
            var unaryExpr = ParseUnaryExpression(isPrefix, possibleLHS); // may be the final symbol in the expr
            //var groupExpr = ParseExpression(unaryExpr);

            //return unaryExpr;
            possibleLHS = unaryExpr; // try to see if we're the LHS of a binary or ternary expression
        }

        if (isCurrentTokenIdentifier && possibleLHS is null)
        {
            Consume();
            possibleLHS = Emit(new IdentifierExpression(token.Lexeme), start);
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

        bool isBinary = !onlyParseSingle && !isParsingPattern && IsBinaryOperator(/*(possibleLHS is null && !isCurrentTokenIdentifier) ? 1 :*/ 0);
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

        if (possibleLHS is not null && (
            Matches(TokenKind.Dot) && Matches(TokenKind.Identifier, 1) ||
            Matches(TokenKind.Question) && Matches(TokenKind.Dot, 1)
            ))
        {
            // Resolve member access
            bool isConditional = Matches(TokenKind.Question);

            if (isConditional)
                Expect(TokenKind.Question);
            
            Expect(TokenKind.Dot);

            possibleLHS = ResolveIdentifier(isMaybeGeneric: true, lhs: possibleLHS, lhsConditional: isConditional);
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

        var start = GetStartPosition();

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
            
            if (!TryParseType(out var type))
            {
                Seek(startPosition);
                return false;
            }

            temp.Add(type);

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

        typeArguments = Emit(new TypeArgumentsNode(temp), start);

        return true;
    }

    private bool TryParseTupleType([NotNullWhen(true)] out TupleTypeNode? tupleType)
    {
        var start = GetStartPosition();

        List<TupleTypeElementNode> elements = [];
        tupleType = null;

        do
        {
            var elementStart = GetStartPosition();

            if (!TryParseType(out TypeNode? elementType))
            {
                return false;
            }

            string? identifier = null;

            if (Matches(TokenKind.Identifier))
            {
                identifier = Consume().Lexeme;
            }

            elements.Add(Emit(new TupleTypeElementNode(elementType, identifier), elementStart));

        } while (ConsumeIfMatch(TokenKind.Comma));

        if (!ConsumeIfMatch(TokenKind.CloseParen))
            return false;

        tupleType = Emit(new TupleTypeNode(elements), start);
        return true;
    }

    /**
     * Attempts to parse a type
     * Does NOT backtrack in case of failure, it will only return false
     */
    private bool TryParseType([NotNullWhen(true)] out TypeNode? type)
    {
        var start = GetStartPosition();
        type = null;

        if (ConsumeIfMatch(TokenKind.OpenParen))
        {
            if (TryParseTupleType(out var tuple))
            {
                type = Emit(new TypeNode(tuple), start);
                return true;
            }

            return false;
        }

        var baseType = IsMaybeType(PeekCurrent(), true) ? ResolveIdentifier() : null;

        type = null;

        if (baseType is null)
        {
            return false;
        }

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

            if (!Matches(TokenKind.CloseBracket))
            {
                arrayData.ArrayRank = ParseExpression();
                arrayData.RankOmitted = arrayData.ArrayRank is null;

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

        type = Emit(new TypeNode(baseType, typeArguments, arrayData, isNullable || (maybeArrayNullable && !arrayData.IsArray)), start);
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

    private bool TryParseDeclarationStatement([NotNullWhen(true)] out StatementNode? statement, bool onlyParseDeclarator=false)
    {
        var startPosition = GetStartPosition();
        var start = Tell();

        statement = null;

        bool isConst = ConsumeIfMatch(TokenKind.ConstKeyword);

        if (!TryParseType(out var type))
        {
            Seek(start);
            return false;
        }

        var declarators = new List<VariableDeclaratorNode>();

        do
        {
            var declaratorStart = GetStartPosition();

            if (!Matches(TokenKind.Identifier))
            {
                Seek(start);
                return false;
            }

            var identifier = Consume().Lexeme;
            bool hasDefinition = ConsumeIfMatch(TokenKind.Equals);
            ExpressionNode? value = null;

            if (hasDefinition)
            {
                value = ParseExpression();

                if (value is null)
                {
                    Seek(start);
                    return false;
                }
            }

            declarators.Add(Emit(new VariableDeclaratorNode(identifier, value), declaratorStart));

        } while (ConsumeIfMatch(TokenKind.Comma));

        if (!onlyParseDeclarator && !ConsumeIfMatch(TokenKind.Semicolon))
        {
            Seek(start);
            return false;
        }

        statement = Emit(new VariableDeclarationStatement(type, declarators, isConst), startPosition);
        return true;
    }
    private bool ParseTupleDesignations(out List<TupleElementNode> designations)
    {
        designations = [];

        do
        {
            var start = GetStartPosition();
            bool hasType = !Matches(TokenKind.Comma, 1) && !Matches(TokenKind.CloseParen, 1);

            TypeNode? elementType = null;

            if (hasType && !TryParseType(out elementType))
            {
                return false;
            }

            if (!Matches(TokenKind.Identifier))
            {
                return false;
            }

            var identifier = Consume().Lexeme;

            designations.Add(Emit(new TupleElementNode(identifier, elementType), start));

        } while (ConsumeIfMatch(TokenKind.Comma));

        return true;
    }

    private bool TryParseTupleDeconstruction([NotNullWhen(true)] out TupleDeconstructStatementNode? tupleDeconstruction)
    {
        var startPosition = GetStartPosition();
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
            var elementStart = GetStartPosition();
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

            designations.Add(Emit(new TupleElementNode(identifier, elementType), elementStart));

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

        tupleDeconstruction = Emit(new TupleDeconstructStatementNode(designations, expr, specifiedType: type), startPosition);
        return true;
    }

    private ExpressionStatementNode ParseExpressionStatement(bool expectSemicolon = true)
    {
        var start = GetStartPosition();
        var expr = ParseExpression();
        if (expectSemicolon)
            Expect(TokenKind.Semicolon);

        return Emit(new ExpressionStatementNode(expr!), start);
    }

    private BlockNode ParseBlock()
    {
        var start = GetStartPosition();
        Expect(TokenKind.OpenBrace);
        var statements = ParseStatementList();
        Expect(TokenKind.CloseBrace);

        return Emit(new BlockNode(statements), start);
    }

    private AstNode ParseBody()
    {
        // Could be either an embedded statement or a block?
        return PeekCurrent().Kind == TokenKind.OpenBrace ? ParseBlock() : ParseStatement(isEmbeddedStatement: true);
    }

    private readonly Dictionary<string, GenericConstraintType> _constraintTypes = new()
    {
        ["struct"] = GenericConstraintType.Struct,
        ["class"] = GenericConstraintType.Class,
        ["class?"] = GenericConstraintType.NullableClass,
        ["notnull"] = GenericConstraintType.NotNull,
        ["unmanaged"] = GenericConstraintType.Unmanaged,
        ["new()"] = GenericConstraintType.New,
        ["default"] = GenericConstraintType.Default
    };

    private List<WhereConstraintNode> ParseGenericConstraints()
    {
        var start = GetStartPosition();
        List<WhereConstraintNode> whereConstraints = [];

        while (ConsumeIfMatchLexeme("where"))
        {
            var whereStart = GetStartPosition();
            var target = ParseType()!; // is this needed? it's just an identifier right?
            Expect(TokenKind.Colon);

            List<GenericConstraintNode> constraints = [];

            do
            {
                var constraintStart = GetStartPosition();
                GenericConstraintType? constraintType;
                TypeNode? baseType = null;

                // new()
                if (ConsumeIfMatchSequence(TokenKind.NewKeyword, TokenKind.OpenParen, TokenKind.CloseParen))
                {
                    constraintType = GenericConstraintType.New;
                }
                // class?
                else if (ConsumeIfMatchSequence(TokenKind.ClassKeyword, TokenKind.OpenParen))
                {
                    constraintType = GenericConstraintType.NullableClass;
                }
                else if (_constraintTypes.TryGetValue(PeekCurrent().Lexeme, out var temp))
                {
                    Consume();
                    constraintType = temp;
                }
                else
                {
                    constraintType = GenericConstraintType.Type;
                    baseType = ParseType();
                }

                constraints.Add(Emit(new GenericConstraintNode(constraintType.Value, baseType), constraintStart));

            } while (ConsumeIfMatch(TokenKind.Comma));

            whereConstraints.Add(Emit(new WhereConstraintNode(target, constraints), whereStart));
        }

        return whereConstraints;
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
        var start = GetStartPosition();
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

        return Emit(new IfStatementNode(expr, body, elseBody), start);
    }

    private DoStatementNode ParseDoStatement()
    {
        var start = GetStartPosition();

        Expect(TokenKind.DoKeyword);
        var body = ParseBody()!;
        Expect(TokenKind.WhileKeyword);
        Expect(TokenKind.OpenParen);
        var expr = ParseExpression()!;
        Expect(TokenKind.CloseParen);
        Expect(TokenKind.Semicolon);

        return Emit(new DoStatementNode(expr, body), start);
    }

    private AstNode ParseCommaSeperatedExpressionStatements()
    {
        var start = GetStartPosition();
        var statements = new List<ExpressionStatementNode>();

        if (Matches(TokenKind.Semicolon) || Matches(TokenKind.CloseParen))
            return Emit(new ExpressionStatementListNode(statements), start);

        do
        {
            var expr = ParseExpressionStatement(false);
            if (expr is not null)
                statements.Add(expr);
        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Comma));

        return Emit(new ExpressionStatementListNode(statements), start);
    }

    private ArgumentListNode ParseArgumentList()
    {
        var start = GetStartPosition();
        var expressions = new List<ArgumentNode>();

        do
        {
            var argumentStart = GetStartPosition();

            if (Matches(TokenKind.CloseParen))
                return Emit(new ArgumentListNode(expressions), start);

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
            else if (!Matches(TokenKind.ThisKeyword) && _parameterTypes.TryGetValue(PeekCurrent().Lexeme, out var value))
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
                expressions.Add(Emit(new ArgumentNode(expr, parameterType, targetType, name), argumentStart));

        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Comma));

        return Emit(new ArgumentListNode(expressions), start);
    }

    private AstNode ParseForInitializer()
    {
        // The initializer may either be a variable declaration statement
        // Like int i = 0; or a comma-seperated list of expression statements seperated by commas
        // Only the following are allowed: increment/decrement, assignment,
        // method invocation, await expression, object creation (new keyword)
        // Like "for (i = 3, Console.WriteLine("test"), i++; i < 10; i++)"

        // "Sane" path of variable declaration
        if (TryParseDeclarationStatement(out var statement))
        {
            return statement;
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
        var start = GetStartPosition();

        Expect(TokenKind.ForKeyword);
        Expect(TokenKind.OpenParen);
        var initializer = ParseForInitializer();
        //Expect(TokenKind.Semicolon);
        var expression = ParseExpression()!;
        Expect(TokenKind.Semicolon);
        var iterationStatement = ParseForIteration()!;
        Expect(TokenKind.CloseParen);

        var body = ParseBody();

        return Emit(new ForStatementNode(initializer, expression, iterationStatement, body), start);
    }

    private ForEachStatementNode ParseForEachStatement()
    {
        var start = GetStartPosition();

        Expect(TokenKind.ForeachKeyword);
        Expect(TokenKind.OpenParen);
        var type = ParseType();
        bool isTupleDesignation = ConsumeIfMatch(TokenKind.OpenParen);

        AstNode? identifier = null;

        if (isTupleDesignation)
        {
            var tupleDesignationsStart = GetStartPosition();

            if (ParseTupleDesignations(out var temp))
                identifier = Emit(new TupleVariableDesignationsNode(temp), tupleDesignationsStart);

            Expect(TokenKind.CloseParen);
        }
        else
        {
            identifier = ResolveIdentifier();
        }

        Expect(TokenKind.InKeyword);
        var collection = ParseExpression()!;
        Expect(TokenKind.CloseParen);
        var body = ParseBody();

        return Emit(new ForEachStatementNode(type, identifier!, collection, body), start);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        var start = GetStartPosition();

        Expect(TokenKind.WhileKeyword);
        Expect(TokenKind.OpenParen);
        var expr = ParseExpression()!;
        Expect(TokenKind.CloseParen);
        var body = ParseBody();

        return Emit(new WhileStatementNode(expr, body), start);
    }

    private static AstNode ResolveQualifiedNameRecursive(List<Token> members)
    {
        var member = members[^1];
        var identifier = EmitStatic(new IdentifierExpression(member.Lexeme), member);

        if (members.Count == 1)
            return identifier;

        members.Remove(member);

        return EmitStatic(new QualifiedNameNode(
            lhs: ResolveQualifiedNameRecursive(members),
            identifier: identifier
        ), members.First().Start, members.Last().End);
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
        var start = GetStartPosition();
        bool isGlobal = MatchesLexeme("global");

        if (isGlobal)
        {
            Expect(TokenKind.Identifier); // global
        }

        Expect(TokenKind.UsingKeyword);

        var isStatic = ConsumeIfMatch(TokenKind.StaticKeyword);

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
            return Emit(new UsingDirectiveNode(type, alias, isGlobal, isNamespaceGlobal, isStatic), start);
        }

        var ns = ParseQualifiedName();
        Expect(TokenKind.Semicolon);

        return Emit(new UsingDirectiveNode(ns, alias, isGlobal, isNamespaceGlobal, isStatic), start);
    }

    private ReturnStatementNode ParseReturnStatement()
    {
        var start = GetStartPosition();

        Expect(TokenKind.ReturnKeyword);

        if (ConsumeIfMatch(TokenKind.Semicolon))
            return new ReturnStatementNode(null);

        var expression = ParseExpression();

        Expect(TokenKind.Semicolon);

        return Emit(new ReturnStatementNode(expression), start);
    }

    private LocalFunctionDeclarationNode ParseLocalFunction()
    {
        var start = GetStartPosition();

        ParseModifiers(out var accessModifier, out var modifiers);
        Debug.Assert(accessModifier is null);

        var type = ParseType();
        var identifier = ResolveIdentifier(isMaybeGeneric: true, isInNamespaceOrType: true);
        Expect(TokenKind.OpenParen);
        var parms = ParseParameterList();
        Expect(TokenKind.CloseParen);
        var genericConstraints = ParseGenericConstraints();
        var body = ParseMethodBody();

        Debug.Assert(body is not null);

        return Emit(new LocalFunctionDeclarationNode(modifiers, identifier, type, parms, body, genericConstraints), start);
    }

    private ParenthesizedPatternNode ParseParenthesizedPattern()
    {
        var start = GetStartPosition();

        Expect(TokenKind.OpenParen);
        var innerPattern = ParsePattern()!;
        Expect(TokenKind.CloseParen);
        return Emit(new ParenthesizedPatternNode(innerPattern), start);
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
        var start = GetStartPosition();
        var token = Consume();

        if (_relationalOperators.TryGetValue(token.Kind, out var value))
        {
            return Emit(new RelationalPatternNode(value, ParseExpression()!), start);
        }

        throw new Exception("Expected relational pattern");
    }

    private ConstantPatternNode ParseConstantPattern()
    {
        var value = ParseExpression(isParsingPattern: true)!;

        return EmitStatic(new ConstantPatternNode(value), value.Location);
    }

    private readonly List<string> _contextualKeywordsForPatterns = ["and", "or", "not", "when"];

    private bool TryParseDeclarationPattern([NotNullWhen(true)] out DeclarationPatternNode? declarationPattern)
    {
        var startPosition = GetStartPosition();
        var start = Tell();

        declarationPattern = null;

        if (_contextualKeywordsForPatterns.Contains(PeekCurrent().Lexeme))
        {
            Seek(start);
            return false;
        }

        if (!TryParseType(out var type))
        {
            Seek(start);
            return false;
        }

        if (type.IsNullable) // can't be nullable so we probably caught a ternary expression
        {
            Seek(start);
            return false;
        }

        if (!Matches(TokenKind.Identifier) || _contextualKeywordsForPatterns.Contains(PeekCurrent().Lexeme))
        {
            Seek(start);
            return false;
        }

        declarationPattern = Emit(new DeclarationPatternNode(type, Consume().Lexeme), startPosition);
        return true;
    }

    private DiscardPatternNode ParseDiscardPattern()
    {
        var start = GetStartPosition();
        Consume();
        return Emit(new DiscardPatternNode(), start);
    }

    // Parsing patterns is quite similar to parsing expressions
    private PatternNode? ParsePattern(PatternNode? possibleLHS = null)
    {
        var start = GetStartPosition();
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

        if (TryParseDeclarationPattern(out var declarationPattern))
        {
            return declarationPattern;
        }

        if (possibleLHS is null && (isIdentifier || isLiteral || IsMaybeType(PeekCurrent(), true)) &&
            !_contextualKeywordsForPatterns.Contains(PeekCurrent().Lexeme))
        {
            // @note: Roslyn doesn't parse this as a constant pattern so I assume this is 'technically wrong'
            // but for the analyzer this makes things more simple and AFAIK there's little difference between
            // an old switch case/constant pattern anyways, might need refactoring if this proves to be wrong
            // in the future
            possibleLHS = ParseConstantPattern();
            //return ParsePattern(possibleLHS); // we may want to parse relational patterns after constant patterns
        }

        var token = PeekCurrent();

        bool isLogicalNot = token.Lexeme == "not";
        bool isLogicalAnd = token.Lexeme == "and";
        bool isLogicalOr = token.Lexeme == "or";

        if (isLogicalNot)
        {
            Debug.Assert(possibleLHS is null);
            Consume();
            return Emit(new NotPatternNode(ParsePattern()!), start);
        }

        if (isLogicalAnd)
        {
            Debug.Assert(possibleLHS is not null);
            Consume();
            return Emit(new AndPatternNode(possibleLHS!, ParsePattern()!), start);
        }

        if (isLogicalOr)
        {
            Debug.Assert(possibleLHS is not null);
            Consume();
            return Emit(new OrPatternNode(possibleLHS!, ParsePattern()!), start);
        }

        return possibleLHS;
    }

    private SwitchStatementNode ParseSwitchStatement()
    {
        var start = GetStartPosition();

        Expect(TokenKind.SwitchKeyword);

        Expect(TokenKind.OpenParen);

        var expr = ParseExpression()!;

        Expect(TokenKind.CloseParen);

        Expect(TokenKind.OpenBrace);

        var sections = new List<SwitchSectionNode>();

        while (!Matches(TokenKind.CloseBrace))
        {
            var sectionStart = GetStartPosition();
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

            SwitchSectionNode newSection = Emit<SwitchSectionNode>(isCase
                ? new SwitchCaseNode(caseValue!, statements, whenClause)
                : new SwitchDefaultCaseNode(statements), sectionStart);

            sections.Add(newSection);
        }

        Expect(TokenKind.CloseBrace);
        return Emit(new SwitchStatementNode(expr, sections), start);
    }

    private BreakStatementNode ParseBreakStatement()
    {
        var start = GetStartPosition();
        Expect(TokenKind.BreakKeyword);
        Expect(TokenKind.Semicolon);
        return Emit(new BreakStatementNode(), start);
    }

    private ContinueStatementNode ParseContinueStatement()
    {
        var start = GetStartPosition();
        Expect(TokenKind.ContinueKeyword);
        Expect(TokenKind.Semicolon);
        return Emit(new ContinueStatementNode(), start);
    }

    private ThrowExpressionNode ParseThrowExpression()
    {
        var start = GetStartPosition();
        Expect(TokenKind.ThrowKeyword);
        var expression = ParseExpression();
        return Emit(new ThrowExpressionNode(expression), start);
    }

    private TryStatementNode ParseTryStatement()
    {
        var start = GetStartPosition();
        Expect(TokenKind.TryKeyword);
        var block = ParseBlock();

        List<CatchClauseNode> catchClauses = [];
        FinallyClauseNode? finallyClause = null;
        ExpressionNode? whenClause = null;

        while (ConsumeIfMatch(TokenKind.CatchKeyword))
        {
            var catchStart = GetStartPosition();

            TypeNode? type = null;
            string? identifier = null;
            if (ConsumeIfMatch(TokenKind.OpenParen))
            {
                type = ParseType();

                if (!Matches(TokenKind.CloseParen))
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

            catchClauses.Add(Emit(new CatchClauseNode(type, identifier, catchBlock, whenClause), catchStart));
        }

        if (ConsumeIfMatch(TokenKind.FinallyKeyword))
        {
            var finallyBlock = ParseBlock();
            finallyClause = EmitStatic(new FinallyClauseNode(finallyBlock), finallyBlock.Location);
        }

        return Emit(new TryStatementNode(block, catchClauses, finallyClause), start);
    }

    private UsingStatementNode ParseUsingStatement()
    {
        var start = GetStartPosition();

        Expect(TokenKind.UsingKeyword);
        var isDeclaration = !Matches(TokenKind.OpenParen);

        if (!isDeclaration)
            Expect(TokenKind.OpenParen);

        VariableDeclarationStatement? declarator = null;
        ExpressionNode? expression = null;
        AstNode? body = null;

        if (TryParseDeclarationStatement(out var statement, true))
            declarator = (VariableDeclarationStatement)statement;
        else
            expression = ParseExpression();

        if (!isDeclaration)
            Expect(TokenKind.CloseParen);
        else
            Expect(TokenKind.Semicolon);

        if (!isDeclaration)
            body = ParseBody();

        return Emit(new UsingStatementNode(declarator, expression, body), start);
    }

    private EmptyStatementNode ParseEmptyStatement()
    {
        var start = GetStartPosition();
        Expect(TokenKind.Semicolon);
        return Emit(new EmptyStatementNode(), start);
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

        if (!isEmbeddedStatement && TryParseDeclarationStatement(out var statement))
            return statement;

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

        if (Matches(TokenKind.UsingKeyword) && 
            (MatchesLexeme("global", peekOffset: 1) || Matches(TokenKind.StaticKeyword, 1)))
            return true;

        bool isUsingDirective = true;

        isUsingDirective &= Matches(TokenKind.UsingKeyword);
        isUsingDirective &= Matches(TokenKind.Identifier, 1);
        isUsingDirective &= Matches(TokenKind.Equals, 2) || Matches(TokenKind.Dot, 2) || Matches(TokenKind.Semicolon, 2);

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
               kind == TokenKind.StructKeyword ||
               token.Lexeme == "record";
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
        var start = GetStartPosition();
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

            arguments.Add(EmitStatic(new AttributeArgumentNode(expr), expr.Location));

        } while (ConsumeIfMatch(TokenKind.Comma));

        if (maybeAttribute && ConsumeIfMatch(TokenKind.CloseBracket))
        {
            attribute = Emit(new AttributeNode(arguments, target), start);
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
        "protected",
        "readonly"
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
        var start = GetStartPosition();

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
            return Emit(new PropertyAccessorNode(
                PropertyAccessorType.Auto,
                accessModifier ?? AccessModifier.Public,
                null,
                null,
                initOnly
            ), start);

        // Expression bodied member { get => true; }
        if (ConsumeIfMatch(TokenKind.EqualsGreaterThan))
        {
            var expr = ParseExpression();
            Expect(TokenKind.Semicolon);

            return Emit(new PropertyAccessorNode(
                PropertyAccessorType.ExpressionBodied,
                accessModifier ?? AccessModifier.Public,
                expr,
                null,
                initOnly
            ), start);
        }

        // Block bodied member { get { return _field } }
        if (Matches(TokenKind.OpenBrace))
        {
            var block = ParseBlock();

            return Emit(new PropertyAccessorNode(
                PropertyAccessorType.BlockBodied,
                accessModifier ?? AccessModifier.Public,
                null,
                block,
                initOnly
            ), start);
        }

        return null;
    }

    private MemberNode ParseProperty(string propertyName, AccessModifier accessModifier, List<OptionalModifier> modifiers, TypeNode propertyType, List<AttributeNode> attributes, Position start)
    {
        ConsumeIfMatch(TokenKind.OpenBrace);

        bool isOnlyGetter = ConsumeIfMatch(TokenKind.EqualsGreaterThan);

        if (isOnlyGetter)
        {
            var expr = ParseExpression();
            Expect(TokenKind.Semicolon);

            var autoGetter = Emit(new PropertyAccessorNode(
                PropertyAccessorType.ExpressionBodied,
                AccessModifier.Public,
                expr,
                null,
                false
            ), start);

            return Emit(new PropertyMemberNode(accessModifier, modifiers, propertyName, propertyType, autoGetter, null, null, attributes), start);
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

        return Emit(new PropertyMemberNode(accessModifier, modifiers, propertyName, propertyType, getter, setter, value, attributes), start);
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
        var start = GetStartPosition();
        var parameters = new List<ParameterNode>();

        // @todo: support ref, out, params, optional, ...

        do
        {
            var parameterStart = GetStartPosition();
            
            if (Matches(TokenKind.CloseParen))
                return Emit(new ParameterListNode(parameters), start);

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

            parameters.Add(Emit(new ParameterNode(type, identifier.Lexeme, defaultValue, attributes, parameterType), parameterStart));

        } while (!IsAtEnd() && ConsumeIfMatch(TokenKind.Comma));

        return Emit(new ParameterListNode(parameters), start);
    }

    private ConstructorNode ParseConstructor(AccessModifier accessModifier, List<AttributeNode> attributes, Position start)
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

        return Emit(new ConstructorNode(accessModifier, parms, baseArguments, body, type, attributes), start);
    }

    private MethodNode ParseMethod(AccessModifier accessModifier, List<OptionalModifier> modifiers, TypeNode returnType, AstNode methodName, List<AttributeNode> attributes, Position start)
    {
        Expect(TokenKind.OpenParen);
        var parms = ParseParameterList();
        Expect(TokenKind.CloseParen);
        AstNode? body = null;

        var genericConstraints = ParseGenericConstraints();

        if (!ConsumeIfMatch(TokenKind.Semicolon))
            body = ParseMethodBody();

        return Emit(new MethodNode(accessModifier, modifiers, returnType, methodName, parms, body, attributes, genericConstraints), start);
    }

    private EnumMemberNode ParseEnumMember(List<AttributeNode> attributes, Position start)
    {
        var identifier = Consume();
        ExpressionNode? value = null;

        if (ConsumeIfMatch(TokenKind.Equals))
        {
            value = ParseExpression();
        }

        ConsumeIfMatch(TokenKind.Comma);

        return Emit(new EnumMemberNode(identifier.Lexeme, value, attributes), start);
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
        var start = GetStartPosition();

        List<AttributeNode> attributes = [];

        if (Matches(TokenKind.OpenBracket))
            attributes = TryParseAttributes(out _);

        if (kind == TypeKind.Enum)
            return ParseEnumMember(attributes, start);


        var name = ResolveNameFromAstNode(typeName);

        ParseModifiers(out var accessModifier, out var modifiers);
        var isCtor = PeekCurrent().Lexeme == name && PeekSafe().Kind == TokenKind.OpenParen;
        
        var isEvent = ConsumeIfMatch(TokenKind.EventKeyword);

        if (isCtor)
            return ParseConstructor(accessModifier ?? AccessModifier.Private, attributes, start);

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
            return Emit(new FieldMemberNode(accessModifier ?? AccessModifier.Private, modifiers, ResolveNameFromAstNode(identifier), type, value, attributes, isEvent), start);
        }
        else if (isProperty)
        {
            return ParseProperty(ResolveNameFromAstNode(identifier), accessModifier ?? AccessModifier.Private, modifiers, type, attributes, start);
        }
        else if (isMethod)
        {
            return ParseMethod(accessModifier ?? AccessModifier.Private, modifiers, type, identifier, attributes, start);
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
        var start = GetStartPosition();

        List<AttributeNode> attributes = [];

        if (Matches(TokenKind.OpenBracket))
            attributes = TryParseAttributes(out _);
            
        ParseModifiers(out var accessModifier, out var modifiers);

        var token = Consume();
        var type = token.Kind;

        var identifier = ResolveIdentifier(isMaybeGeneric: true, isInNamespaceOrType: true);
        List<AstNode> parentNames = [];

        ParameterListNode? parameters = null;
        ArgumentListNode? baseArguments = null;

        if (ConsumeIfMatch(TokenKind.OpenParen))
        {
            parameters = ParseParameterList();
            Expect(TokenKind.CloseParen);
        }

        if (ConsumeIfMatch(TokenKind.Colon))
        {
            do
            {
                parentNames.Add(ResolveIdentifier(isMaybeGeneric: true, isInNamespaceOrType: true));

                if (ConsumeIfMatch(TokenKind.OpenParen))
                {
                    baseArguments = ParseArgumentList();
                    Expect(TokenKind.CloseParen);
                }
            } while (ConsumeIfMatch(TokenKind.Comma));
        }

        var genericConstraints = ParseGenericConstraints();

        var kind = type switch
        {
            TokenKind.ClassKeyword => TypeKind.Class,
            TokenKind.StructKeyword => TypeKind.Struct,
            TokenKind.InterfaceKeyword => TypeKind.Interface,
            TokenKind.EnumKeyword => TypeKind.Enum,
            TokenKind.Identifier when token.Lexeme == "record" => TypeKind.Record,
            _ => throw new Exception()
        };

        List<MemberNode> members = [];

        if (ConsumeIfMatch(TokenKind.OpenBrace))
        {
            members = ParseMembers(kind, identifier);

            Expect(TokenKind.CloseBrace);
        }
        else if (kind != TypeKind.Record)
        {
            Expect(TokenKind.OpenBrace);
        }

        return Emit<TypeDeclarationNode>(kind switch
        {
            TypeKind.Class => new ClassDeclarationNode(identifier, members, parentNames, accessModifier, modifiers, attributes, parameters, baseArguments, genericConstraints),
            TypeKind.Enum => new EnumDeclarationNode(identifier, members.Cast<EnumMemberNode>().ToList(), parentNames, accessModifier, modifiers, attributes),
            TypeKind.Interface => new InterfaceDeclarationNode(identifier, members, parentNames, accessModifier, modifiers, attributes, genericConstraints),
            TypeKind.Struct => new StructDeclarationNode(identifier, members, parentNames, accessModifier, modifiers, attributes, parameters, baseArguments, genericConstraints),
            TypeKind.Record => new RecordDeclarationNode(identifier, members, parentNames, accessModifier, modifiers, attributes, parameters, baseArguments, genericConstraints),
            _ => throw new NotImplementedException(),
        }, start);
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

    private NamespaceNode ParseNamespace(bool isGlobal = false)
    {
        var start = GetStartPosition();
        Expect(TokenKind.NamespaceKeyword);
        var name = ParseQualifiedName(); // @fixme: qualified name or member access?
        var isFileScoped = ConsumeIfMatch(TokenKind.Semicolon);

        if (!isFileScoped)
            Expect(TokenKind.OpenBrace);

        var ns = ParseNamespaceContent(QualifiedNameToString(name), isFileScoped, isGlobal, start);

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

    private NamespaceNode ParseNamespaceContent(string name, bool isFileScoped, bool isGlobal, Position start)
    {
        var ns = (isGlobal || isFileScoped) ? new GlobalNamespaceNode(name) : new NamespaceNode(name, isFileScoped);
        bool allowTopLevelStatements = isGlobal || isFileScoped;
        var directives = ParseUsingDirectives();

        // try parse assembly/module

        var attributes = ParseTopLevelAttributes();

        // the 'rule' in C# is that top-level statements must precede any type declarations and namespaces
        // this method stops the moment it encounters a type declaration
        if (ns is GlobalNamespaceNode globalNamespace && allowTopLevelStatements)
        {
            var statements = ParseTopLevelStatements()
                .Select(s => EmitStatic(new GlobalStatementNode(s), s.Location)).ToList();

            globalNamespace.GlobalStatements.AddRange(statements!);
        }

        var declarationsAndNamespaces = ParseTypeDeclarationsAndNamespaces();

        var typeDeclarations = declarationsAndNamespaces.OfType<TypeDeclarationNode>().ToList();
        var namespaces = declarationsAndNamespaces.OfType<NamespaceNode>().ToList();

        ns.UsingDirectives.AddRange(directives);
        ns.Attributes.AddRange(attributes);
        ns.TypeDeclarations.AddRange(typeDeclarations);
        ns.Namespaces.AddRange(namespaces);

        return Emit(ns, start);
    }

    private void AssignParentRecursive(AstNode node)
    {
        foreach (var child in node.Children)
        {
            if (child is null) // This shouldn't happen, but it does so catch it
                continue;

            child.Parent = node;
            AssignParentRecursive(child);
        }
    }

    private AST ParseInternal(Token[] tokens)
    {
        var ast = new AST { Root = EmitStatic<GlobalNamespaceNode>(new(), new CodeLocation()) };

        if (tokens.Length == 0)
            return ast;

        _input = tokens;

        ast.Root = (GlobalNamespaceNode)ParseNamespaceContent("global", true, true, GetStartPosition());

        AssignParentRecursive(ast.Root);

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