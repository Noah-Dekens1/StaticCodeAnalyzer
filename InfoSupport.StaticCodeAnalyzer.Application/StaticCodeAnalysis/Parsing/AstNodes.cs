using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

public abstract class AstNode
{
    public List<Token> Tokens { get; } = [];

    public abstract List<AstNode> Children { get; }
    public CodeLocation Location { get; set; }

#if DEBUG
    public bool ConstructedInEmit { get; set; }
#endif

    public static T Construct<T>(T node)
        where T : AstNode
    {
#if DEBUG
        node.ConstructedInEmit = true;
#endif
        return node;
    }
}

public class GlobalNamespaceNode(
    string? name = null,
    List<GlobalStatementNode>? globalStatements = null,
    List<UsingDirectiveNode>? usingDirectives = null,
    List<TypeDeclarationNode>? typeDeclarations = null,
    List<NamespaceNode>? namespaces = null,
    List<AttributeNode>? attributes = null
    ) : NamespaceNode(name ?? "global", isFileScoped: true, usingDirectives, typeDeclarations, namespaces, attributes)
{
    public List<GlobalStatementNode> GlobalStatements { get; } = globalStatements ?? [];

    public override List<AstNode> Children => [.. UsingDirectives, .. GlobalStatements, .. TypeDeclarations, .. Namespaces, ..Attributes];
}

[DebuggerDisplay("namespace {Name,nq}")]
public class NamespaceNode(
    string name,
    bool isFileScoped = false,
    List<UsingDirectiveNode>? usingDirectives = null,
    List<TypeDeclarationNode>? typeDeclarations = null,
    List<NamespaceNode>? namespaces = null,
    List<AttributeNode>? attributes = null
) : AstNode
{
    public string Name { get; } = name;
    public bool IsFileScoped { get; } = isFileScoped;
    public List<UsingDirectiveNode> UsingDirectives { get; } = usingDirectives ?? [];
    public List<AttributeNode> Attributes { get; } = attributes ?? [];
    public List<TypeDeclarationNode> TypeDeclarations { get; } = typeDeclarations ?? [];
    public List<NamespaceNode> Namespaces { get; } = namespaces ?? [];

    public override List<AstNode> Children => [.. UsingDirectives, .. TypeDeclarations, .. Namespaces, ..Attributes];
}

public class StatementNode : AstNode
{
    public override List<AstNode> Children => [];
}

[DebuggerDisplay("return {ToString(),nq}")]
public class ReturnStatementNode(ExpressionNode? returnExpression) : StatementNode
{
    public ExpressionNode? ReturnExpression { get; } = returnExpression;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(ReturnExpression);

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{ReturnExpression}";
}

[DebuggerDisplay("{Statement,nq}")]
public class GlobalStatementNode(StatementNode statement) : AstNode
{
    public StatementNode Statement { get; } = statement;

    public override List<AstNode> Children => [Statement];
}

[DebuggerDisplay("{Expression,nq}")]
public class ExpressionStatementNode(ExpressionNode expression) : StatementNode
{
    public ExpressionNode Expression { get; } = expression;
    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Expression.ToString()!;
}

public class ExpressionNode : AstNode
{
    public override List<AstNode> Children => [];
}

public class LiteralExpressionNode : ExpressionNode
{

}

[DebuggerDisplay("{ToString(),nq}")]
public class NumericLiteralNode(object? value) : LiteralExpressionNode
{
    public object? Value { get; } = value;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{Value}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class DefaultLiteralNode : LiteralExpressionNode
{
    [ExcludeFromCodeCoverage]
    public override string ToString() => "default";
}

[DebuggerDisplay("{ToString()}")]
public class BooleanLiteralNode(bool value) : LiteralExpressionNode
{
    public bool Value { get; } = value;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{(Value ? "true" : "false")}";
}

[DebuggerDisplay("{ToString()}")]
public class CharLiteralNode(char value) : LiteralExpressionNode
{
    public char Value { get; } = value;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"'{Value}'";
}

[DebuggerDisplay("{ToString(),nq}")]
public class StringLiteralNode(string value) : LiteralExpressionNode
{
    public string Value { get; } = value;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"\"{Value}\"";
}

[DebuggerDisplay("{ToString(),nq}")]
public class StringInterpolationNode(ExpressionNode expression) : AstNode
{
    public ExpressionNode Expression { get; } = expression;

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class InterpolatedStringLiteralNode(string value, List<StringInterpolationNode> interpolations) : LiteralExpressionNode
{
    public string Value { get; } = value;
    public List<StringInterpolationNode> Interpolations { get; } = interpolations;

    public override List<AstNode> Children => [.. Interpolations];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"\"{Value}\"";
}

[DebuggerDisplay("{ToString(),nq}")]
public class NullLiteralNode : LiteralExpressionNode
{
    [ExcludeFromCodeCoverage]
    public override string ToString() => "null";
}

[DebuggerDisplay("{ToString()}")]
public class ParenthesizedExpressionNode(ExpressionNode expr) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expr;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"({Expression})";

    public override List<AstNode> Children => [Expression];
}



public enum UnaryOperator
{
    Negation,
    LogicalNot,
    Increment,
    Decrement,
    BitwiseComplement,
}

[DebuggerDisplay("{ToString()}")]
public class UnaryExpressionNode : ExpressionNode
{
    public ExpressionNode Expression { get; }
    public bool IsPrefix { get; } = true;

    [ExcludeFromCodeCoverage]
    public virtual UnaryOperator Operator { get; }

    public UnaryExpressionNode(ExpressionNode expr, bool isPrefix = true)
    {
        Expression = expr;
        IsPrefix = isPrefix;
    }

    [ExcludeFromCodeCoverage]
    private string OperatorForDbg => Operator switch
    {
        UnaryOperator.Negation => "-",
        UnaryOperator.LogicalNot => "!",
        UnaryOperator.Increment => "++",
        UnaryOperator.Decrement => "--",
        UnaryOperator.BitwiseComplement => "~",
        _ => throw new NotImplementedException()
    };

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{(IsPrefix ? OperatorForDbg : "")}{Expression}{(!IsPrefix ? OperatorForDbg : "")}";
}

public class CastExpressionNode(TypeNode type, ExpressionNode expr) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expr;
    public TypeNode Type { get; } = type;

    public override List<AstNode> Children => [Type, Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"({Type}){Expression}";
}

public class UnaryNegationNode(ExpressionNode expr, bool isPrefix = true) : UnaryExpressionNode(expr, isPrefix)
{
    public override UnaryOperator Operator => UnaryOperator.Negation;
}

public class UnaryIncrementNode(ExpressionNode expr, bool isPrefix = true) : UnaryExpressionNode(expr, isPrefix)
{
    public override UnaryOperator Operator => UnaryOperator.Increment;
}

public class UnaryDecrementNode(ExpressionNode expr, bool isPrefix = true) : UnaryExpressionNode(expr, isPrefix)
{
    public override UnaryOperator Operator => UnaryOperator.Decrement;
}

public class UnaryBitwiseComplementNode(ExpressionNode expr, bool isPrefix = true) : UnaryExpressionNode(expr, isPrefix)
{
    public override UnaryOperator Operator => UnaryOperator.BitwiseComplement;
}

public class UnaryLogicalNotNode(ExpressionNode expr, bool isPrefix = true) : UnaryExpressionNode(expr, isPrefix)
{
    public override UnaryOperator Operator => UnaryOperator.LogicalNot;
}

public enum BinaryOperator
{
    // Arithmetic
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulus,

    // Boolean
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    ConditionalAnd,
    ConditionalOr,

    // Compound
    AddAssign,
    SubtractAssign,
    MultiplyAssign,
    DivideAssign,
    ModulusAssign,
    AndAssign,
    OrAssign,
    XorAssign,

    Assignment,
    NullCoalescing,
    NullCoalescingAssignment,

    // Bitwise
    LeftShift,
    RightShift,
    LeftShiftAssign,
    RightShiftAssign,
    LogicalXor,
    LogicalAnd,
    LogicalOr,

    // ...
}

[DebuggerDisplay("{ToString()}")]
public class BinaryExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : ExpressionNode
{
    public ExpressionNode LHS { get; } = lhs;
    public ExpressionNode RHS { get; } = rhs;

    public virtual BinaryOperator Operator { get; }
    public override List<AstNode> Children => [LHS, RHS];

    [ExcludeFromCodeCoverage]
    private string OperatorForDbg => Operator switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulus => "%",

        BinaryOperator.Equals => "==",
        BinaryOperator.NotEquals => "!=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterThanOrEqual => ">=",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessThanOrEqual => "<=",
        BinaryOperator.ConditionalAnd => "&&",
        BinaryOperator.ConditionalOr => "||",

        BinaryOperator.AddAssign => "+=",
        BinaryOperator.SubtractAssign => "-=",
        BinaryOperator.MultiplyAssign => "*=",
        BinaryOperator.DivideAssign => "/=",
        BinaryOperator.ModulusAssign => "%=",
        BinaryOperator.AndAssign => "&=",
        BinaryOperator.OrAssign => "|=",
        BinaryOperator.XorAssign => "^=",

        BinaryOperator.Assignment => "=",
        BinaryOperator.NullCoalescing => "??",
        BinaryOperator.NullCoalescingAssignment => "??=",

        BinaryOperator.LeftShift => "<<",
        BinaryOperator.LeftShiftAssign => "<<=",
        BinaryOperator.RightShift => ">>",
        BinaryOperator.RightShiftAssign => ">>=",
        BinaryOperator.LogicalAnd => "&",
        BinaryOperator.LogicalOr => "|",
        BinaryOperator.LogicalXor => "^",

        _ => throw new NotImplementedException()
    };

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS} {OperatorForDbg} {RHS}";
}

public class AddExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.Add; }
}

public class MultiplyExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.Multiply; }
}

public class SubtractExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.Subtract; }
}

public class DivideExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.Divide; }
}

public class ModulusExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.Modulus; }
}

public class EqualsExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.Equals; }
}

public class NotEqualsExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.NotEquals; }
}

public class GreaterThanExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.GreaterThan; }
}

public class ConditionalAndExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.ConditionalAnd; }
}


public class GreaterThanEqualsExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.GreaterThanOrEqual; }
}

public class LessThanExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.LessThan; }
}

public class LessThanEqualsExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.LessThanOrEqual; }
}

public class ConditionalOrExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.ConditionalOr; }
}

public class AssignmentExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.Assignment;
}

public class AddAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.AddAssign;
}

public class MultiplyAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.MultiplyAssign;
}

public class SubtractAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.SubtractAssign;
}

public class DivideAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.DivideAssign;
}

public class ModulusAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.ModulusAssign;
}

public class AndAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.AndAssign;
}

public class OrAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.OrAssign;
}

public class NullCoalescingExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.NullCoalescing;
}

public class NullCoalescingAssignmentExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.NullCoalescingAssignment;
}

public class LeftShiftExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.LeftShift;
}

public class LeftShiftAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.LeftShiftAssign;
}

public class RightShiftExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.RightShift;
}

public class RightShiftAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.RightShiftAssign;
}

public class LogicalAndExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.LogicalAnd;
}

public class LogicalOrExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.LogicalOr;
}

public class LogicalXorExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.LogicalXor;
}

public class LogicalXorAssignExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator => BinaryOperator.XorAssign;
}

[DebuggerDisplay("{ToString(),nq}")]
public class TypeArgumentsNode(List<TypeNode> typeArguments) : AstNode
{
    public List<TypeNode> TypeArguments { get; } = typeArguments;

    public override List<AstNode> Children => [.. TypeArguments];

    [ExcludeFromCodeCoverage]
    public override string ToString() => string.Join(", ", TypeArguments);
}

public struct ArrayTypeData
{
    public bool IsArray { get; set; }
    public ExpressionNode? ArrayRank { get; set; }
    public bool RankOmitted { get; set; } = true;
    public bool IsInnerTypeNullable { get; set; } = false;

    public ArrayTypeData()
    {
        IsArray = false;
        ArrayRank = null;
        RankOmitted = true;
        IsInnerTypeNullable = false;
    }

    public ArrayTypeData(bool isArray, bool innerTypeNullable=false)
    {
        IsArray = isArray;
        IsInnerTypeNullable = innerTypeNullable;
    }

    public ArrayTypeData(ExpressionNode? rank)
    {
        IsArray = true;
        RankOmitted = rank is null;
        ArrayRank = rank;
    }
}

[DebuggerDisplay("{ToString(),nq}")]
public class TypeNode(AstNode baseType, TypeArgumentsNode? typeArguments = null, ArrayTypeData? arrayType = null, bool isNullable = false) : AstNode
{
    public AstNode BaseType { get; } = baseType;
    public TypeArgumentsNode? TypeArgumentsNode { get; } = typeArguments;
    public ArrayTypeData ArrayType { get; } = arrayType ?? new();
    public bool IsNullable { get; } = isNullable;

    public override List<AstNode> Children => Utils.ParamsToList(BaseType, TypeArgumentsNode, ArrayType.ArrayRank);

    [ExcludeFromCodeCoverage]
    public override string ToString() => TypeArgumentsNode is not null
        ? $"{BaseType}<{TypeArgumentsNode}>{(IsNullable ? "?" : "")}"
        : $"{BaseType}{(IsNullable ? "?" : "")}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TupleTypeNode(List<TupleTypeElementNode> elements) : AstNode
{
    public List<TupleTypeElementNode> Elements { get; } = elements;

    public override List<AstNode> Children => [.. Elements];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"({string.Join(", ", Elements)})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class GenericNameNode(AstNode identifier, TypeArgumentsNode? typeArguments = null) : ExpressionNode
{
    public AstNode Identifier { get; } = identifier;
    public TypeArgumentsNode? TypeArgumentsNode { get; } = typeArguments;

    public override List<AstNode> Children => Utils.ParamsToList(Identifier, TypeArgumentsNode);

    [ExcludeFromCodeCoverage]
    public override string ToString() => TypeArgumentsNode is not null
        ? $"{Identifier}<{TypeArgumentsNode}>"
        : $"{Identifier}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class VariableDeclaratorNode(string identifier, ExpressionNode? value = null) : AstNode
{
    public string Identifier { get; } = identifier;
    public ExpressionNode? Value { get; } = value;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Value);

    [ExcludeFromCodeCoverage]
    public override string ToString() => Value is not null
        ? $"{Identifier} = {Value}"
        : $"{Identifier}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class VariableDeclarationStatement(TypeNode type, List<VariableDeclaratorNode> declarators, bool isConst=false) : StatementNode
{
    public bool IsConst { get; } = isConst;
    public TypeNode Type { get; } = type;

    public List<VariableDeclaratorNode> Declarators { get; } = declarators;
    public override List<AstNode> Children => [Type, ..Declarators];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{(IsConst ? "const " : "")}{Type} {string.Join(", ", Declarators)}";
}


[DebuggerDisplay(";")]
public class EmptyStatementNode : StatementNode
{

}

public class BlockNode(List<StatementNode> statements) : StatementNode
{
    public List<StatementNode> Statements { get; } = statements;
    public override List<AstNode> Children => [.. Statements];
}

// @FIXME is an identifier an expression and not just a part of an expression?
[DebuggerDisplay("{Identifier,nq}")]
public class IdentifierExpression(string identifier) : ExpressionNode
{
    public string Identifier { get; } = identifier;

    [ExcludeFromCodeCoverage]
    public override string ToString() => Identifier;
}

[DebuggerDisplay("if ({Expression,nq})")]
public class IfStatementNode(ExpressionNode expression, AstNode body, AstNode? elseBody) : StatementNode
{
    public ExpressionNode Expression { get; } = expression;
    public AstNode Body { get; } = body;
    public AstNode? ElseBody { get; } = elseBody;

    public override List<AstNode> Children =>
        ElseBody is not null ? [Expression, Body, ElseBody] : [Expression, Body];
}

[DebuggerDisplay("do ... while ({Condition,nq})")]
public class DoStatementNode(ExpressionNode condition, AstNode body) : StatementNode
{
    public ExpressionNode Condition { get; } = condition;
    public AstNode Body { get; } = body;

    public override List<AstNode> Children => [Condition, Body];
}

[DebuggerDisplay("for ({Initializer,nq};{Condition,nq};{IterationExpression,nq}) ...")]
public class ForStatementNode(
    AstNode initializer, ExpressionNode? condition, AstNode iteration, AstNode body
    ) : StatementNode
{
    public AstNode Initializer { get; } = initializer;
    public ExpressionNode? Condition { get; } = condition;
    public AstNode IterationExpression { get; } = iteration;
    public AstNode Body { get; } = body;

    public override List<AstNode> Children => Utils.ParamsToList(Initializer, Condition, IterationExpression, Body);
}

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class ExpressionStatementListNode(List<ExpressionStatementNode> statements) : AstNode
{
    public List<ExpressionStatementNode> Statements { get; } = statements;

    public override List<AstNode> Children => [.. Statements];

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay
    {
        get => string.Join(',', Children);
    }
}

[DebuggerDisplay("foreach ({VariableType,nq} {VariableIdentifier,nq} in {Collection,nq}) ...")]
public class ForEachStatementNode(TypeNode variableType, AstNode variableIdentifier, ExpressionNode collection, AstNode body) : StatementNode
{
    public TypeNode VariableType { get; } = variableType;
    public AstNode VariableIdentifier { get; } = variableIdentifier;
    public ExpressionNode Collection { get; } = collection;
    public AstNode Body { get; } = body;

    public override List<AstNode> Children => [VariableType, VariableIdentifier, Collection, Body];
}

[DebuggerDisplay("while ({Condition,nq}) ...")]
public class WhileStatementNode(ExpressionNode condition, AstNode body) : StatementNode
{
    public ExpressionNode Condition { get; } = condition;
    public AstNode Body { get; } = body;

    public override List<AstNode> Children => [Condition, Body];
}

[DebuggerDisplay("{ToString(),nq}")]
public class MemberAccessExpressionNode(ExpressionNode lhs, ExpressionNode identifier) : ExpressionNode
{
    public ExpressionNode LHS { get; } = lhs;
    public ExpressionNode Identifier { get; } = identifier;

    public override List<AstNode> Children => [LHS, Identifier];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}.{Identifier}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ConditionalMemberAccessExpressionNode(ExpressionNode lhs, ExpressionNode identifier)
    : MemberAccessExpressionNode(lhs, identifier)
{
    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}?.{Identifier}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class NullForgivingExpressionNode(ExpressionNode expression) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expression;

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{Expression}!";
}

[DebuggerDisplay("{ToString()}")]
public class ArgumentNode(ExpressionNode expression, ParameterType parameterType=ParameterType.Regular, TypeNode? targetType=null, string? name=null) : AstNode
{
    public ExpressionNode Expression { get; } = expression;
    public string? Name { get; } = name;
    public ParameterType ParameterType { get; } = parameterType;
    /** Type of out parameter */
    public TypeNode? TargetType { get; } = targetType;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Expression, TargetType);

    [ExcludeFromCodeCoverage]
    public override string ToString() => Name is not null
        ? $"{Name}: {Expression}"
        : $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ArgumentListNode(List<ArgumentNode> arguments) : AstNode
{
    public List<ArgumentNode> Arguments { get; } = arguments;

    public override List<AstNode> Children => [.. Arguments];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Arguments.Count >= 10
        ? $"{Arguments.Count} arguments"
        : string.Join(',', Arguments.Select(a => a.ToString()));
}

[DebuggerDisplay("{ToString(),nq}")]
public class BracketedArgumentList(List<ArgumentNode> arguments) : ArgumentListNode(arguments)
{

}

public enum ParameterType
{
    Regular,
    Ref,
    RefReadonly,
    In,
    Out,
    This,
    Params
}

[DebuggerDisplay("{ToString(),nq}")]
public class ParameterNode(TypeNode type, string identifier, ExpressionNode? defaultValue = null, List<AttributeNode>? attributes = null, ParameterType parameterType = ParameterType.Regular) : AstNode
{
    public TypeNode Type { get; } = type;
    public string Identifier { get; } = identifier;
    public List<AttributeNode> Attributes { get; } = attributes ?? [];
    public ParameterType ParameterType { get; } = parameterType;
    public ExpressionNode? DefaultValue { get; } = defaultValue;

    public override List<AstNode> Children => [..Attributes, ..Utils.ParamsToList<AstNode>(Type, DefaultValue)];

    [ExcludeFromCodeCoverage]
    public override string ToString()
        => $"{Type} {Identifier}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ParameterListNode(List<ParameterNode> parameters) : AstNode
{
    public List<ParameterNode> Parameters { get; } = parameters;

    public override List<AstNode> Children => [.. Parameters];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Parameters.Count >= 10
        ? $"{Parameters.Count} parameters"
        : string.Join(',', Parameters.Select(a => a.ToString()));

}

[DebuggerDisplay("{ToString(),nq}")]
public class InvocationExpressionNode(ExpressionNode lhs, ArgumentListNode arguments) : ExpressionNode
{
    public ExpressionNode LHS { get; } = lhs;
    public ArgumentListNode Arguments { get; } = arguments;

    public override List<AstNode> Children => [LHS, Arguments];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}({Arguments})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ElementAccessExpressionNode(ExpressionNode lhs, BracketedArgumentList arguments) : ExpressionNode
{
    public ExpressionNode LHS { get; } = lhs;
    public ArgumentListNode Arguments { get; } = arguments;

    public override List<AstNode> Children => [LHS, Arguments];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}[{Arguments}]";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ConditionalElementAccessExpressionNode(ExpressionNode lhs, BracketedArgumentList arguments)
    : ElementAccessExpressionNode(lhs, arguments)
{
    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}?[{Arguments}]";
}

[DebuggerDisplay("{ToString(),nq}")]
public class IndexExpressionNode(ExpressionNode expression, bool fromEnd=false) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expression;
    public bool FromEnd { get; } = fromEnd;

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{(FromEnd ? "^" : "")}{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class RangeExpressionNode(IndexExpressionNode? lhs, IndexExpressionNode? rhs) : ExpressionNode
{
    public IndexExpressionNode? LHS { get; } = lhs;
    public IndexExpressionNode? RHS { get; } = rhs;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(LHS, RHS);

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}..{RHS}";
}

public abstract class CollectionInitializerElementNode : AstNode
{
}

public class RegularCollectionInitializerNode(ExpressionNode value) : CollectionInitializerElementNode
{
    public ExpressionNode Values { get; } = value;
    public override List<AstNode> Children => [Values];
}

public class IndexedCollectionInitializerNode(ExpressionNode key, ExpressionNode value) : CollectionInitializerElementNode
{
    public ExpressionNode Key { get; } = key;
    public ExpressionNode Value { get; } = value;

    public override List<AstNode> Children => [Key, Value];
}

public class ComplexCollectionInitializerNode(List<ExpressionNode> values) : CollectionInitializerElementNode
{
    public List<ExpressionNode> Values { get; } = values;
    public override List<AstNode> Children => [.. Values];
}

public class CollectionInitializerNode(List<CollectionInitializerElementNode> values) : CollectionInitializerElementNode
{
    public List<CollectionInitializerElementNode> Values { get; } = values;

    public override List<AstNode> Children => [.. Values];
}

[DebuggerDisplay("{ToString(),nq}")]
public class ObjectCreationExpressionNode(TypeNode? type, bool isArrayCreation=false, ArgumentListNode? arguments = null, CollectionInitializerNode? initializer = null) : ExpressionNode
{
    public TypeNode? Type { get; } = type;
    public ArgumentListNode Arguments { get; } = arguments ?? Construct(new ArgumentListNode([]));
    public CollectionInitializerNode? CollectionInitializer = initializer;
    public bool IsArrayCreation { get; } = isArrayCreation;
    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Type, Arguments, CollectionInitializer);

    [ExcludeFromCodeCoverage]
    public override string ToString() => CollectionInitializer is null
        ? $"new {Type}({Arguments})"
        : $"new {Type}({Arguments}) {CollectionInitializer}";
}

[DebuggerDisplay("{LHS,nq}.{Identifier,nq}")]
public class QualifiedNameNode(AstNode lhs, IdentifierExpression identifier) : AstNode
{
    public AstNode LHS { get; } = lhs;
    public IdentifierExpression Identifier { get; } = identifier;

    public override List<AstNode> Children => [LHS, Identifier];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}.{Identifier}";

}

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class UsingDirectiveNode(AstNode ns, string? alias, bool isGlobal=false, bool isNamespaceGlobal=false) : AstNode
{
    public string? Alias { get; } = alias;
    public AstNode NamespaceOrType { get; } = ns;
    public bool IsGlobal { get; } = isGlobal;
    public bool IsNamespaceGlobal { get; } = isNamespaceGlobal;

    public override List<AstNode> Children => [NamespaceOrType];

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay
    {
        get => Alias is not null ? $"using {Alias} = {NamespaceOrType}" : $"using {NamespaceOrType}";
    }
}

public abstract class TypeDeclarationNode(List<AttributeNode>? attributes = null) : AstNode
{
    public List<AttributeNode> Attributes { get; } = attributes ?? [];
}

public enum AccessModifier
{
    Private,
    Protected,
    Internal,
    Public,
    ProtectedInternal,
    PrivateProtected
}

public enum OptionalModifier
{
    Static,
    Virtual,
    Override,
    Abstract,
    Sealed,
    Extern,
    Partial,
    New,
    Readonly,
    Const,
    Volatile,
    Async,
    Required
}

public abstract class MemberNode(List<AttributeNode>? attributes = null) : AstNode
{
    public List<AttributeNode> Attributes { get; } = attributes ?? [];
}

[DebuggerDisplay("{ToString(),nq}")]
public class FieldMemberNode(AccessModifier accessModifier, List<OptionalModifier> modifiers, string fieldName, TypeNode fieldType, ExpressionNode? value, List<AttributeNode>? attributes = null) : MemberNode(attributes)
{
    public AccessModifier AccessModifier = accessModifier;
    public List<OptionalModifier> Modifiers = modifiers;
    public string FieldName { get; } = fieldName;
    public TypeNode FieldType { get; } = fieldType;
    public ExpressionNode? Value { get; } = value;

    public override List<AstNode> Children => [..Attributes, ..Utils.ParamsToList<AstNode>(FieldType, Value)];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Value is not null
        ? $"{AccessModifier} {string.Join(' ', Modifiers)} {FieldType} {FieldName} = {Value}"
        : $"{AccessModifier} {string.Join(' ', Modifiers)} {FieldType} {FieldName}";
}

public enum PropertyAccessorType
{
    Auto,
    BlockBodied,
    ExpressionBodied
}

public static class Utils
{
    public static List<T> ParamsToList<T>(params T?[] values)
    {
        var list = new List<T>();

        foreach (var item in values)
            if (item is not null)
                list.Add(item);

        return list;
    }
}

public class PropertyAccessorNode(
    PropertyAccessorType accessorType,
    AccessModifier accessModifier,
    ExpressionNode? expressionBody,
    BlockNode? blockBody,
    bool initOnly = false
    ) : AstNode
{
    public PropertyAccessorType AccessorType { get; } = accessorType;
    public AccessModifier AccessModifier { get; } = accessModifier;
    public ExpressionNode? ExpressionBody { get; } = expressionBody;
    public BlockNode? BlockBody { get; } = blockBody;
    public bool IsInitOnly { get; } = initOnly;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(ExpressionBody, BlockBody);
}


public class PropertyMemberNode(
    AccessModifier accessModifier, List<OptionalModifier> modifiers, string propertyName, TypeNode propertyType, 
    PropertyAccessorNode? getter, PropertyAccessorNode? setter, ExpressionNode? value, List<AttributeNode>? attributes = null) : MemberNode(attributes)
{
    public AccessModifier AccessModifier { get; } = accessModifier;
    public List<OptionalModifier> Modifiers { get; } = modifiers;
    public string PropertyName { get; } = propertyName;
    public TypeNode PropertyType { get; } = propertyType;
    public PropertyAccessorNode? Getter { get; } = getter;
    public PropertyAccessorNode? Setter { get; } = setter;
    public ExpressionNode? Value { get; } = value;

    public override List<AstNode> Children => [..Attributes, ..Utils.ParamsToList<AstNode>(PropertyType, Getter, Setter, Value)];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{PropertyType} {PropertyName}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class EnumMemberNode(string identifier, ExpressionNode? value, List<AttributeNode>? attributes = null) : MemberNode(attributes)
{
    public string Identifier { get; } = identifier;
    public ExpressionNode? Value { get; } = value;

    public override List<AstNode> Children => [..Attributes, ..Utils.ParamsToList<AstNode>(Value)];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Value is not null
        ? $"{Identifier} = {Value}"
        : $"{Identifier}";
}

public enum ConstructorArgumentsType
{
    None,
    Base,
    This
}

[DebuggerDisplay("{AccessModifier,nq} Constructor({Parameters,nq})")]
public class ConstructorNode(AccessModifier accessModifier, ParameterListNode parameters, ArgumentListNode? baseArguments, AstNode body, ConstructorArgumentsType constructorArgumentsType = ConstructorArgumentsType.None, List<AttributeNode>? attributes = null) : MemberNode(attributes)
{
    public AccessModifier AccessModifier { get; } = accessModifier;
    public ParameterListNode Parameters { get; } = parameters;
    public ConstructorArgumentsType ConstructorArgumentsType { get; } = constructorArgumentsType;
    public ArgumentListNode? BaseArguments { get; } = baseArguments;
    public AstNode Body { get; } = body;

    public override List<AstNode> Children => [..Attributes, ..Utils.ParamsToList(Parameters, BaseArguments, Body)];
}

public enum GenericConstraintType
{
    Struct,
    Class,
    NullableClass,
    NotNull,
    Unmanaged,
    New,
    Default,
    Type // any type-based constraints and their nullable variants
}

[DebuggerDisplay("{ToString(),nq}")]
public class GenericConstraintNode(GenericConstraintType constraintType, TypeNode? baseType = null) : AstNode
{
    public GenericConstraintType ConstraintType { get; } = constraintType;
    public TypeNode? BaseType { get; } = baseType;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(BaseType);

    [ExcludeFromCodeCoverage]
    public override string ToString() => BaseType is null
        ? $"{ConstraintType}"
        : $"{ConstraintType} {BaseType}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class WhereConstraintNode(TypeNode target, List<GenericConstraintNode> constraints) : AstNode
{
    public TypeNode Target { get; } = target;
    public List<GenericConstraintNode> Constraints { get; } = constraints;

    public override List<AstNode> Children => [Target, .. Constraints];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"where {Target} : {string.Join(", ", Constraints)}";
}

[DebuggerDisplay("{AccessModifier,nq} {ReturnType,nq} {MethodName,nq}({Parameters,nq})")]
public class MethodNode(
    AccessModifier accessModifier,
    List<OptionalModifier> modifiers,
    TypeNode returnType,
    AstNode methodName,
    ParameterListNode parameters,
    AstNode? body, 
    List<AttributeNode>? attributes = null,
    List<WhereConstraintNode>? genericConstraints = null
    ) : MemberNode(attributes)
{
    public AccessModifier AccessModifier { get; } = accessModifier;
    public List<OptionalModifier> Modifiers { get; } = modifiers;
    public TypeNode ReturnType { get; } = returnType;
    public AstNode MethodName { get; } = methodName;
    public ParameterListNode Parameters { get; } = parameters;
    public AstNode? Body { get; } = body;
    public List<WhereConstraintNode> GenericConstraints = genericConstraints ?? [];

    public override List<AstNode> Children => [..Attributes, ..GenericConstraints, ..Utils.ParamsToList(ReturnType, MethodName, Parameters, Body)];
}

public class BasicDeclarationNode(
    AstNode name, List<MemberNode> members, AstNode? parentName = null, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null, 
    List<AttributeNode>? attributes = null, ParameterListNode? parameters = null, 
    ArgumentListNode? baseArguments = null, List<WhereConstraintNode>? genericConstraints = null) : TypeDeclarationNode(attributes)
{
    public AccessModifier AccessModifier { get; } = accessModifier ?? AccessModifier.Internal;
    public List<OptionalModifier> Modifiers { get; } = modifiers ?? [];
    public AstNode? ParentName { get; } = parentName;
    public AstNode Name { get; } = name;
    public List<MemberNode> Members { get; } = members;

    // Primary Constructor parameters
    public ParameterListNode Parameters { get; } = parameters ?? Construct<ParameterListNode>(new([]));
    public ArgumentListNode BaseArguments { get; } = baseArguments ?? Construct<ArgumentListNode>(new([]));

    public List<WhereConstraintNode> GenericConstraints { get; } = genericConstraints ?? [];

    public override List<AstNode> Children => [.. Members, Name, Parameters, BaseArguments, ..GenericConstraints, ..Utils.ParamsToList<AstNode>(ParentName)];
}

[DebuggerDisplay("class {Name,nq}")]
public class ClassDeclarationNode(
    AstNode className, List<MemberNode> members, AstNode? parentName = null, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null, 
    List<AttributeNode>? attributes = null, ParameterListNode? parameters = null, 
    ArgumentListNode? baseArguments = null, List<WhereConstraintNode>? genericConstraints = null
    ) : BasicDeclarationNode(className, members, parentName, accessModifier, modifiers, attributes, parameters, baseArguments, genericConstraints)
{
}

[DebuggerDisplay("interface {Name,nq}")]
public class InterfaceDeclarationNode(
    AstNode name, List<MemberNode> members, AstNode? parentName = null, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null, 
    List<AttributeNode>? attributes = null, List<WhereConstraintNode>? genericConstraints = null
    ) : BasicDeclarationNode(name, members, parentName, accessModifier, modifiers, attributes, genericConstraints: genericConstraints)
{

}

[DebuggerDisplay("struct {Name,nq}")]
public class StructDeclarationNode(
    AstNode name, List<MemberNode> members, AstNode? parentName = null, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null,
    List<AttributeNode>? attributes = null, ParameterListNode? parameters = null, 
    ArgumentListNode? baseArguments = null, List<WhereConstraintNode>? genericConstraints = null
    ) : BasicDeclarationNode(name, members, parentName, accessModifier, modifiers, attributes, parameters, baseArguments, genericConstraints)
{

}

[DebuggerDisplay("record {Name,nq}")]
public class RecordDeclarationNode(
    AstNode name, List<MemberNode> members, AstNode? parentName = null,
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null,
    List<AttributeNode>? attributes = null, ParameterListNode? parameters = null,
    ArgumentListNode? baseArguments = null, List<WhereConstraintNode>? genericConstraints = null
    ) : BasicDeclarationNode(name, members, parentName, accessModifier, modifiers, attributes, parameters, baseArguments, genericConstraints)
{

}

[DebuggerDisplay("enum {EnumName,nq}")]
public class EnumDeclarationNode(
    AstNode enumName, List<EnumMemberNode> members, AstNode? parentType, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null, 
    List<AttributeNode>? attributes = null) : TypeDeclarationNode(attributes)
{
    public AccessModifier AccessModifier { get; } = accessModifier ?? AccessModifier.Internal;
    public List<OptionalModifier> Modifiers { get; } = modifiers ?? [];
    public AstNode? ParentType { get; } = parentType;
    public AstNode EnumName { get; } = enumName;
    public List<EnumMemberNode> Members { get; } = members;

    public override List<AstNode> Children => [.. Members, ..Utils.ParamsToList<AstNode>(ParentType, EnumName), ..Attributes];
}

public enum TypeKind
{
    Class,
    Struct,
    Enum,
    Interface,
    Record
}

// @fixme: Maybe it's not entirely correct to make it a statement
[DebuggerDisplay("{ToString(),nq}")]
public class LocalFunctionDeclarationNode(
    List<OptionalModifier> modifiers, AstNode name, TypeNode returnType, ParameterListNode parameters, 
    AstNode body, List<WhereConstraintNode>? genericConstraints = null
    ) : StatementNode
{
    public List<WhereConstraintNode> GenericConstraints { get; } = genericConstraints ?? [];
    public List<OptionalModifier> Modifiers { get; } = modifiers;
    public AstNode Name { get; } = name;
    public TypeNode ReturnType { get; } = returnType;
    public ParameterListNode Parameters { get; } = parameters;
    public AstNode Body { get; } = body;

    public override List<AstNode> Children => [Name, ReturnType, Parameters, Body, ..GenericConstraints];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{ReturnType} {Name}({Parameters})";
}

public abstract class ElementNode : AstNode
{
}

[DebuggerDisplay("{ToString(),nq}")]
public class SpreadElementNode(ExpressionNode expression) : ElementNode
{
    public ExpressionNode Expression { get; } = expression;

    public override List<AstNode> Children => [Expression];

    public override string ToString() => $".. {Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ExpressionElementNode(ExpressionNode expression) : ElementNode
{
    public ExpressionNode Expression { get; } = expression;

    public override List<AstNode> Children => [Expression];

    public override string ToString() => $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class CollectionExpressionNode(List<ElementNode> elements) : ExpressionNode
{
    public List<ElementNode> Elements { get; } = elements;

    public override List<AstNode> Children => [.. Elements];

    public override string ToString() => $"[{string.Join(", ", Elements)}]";
}

[DebuggerDisplay("{ToString(),nq}")]
public class LambdaParameterNode(string identifier, TypeNode? type = null) : AstNode
{
    public TypeNode? Type { get; } = type;
    public string Identifier { get; } = identifier;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Type);

    [ExcludeFromCodeCoverage]
    public override string ToString()
        => $"{Type} {Identifier}";
}

public class LambdaExpressionNode(List<LambdaParameterNode> parameters, AstNode body, bool isAsync = false) : ExpressionNode
{
    public List<LambdaParameterNode> Parameters { get; } = parameters;
    public AstNode Body { get; } = body;
    public bool IsAsync { get; } = isAsync;

    public override List<AstNode> Children => [..Parameters, Body];
}

public abstract class PatternNode : AstNode
{
}

[DebuggerDisplay("{ToString(),nq}")]
public class IsExpressionNode(ExpressionNode expression, PatternNode pattern) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expression;
    public PatternNode Pattern { get; } = pattern;

    public override List<AstNode> Children => [Expression, Pattern];
    public override string ToString() => $"{Expression} is {Pattern}";
}

public class DeclarationPatternNode(TypeNode type, string identifier) : PatternNode
{
    public TypeNode Type { get; } = type;
    public string Identifier { get; } = identifier;

    public override List<AstNode> Children => [Type];
    public override string ToString() => $"{Type} {Identifier}";
}

public enum RelationalPatternOperator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
}

[DebuggerDisplay("{ToString(),nq}")]
public class RelationalPatternNode(RelationalPatternOperator op, ExpressionNode value) : PatternNode
{
    public RelationalPatternOperator Operator { get; } = op;
    public ExpressionNode Value { get; } = value;

    public override List<AstNode> Children => [Value];

    [ExcludeFromCodeCoverage]
    private string OperatorForDbg => Operator switch
    {
        RelationalPatternOperator.GreaterThan => ">",
        RelationalPatternOperator.GreaterThanOrEqual => ">=",
        RelationalPatternOperator.LessThan => "<",
        RelationalPatternOperator.LessThanOrEqual => "<=",
        _ => throw new NotImplementedException()
    };

    public override string ToString() => $"{OperatorForDbg} {Value}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ConstantPatternNode(ExpressionNode value) : PatternNode
{
    public ExpressionNode Value { get; } = value;
    public override List<AstNode> Children => [Value];

    public override string ToString() => $"{Value}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class DiscardPatternNode : PatternNode
{
    public override List<AstNode> Children => [];

    public override string ToString() => $"_";
}

public abstract class LogicalPatternNode : PatternNode
{

}


public class AndPatternNode(PatternNode lhs, PatternNode rhs) : LogicalPatternNode
{
    public PatternNode LHS { get; } = lhs;
    public PatternNode RHS { get; } = rhs;

    public override List<AstNode> Children => [LHS, RHS];
    public override string ToString() => $"{LHS} and {RHS}";
}

public class OrPatternNode(PatternNode lhs, PatternNode rhs) : LogicalPatternNode
{
    public PatternNode LHS { get; } = lhs;
    public PatternNode RHS { get; } = rhs;

    public override List<AstNode> Children => [LHS, RHS];
    public override string ToString() => $"{LHS} or {RHS}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class NotPatternNode(PatternNode pattern) : LogicalPatternNode
{
    public PatternNode Pattern { get; } = pattern;

    public override List<AstNode> Children => [Pattern];

    public override string ToString() => $"not {Pattern}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ParenthesizedPatternNode(PatternNode pattern) : PatternNode
{
    public PatternNode InnerPattern { get; } = pattern;

    public override List<AstNode> Children => [InnerPattern];

    public override string ToString() => $"({InnerPattern})";
}

public abstract class SwitchSectionNode : AstNode
{
}

[DebuggerDisplay("{ToString(),nq}")]
public class SwitchCaseNode(PatternNode casePattern, List<StatementNode> statements, ExpressionNode? whenClause = null) : SwitchSectionNode
{
    PatternNode CasePattern { get; } = casePattern;
    public List<StatementNode> Statements { get; } = statements;
    public ExpressionNode? WhenClause = whenClause;

    public override List<AstNode> Children => [.. Utils.ParamsToList<AstNode>(CasePattern, WhenClause), .. Statements];

    public override string ToString() => $"case {CasePattern}:";
}

[DebuggerDisplay("default:")]
public class SwitchDefaultCaseNode(List<StatementNode> statements) : SwitchSectionNode
{
    public List<StatementNode> Statements { get; } = statements;

    public override List<AstNode> Children => [.. Statements];
}

[DebuggerDisplay("{ToString(),nq}")]
public class SwitchStatementNode(ExpressionNode switchExpression, List<SwitchSectionNode> sections) : StatementNode
{
    public ExpressionNode SwitchExpression { get; } = switchExpression;
    public List<SwitchSectionNode> SwitchSectionNodes { get; } = sections;

    public override List<AstNode> Children => [SwitchExpression, .. SwitchSectionNodes];
    public override string ToString() => $"switch {SwitchExpression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class SwitchExpressionArmNode(PatternNode condition, ExpressionNode value, ExpressionNode? whenClause = null) : AstNode
{
    public PatternNode Condition { get; } = condition;
    public ExpressionNode Value { get; } = value;
    public ExpressionNode? WhenClause { get; } = whenClause;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Condition, Value, WhenClause);

    public override string ToString() => WhenClause is not null
        ? $"{Condition} => {Value} when {WhenClause}"
        : $"{Condition} => {Value}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class SwitchExpressionNode(ExpressionNode switchExpression, List<SwitchExpressionArmNode> arms) : ExpressionNode
{
    public ExpressionNode SwitchExpression { get; } = switchExpression;
    public List<SwitchExpressionArmNode> SwitchArmNodes { get; } = arms;

    public override List<AstNode> Children => [SwitchExpression, .. SwitchArmNodes];
    public override string ToString() => $"{SwitchExpression} switch";
}

[DebuggerDisplay("break;")]
public class BreakStatementNode : StatementNode
{

}

[DebuggerDisplay("continue;")]
public class ContinueStatementNode : StatementNode
{

}

[DebuggerDisplay("{ToString(),nq}")]
public class TernaryExpressionNode(ExpressionNode condition, ExpressionNode trueExpr, ExpressionNode falseExpr) : ExpressionNode
{
    public ExpressionNode Condition { get; } = condition;
    public ExpressionNode TrueExpr { get; } = trueExpr;
    public ExpressionNode FalseExpr { get; } = falseExpr;

    public override List<AstNode> Children => [Condition, TrueExpr, FalseExpr];

    [ExcludeFromCodeCoverage]
    public override string ToString()
        => $"{Condition} ? {TrueExpr} : {FalseExpr}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TypeofExpressionNode(TypeNode type) : ExpressionNode
{
    public TypeNode Type { get; } = type;

    public override List<AstNode> Children => [Type];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"typeof({Type})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class NameofExpressionNode(AstNode value) : ExpressionNode
{
    public AstNode Value { get; } = value;
    public override List<AstNode> Children => [Value];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"nameof({Value})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class SizeofExpressionNode(TypeNode type) : ExpressionNode
{
    public TypeNode Type { get; } = type;
    public override List<AstNode> Children => [Type];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"sizeof({Type})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class DefaultOperatorExpressionNode(TypeNode type) : ExpressionNode
{
    public TypeNode Type { get; } = type;
    public override List<AstNode> Children => [Type];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"default({Type})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class AwaitExpressionNode(ExpressionNode expression) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expression;
    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"await {Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class AttributeArgumentNode(ExpressionNode expression) : AstNode
{
    public ExpressionNode Expression { get; } = expression;

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class AttributeNode(List<AttributeArgumentNode> arguments, string? target=null) : AstNode
{
    public List<AttributeArgumentNode> Arguments { get; } = arguments;
    public string? Target { get; } = target;

    public override List<AstNode> Children => [.. Arguments];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"[{string.Join(", ", Arguments)}]";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ThrowExpressionNode(ExpressionNode? expression) : ExpressionNode
{
    public ExpressionNode? Expression { get; } = expression;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Expression);

    public override string ToString() => $"throw {Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class CatchClauseNode(TypeNode? exceptionType, string? identifier, BlockNode block, ExpressionNode? whenClause=null) : AstNode
{
    public TypeNode? ExceptionType { get; } = exceptionType;
    public string? Identifier { get; } = identifier;
    public BlockNode Block { get; } = block;
    public ExpressionNode? WhenClause { get; } = whenClause;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(ExceptionType, Block, WhenClause);
    public override string ToString() => $"catch ({ExceptionType} {Identifier})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class FinallyClauseNode(BlockNode block) : AstNode
{
    public BlockNode Block { get; } = block;

    public override List<AstNode> Children => [Block];
    public override string ToString() => "finally";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TryStatementNode(BlockNode block, List<CatchClauseNode>? catchClauses=null, FinallyClauseNode? finallyClause=null) : StatementNode
{
    public BlockNode Block { get; } = block;
    public List<CatchClauseNode> CatchClauses { get; } = catchClauses ?? [];
    public FinallyClauseNode? FinallyClause { get; } = finallyClause;

    public override List<AstNode> Children => [..CatchClauses, ..Utils.ParamsToList<AstNode>(Block, FinallyClause)];

    public override string ToString() => $"try";
}

[DebuggerDisplay("{ToString(),nq}")]
public class AsExpressionNode(ExpressionNode lhs, TypeNode targetType) : ExpressionNode
{
    public ExpressionNode LHS { get; } = lhs;
    public TypeNode TargetType { get; } = targetType;

    public override List<AstNode> Children => [LHS, TargetType];
    public override string ToString() => $"{LHS} {TargetType}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class GlobalNamespaceQualifierNode(ExpressionNode ns) : ExpressionNode
{
    public ExpressionNode Namespace { get; } = ns;

    public override List<AstNode> Children => [Namespace];
    public override string ToString() => $"global::{Namespace}";
}

// using (variable-declaration|expression) { code }
[DebuggerDisplay("{ToString(),nq}")]
public class UsingStatementNode(
    VariableDeclarationStatement? declaration = null, 
    ExpressionNode? expression = null,
    AstNode? body = null) : StatementNode // could have a body, could also be block-scoped
{
    public VariableDeclarationStatement? DeclarationStatement { get; } = declaration;
    public ExpressionNode? Expression {  get; } = expression;
    public AstNode? Body { get; } = body;
    public bool IsDeclaration { get; } = body is null;

    public override List<AstNode> Children => Utils.ParamsToList(DeclarationStatement, Expression, Body);

    public override string ToString() => $"using {(DeclarationStatement is not null ? DeclarationStatement : Expression)};";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TupleArgumentNode(ExpressionNode expression, string? name = null) : AstNode
{
    public ExpressionNode Expression { get; } = expression;
    public string? Name { get; } = name;

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Name is not null
        ? $"{Name}: {Expression}"
        : $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TupleExpressionNode(List<TupleArgumentNode> arguments) : ExpressionNode
{
    public List<TupleArgumentNode> Arguments { get; } = arguments;


    public override List<AstNode> Children => [.. Arguments];
    public override string ToString() => $"({string.Join(", ", Children)})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TupleElementNode(string name, TypeNode? type=null) : AstNode
{
    public string Name { get; } = name;
    public TypeNode? Type { get; } = type;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Type);

    public override string ToString() => Type is not null
        ? $"{Type} {Name}"
        : $"{Name}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TupleTypeElementNode(TypeNode type, string? name = null) : AstNode
{
    public string? Name { get; } = name;
    public TypeNode Type { get; } = type;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Type);

    public override string ToString() => Type is not null
        ? $"{Type} {Name}"
        : $"{Name}";
}

// Not sure if this should be merged into variable declaration
// Seems cleaner to leave them apart
[DebuggerDisplay("{ToString(),nq}")]
public class TupleDeconstructStatementNode(
    List<TupleElementNode> designations,
    ExpressionNode rhs,
    TypeNode? specifiedType = null
    ) : StatementNode
{
    public TypeNode? SpecifiedType { get; } = specifiedType;
    public List<TupleElementNode> Designations { get; } = designations;
    public ExpressionNode RHS { get; } = rhs;

    public override List<AstNode> Children => [.. Designations, .. Utils.ParamsToList<AstNode>(SpecifiedType, RHS)];

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        var result = SpecifiedType is not null ? $"{SpecifiedType} " : "";
        result += "(" + string.Join(", ", Designations) + ")";
        return result;
    }
}

[DebuggerDisplay("{ToString(),nq}")]
public class ThisExpressionNode : ExpressionNode
{
    public override List<AstNode> Children => [];
    public override string ToString() => "this";
}

[DebuggerDisplay("{ToString(),nq}")]
public class BaseExpressionNode : ExpressionNode
{
    public override List<AstNode> Children => [];

    [ExcludeFromCodeCoverage]
    public override string ToString() => "base";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TupleVariableDesignationsNode(List<TupleElementNode> designations) : AstNode
{
    public List<TupleElementNode> Designations { get; } = designations;

    public override List<AstNode> Children => [.. Designations];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"({string.Join(", ", Designations)})";
}