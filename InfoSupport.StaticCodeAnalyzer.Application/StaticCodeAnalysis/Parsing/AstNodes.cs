using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

public abstract class AstNode
{
    public List<Token> Tokens { get; set; } = [];

    public abstract List<AstNode> Children { get; }
}

public class GlobalNamespaceNode() : NamespaceNode("global", true)
{
    public List<GlobalStatementNode> GlobalStatements { get; set; } = [];

    public override List<AstNode> Children => [.. UsingDirectives, .. GlobalStatements, .. TypeDeclarations, .. Namespaces];
}

[DebuggerDisplay("namespace {Name,nq}")]
public class NamespaceNode(
    string name,
    bool isFileScoped = false,
    List<UsingDirectiveNode>? usingDirectives = null,
    List<TypeDeclarationNode>? typeDeclarations = null,
    List<NamespaceNode>? namespaces = null
) : AstNode
{
    public string Name { get; } = name;
    public bool IsFileScoped { get; } = isFileScoped;
    public List<UsingDirectiveNode> UsingDirectives { get; } = usingDirectives ?? [];
    public List<TypeDeclarationNode> TypeDeclarations { get; } = typeDeclarations ?? [];
    public List<NamespaceNode> Namespaces { get; } = namespaces ?? [];

    public override List<AstNode> Children => [.. UsingDirectives, .. TypeDeclarations, .. Namespaces];
}

public class StatementNode : AstNode
{
    public override List<AstNode> Children => [];
}

[DebuggerDisplay("return {ToString(),nq}")]
public class ReturnStatementNode(ExpressionNode? returnExpression) : StatementNode
{
    public ExpressionNode? ReturnExpression { get; set; } = returnExpression;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(ReturnExpression);

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{ReturnExpression}";
}

[DebuggerDisplay("{Statement,nq}")]
public class GlobalStatementNode(StatementNode statement) : AstNode
{
    public StatementNode Statement { get; set; } = statement;

    public override List<AstNode> Children => [Statement];
}

[DebuggerDisplay("{Expression,nq}")]
public class ExpressionStatementNode(ExpressionNode expression) : StatementNode
{
    public ExpressionNode Expression { get; set; } = expression;
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
    public object? Value { get; set; } = value;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{Value}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class DefaultLiteralNode : LiteralExpressionNode
{
    public override string ToString() => "default";
}

[DebuggerDisplay("{ToString()}")]
public class BooleanLiteralNode(bool value) : LiteralExpressionNode
{
    public bool Value { get; set; } = value;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{(Value ? "true" : "false")}";
}

[DebuggerDisplay("{ToString()}")]
public class CharLiteralNode(char value) : LiteralExpressionNode
{
    public char Value { get; set; } = value;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"'{Value}'";
}

[DebuggerDisplay("{ToString(),nq}")]
public class StringLiteralNode(string value) : LiteralExpressionNode
{
    public string Value { get; set; } = value;

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"\"{Value}\"";
}

[DebuggerDisplay("{ToString(),nq}")]
public class StringInterpolationNode(ExpressionNode expression) : AstNode
{
    public ExpressionNode Expression { get; set; } = expression;

    public override List<AstNode> Children => [Expression];
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
    public ExpressionNode Expression { get; set; } = expr;

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
    public ExpressionNode Expression { get; set; }
    public bool IsPrefix { get; set; } = true;

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
    public ExpressionNode Expression { get; set; } = expr;
    public TypeNode Type { get; set; } = type;

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
    LogicalAnd,
    LogicalOr,

    // Compound
    AddAssign,
    SubtractAssign,
    MultiplyAssign,
    DivideAssign,
    ModulusAssign,
    AndAssign,
    OrAssign,

    Assignment,
    NullCoalescing,
    NullCoalescingAssignment,

    // Bitwise
    LeftShift,
    RightShift,
    LeftShiftAssign,
    RightShiftAssign,

    // ...
}

[DebuggerDisplay("{ToString()}")]
public class BinaryExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public ExpressionNode RHS { get; set; } = rhs;

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
        BinaryOperator.LogicalAnd => "&&",
        BinaryOperator.LogicalOr => "||",

        BinaryOperator.AddAssign => "+=",
        BinaryOperator.SubtractAssign => "-=",
        BinaryOperator.MultiplyAssign => "*=",
        BinaryOperator.DivideAssign => "/=",
        BinaryOperator.ModulusAssign => "%=",
        BinaryOperator.AndAssign => "&=",
        BinaryOperator.OrAssign => "|=",

        BinaryOperator.Assignment => "=",
        BinaryOperator.NullCoalescing => "??",
        BinaryOperator.NullCoalescingAssignment => "??=",

        BinaryOperator.LeftShift => "<<",
        BinaryOperator.LeftShiftAssign => "<<=",
        BinaryOperator.RightShift => ">>",
        BinaryOperator.RightShiftAssign => ">>=",

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

public class LogicalAndExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.LogicalAnd; }
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

public class LogicalOrExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : BinaryExpressionNode(lhs, rhs)
{
    public override BinaryOperator Operator { get => BinaryOperator.LogicalOr; }
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

[DebuggerDisplay("{ToString(),nq}")]
public class TypeArgumentsNode(List<TypeNode> typeArguments) : AstNode
{
    public List<TypeNode> TypeArguments { get; set; } = typeArguments;

    public override List<AstNode> Children => [.. TypeArguments];

    [ExcludeFromCodeCoverage]
    public override string ToString() => string.Join(", ", TypeArguments);
}

public struct ArrayTypeData
{
    public bool IsArray { get; set; }
    public int ArrayRank { get; set; }
    public bool RankOmitted { get; set; }

    public ArrayTypeData()
    {
        IsArray = false;
        ArrayRank = 0;
        RankOmitted = true;
    }

    public ArrayTypeData(bool isArray)
    {
        IsArray = isArray;
    }

    public ArrayTypeData(int? rank)
    {
        IsArray = true;
        RankOmitted = !rank.HasValue;
        ArrayRank = RankOmitted ? 0 : rank!.Value;
    }
}

[DebuggerDisplay("{ToString(),nq}")]
public class TypeNode(AstNode baseType, TypeArgumentsNode? typeArguments = null, ArrayTypeData? arrayType = null, bool isNullable = false) : AstNode
{
    public AstNode BaseType { get; set; } = baseType;
    public TypeArgumentsNode? TypeArgumentsNode { get; set; } = typeArguments;
    public ArrayTypeData ArrayType { get; set; } = arrayType ?? new();
    public bool IsNullable { get; set; } = isNullable;

    public override List<AstNode> Children => Utils.ParamsToList(BaseType, TypeArgumentsNode);

    [ExcludeFromCodeCoverage]
    public override string ToString() => TypeArgumentsNode is not null
        ? $"{BaseType}<{TypeArgumentsNode}>{(IsNullable ? "?" : "")}"
        : $"{BaseType}{(IsNullable ? "?" : "")}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class GenericNameNode(AstNode identifier, TypeArgumentsNode? typeArguments = null) : ExpressionNode
{
    public AstNode Identifier { get; set; } = identifier;
    public TypeArgumentsNode? TypeArgumentsNode { get; set; } = typeArguments;

    public override List<AstNode> Children => Utils.ParamsToList(Identifier, TypeArgumentsNode);

    [ExcludeFromCodeCoverage]
    public override string ToString() => TypeArgumentsNode is not null
        ? $"{Identifier}<{TypeArgumentsNode}>"
        : $"{Identifier}";
}


[DebuggerDisplay("{Type,nq} {Identifier,nq} = {Expression,nq}")]
public class VariableDeclarationStatement(TypeNode type, string identifier, ExpressionNode expression) : StatementNode
{
    public TypeNode Type { get; set; } = type;
    public string Identifier { get; set; } = identifier;
    public ExpressionNode Expression { get; set; } = expression;
    public override List<AstNode> Children => [Type, Expression];
}


[DebuggerDisplay(";")]
public class EmptyStatementNode : StatementNode
{

}

public class BlockNode(List<StatementNode> statements) : StatementNode
{
    public List<StatementNode> Statements { get; set; } = statements;
    public override List<AstNode> Children => [.. Statements];
}

// @FIXME is an identifier an expression and not just a part of an expression?
[DebuggerDisplay("{Identifier,nq}")]
public class IdentifierExpression(string identifier, bool isNullForgiving = false) : ExpressionNode
{
    public string Identifier { get; set; } = identifier;
    public bool IsNullForgiving { get; set; } = isNullForgiving;

    [ExcludeFromCodeCoverage]
    public override string ToString() => Identifier;
}

[DebuggerDisplay("if ({Expression,nq})")]
public class IfStatementNode(ExpressionNode expression, AstNode body, AstNode? elseBody) : StatementNode
{
    public ExpressionNode Expression { get; set; } = expression;
    public AstNode Body { get; set; } = body;
    public AstNode? ElseBody { get; set; } = elseBody;

    public override List<AstNode> Children =>
        ElseBody is not null ? [Expression, Body, ElseBody] : [Expression, Body];
}

[DebuggerDisplay("do ... while ({Condition,nq})")]
public class DoStatementNode(ExpressionNode condition, AstNode body) : StatementNode
{
    public ExpressionNode Condition { get; set; } = condition;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => [Condition, Body];
}

[DebuggerDisplay("for ({Initializer,nq};{Condition,nq};{IterationExpression,nq}) ...")]
public class ForStatementNode(
    AstNode initializer, ExpressionNode? condition, AstNode iteration, AstNode body
    ) : StatementNode
{
    public AstNode Initializer { get; set; } = initializer;
    public ExpressionNode? Condition { get; set; } = condition;
    public AstNode IterationExpression { get; set; } = iteration;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => Utils.ParamsToList(Initializer, Condition, IterationExpression, Body);
}

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class ExpressionStatementListNode(List<ExpressionStatementNode> statements) : AstNode
{
    public List<ExpressionStatementNode> Statements { get; set; } = statements;

    public override List<AstNode> Children => [.. Statements];

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay
    {
        get => string.Join(',', Children);
    }
}

[DebuggerDisplay("foreach ({VariableType,nq} {VariableIdentifier,nq} in {Collection,nq}) ...")]
public class ForEachStatementNode(TypeNode variableType, string variableIdentifier, ExpressionNode collection, AstNode body) : StatementNode
{
    public TypeNode VariableType { get; set; } = variableType;
    public string VariableIdentifier { get; set; } = variableIdentifier;
    public ExpressionNode Collection { get; set; } = collection;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => [Collection, Body];
}

[DebuggerDisplay("while ({Condition,nq}) ...")]
public class WhileStatementNode(ExpressionNode condition, AstNode body) : StatementNode
{
    public ExpressionNode Condition { get; set; } = condition;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => [Condition, Body];
}

[DebuggerDisplay("{ToString(),nq}")]
public class MemberAccessExpressionNode(ExpressionNode lhs, ExpressionNode identifier) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public ExpressionNode Identifier { get; set; } = identifier;

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

[DebuggerDisplay("{ToString()}")]
public class ArgumentNode(ExpressionNode expression, string? name) : AstNode
{
    public ExpressionNode Expression { get; set; } = expression;
    public string? Name { get; set; } = name;

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Name is not null
        ? $"{Name}: {Expression}"
        : $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ArgumentListNode(List<ArgumentNode> arguments) : AstNode
{
    public List<ArgumentNode> Arguments { get; set; } = arguments;

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

[DebuggerDisplay("{ToString(),nq}")]
public class ParameterNode(TypeNode type, string identifier, List<AttributeNode>? attributes = null) : AstNode
{
    public TypeNode Type { get; set; } = type;
    public string Identifier { get; set; } = identifier;
    public List<AttributeNode> Attributes { get; set; } = attributes ?? [];

    public override List<AstNode> Children => [Type];

    [ExcludeFromCodeCoverage]
    public override string ToString()
        => $"{Type} {Identifier}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ParameterListNode(List<ParameterNode> parameters) : AstNode
{
    public List<ParameterNode> Parameters { get; set; } = parameters;

    public override List<AstNode> Children => [.. Parameters];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Parameters.Count >= 10
        ? $"{Parameters.Count} parameters"
        : string.Join(',', Parameters.Select(a => a.ToString()));

}

[DebuggerDisplay("{ToString(),nq}")]
public class InvocationExpressionNode(ExpressionNode lhs, ArgumentListNode arguments) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public ArgumentListNode Arguments { get; set; } = arguments;

    public override List<AstNode> Children => [LHS, Arguments];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}({Arguments})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ElementAccessExpressionNode(ExpressionNode lhs, BracketedArgumentList arguments) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public ArgumentListNode Arguments { get; set; } = arguments;

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
    public ExpressionNode Expression { get; set; } = expression;
    public bool FromEnd { get; set; } = fromEnd;

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{(FromEnd ? "^" : "")}{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class RangeExpressionNode(IndexExpressionNode? lhs, IndexExpressionNode? rhs) : ExpressionNode
{
    public IndexExpressionNode? LHS { get; set; } = lhs;
    public IndexExpressionNode? RHS { get; set; } = rhs;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(LHS, RHS);

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}..{RHS}";
}

public abstract class CollectionInitializerElementNode : AstNode
{
}

public class RegularCollectionInitializerNode(ExpressionNode value) : CollectionInitializerElementNode
{
    public ExpressionNode Values { get; set; } = value;
    public override List<AstNode> Children => [Values];
}

public class IndexedCollectionInitializerNode(ExpressionNode key, ExpressionNode value) : CollectionInitializerElementNode
{
    public ExpressionNode Key { get; set; } = key;
    public ExpressionNode Value { get; set; } = value;

    public override List<AstNode> Children => [Key, Value];
}

public class ComplexCollectionInitializerNode(List<ExpressionNode> values) : CollectionInitializerElementNode
{
    public List<ExpressionNode> Values { get; set; } = values;
    public override List<AstNode> Children => [.. Values];
}

public class CollectionInitializerNode(List<CollectionInitializerElementNode> values) : CollectionInitializerElementNode
{
    public List<CollectionInitializerElementNode> Values { get; set; } = values;

    public override List<AstNode> Children => [.. Values];
}

[DebuggerDisplay("{ToString(),nq}")]
public class ObjectCreationExpressionNode(TypeNode? type, ArgumentListNode? arguments = null, CollectionInitializerNode? initializer = null) : ExpressionNode
{
    public TypeNode? Type { get; set; } = type;
    public ArgumentListNode Arguments { get; set; } = arguments ?? new ArgumentListNode([]);
    public CollectionInitializerNode? CollectionInitializer = initializer;
    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Type, Arguments, CollectionInitializer);

    [ExcludeFromCodeCoverage]
    public override string ToString() => CollectionInitializer is null
        ? $"new {Type}({Arguments})"
        : $"new {Type}({Arguments}) {CollectionInitializer}";
}

[DebuggerDisplay("{LHS,nq}.{Identifier,nq}")]
public class QualifiedNameNode(AstNode lhs, IdentifierExpression identifier) : AstNode
{
    public AstNode LHS { get; set; } = lhs;
    public IdentifierExpression Identifier { get; set; } = identifier;

    public override List<AstNode> Children => [LHS, Identifier];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{LHS}.{Identifier}";

}

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class UsingDirectiveNode(AstNode ns, string? alias) : AstNode
{
    public string? Alias { get; set; } = alias;
    public AstNode Namespace { get; set; } = ns;

    public override List<AstNode> Children => [Namespace];

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay
    {
        get => Alias is not null ? $"using {Alias} = {Namespace}" : $"using {Namespace}";
    }
}

public abstract class TypeDeclarationNode(List<AttributeNode>? attributes = null) : AstNode
{
    public List<AttributeNode> Attributes { get; set; } = attributes ?? [];
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
    public List<AttributeNode> Attributes { get; set; } = attributes ?? [];
}

[DebuggerDisplay("{ToString(),nq}")]
public class FieldMemberNode(AccessModifier accessModifier, List<OptionalModifier> modifiers, string fieldName, TypeNode fieldType, ExpressionNode? value, List<AttributeNode>? attributes = null) : MemberNode(attributes)
{
    public AccessModifier AccessModifier = accessModifier;
    public List<OptionalModifier> Modifiers = modifiers;
    public string FieldName { get; set; } = fieldName;
    public TypeNode FieldType { get; set; } = fieldType;
    public ExpressionNode? Value { get; set; } = value;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(FieldType, Value);

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
    public PropertyAccessorType AccessorType { get; set; } = accessorType;
    public AccessModifier AccessModifier { get; set; } = accessModifier;
    public ExpressionNode? ExpressionBody { get; set; } = expressionBody;
    public BlockNode? BlockBody { get; set; } = blockBody;
    public bool IsInitOnly { get; set; } = initOnly;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(ExpressionBody, BlockBody);
}


public class PropertyMemberNode(
    AccessModifier accessModifier, List<OptionalModifier> modifiers, string propertyName, TypeNode propertyType, 
    PropertyAccessorNode? getter, PropertyAccessorNode? setter, ExpressionNode? value, List<AttributeNode>? attributes = null) : MemberNode(attributes)
{
    public AccessModifier AccessModifier { get; set; } = accessModifier;
    public List<OptionalModifier> Modifiers { get; set; } = modifiers;
    public string PropertyName { get; set; } = propertyName;
    public TypeNode PropertyType { get; set; } = propertyType;
    public PropertyAccessorNode? Getter { get; set; } = getter;
    public PropertyAccessorNode? Setter { get; set; } = setter;
    public ExpressionNode? Value { get; set; } = value;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(PropertyType, Getter, Setter, Value);

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{PropertyType} {PropertyName}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class EnumMemberNode(string identifier, ExpressionNode? value, List<AttributeNode>? attributes = null) : MemberNode(attributes)
{
    public string Identifier { get; set; } = identifier;
    public ExpressionNode? Value { get; set; } = value;

    public override List<AstNode> Children => Value is not null ? [Value] : [];

    [ExcludeFromCodeCoverage]
    public override string ToString() => Value is not null
        ? $"{Identifier} = {Value}"
        : $"{Identifier}";
}

[DebuggerDisplay("{AccessModifier,nq} Constructor({Parameters,nq})")]
public class ConstructorNode(AccessModifier accessModifier, ParameterListNode parameters, AstNode body, List<AttributeNode>? attributes = null) : MemberNode(attributes)
{
    public AccessModifier AccessModifier { get; set; } = accessModifier;
    public ParameterListNode Parameters { get; set; } = parameters;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => [Parameters, Body];
}

[DebuggerDisplay("{AccessModifier,nq} {ReturnType,nq} {MethodName,nq}({Parameters,nq})")]
public class MethodNode(
    AccessModifier accessModifier,
    List<OptionalModifier> modifiers,
    TypeNode returnType,
    AstNode methodName,
    ParameterListNode parameters,
    AstNode? body, 
    List<AttributeNode>? attributes = null
    ) : MemberNode(attributes)
{
    public AccessModifier AccessModifier { set; get; } = accessModifier;
    public List<OptionalModifier> Modifiers { get; set; } = modifiers;
    public TypeNode ReturnType { get; set; } = returnType;
    public AstNode MethodName { get; set; } = methodName;
    public ParameterListNode Parameters { get; set; } = parameters;
    public AstNode? Body { get; set; } = body;

    public override List<AstNode> Children => Utils.ParamsToList(ReturnType, MethodName, Parameters, Body);
}

public class BasicDeclarationNode(
    AstNode name, List<MemberNode> members, AstNode? parentName = null, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null, 
    List<AttributeNode>? attributes = null, ParameterListNode? parameters = null, ArgumentListNode? baseArguments = null) : TypeDeclarationNode(attributes)
{
    public AccessModifier AccessModifier { get; set; } = accessModifier ?? AccessModifier.Internal;
    public List<OptionalModifier> Modifiers { get; set; } = modifiers ?? [];
    public AstNode? ParentName { get; set; } = parentName;
    public AstNode Name { get; set; } = name;
    public List<MemberNode> Members { get; set; } = members;

    // Primary Constructor parameters
    public ParameterListNode Parameters { get; set; } = parameters ?? new([]);
    public ArgumentListNode BaseArguments { get; set; } = baseArguments ?? new([]);

    public override List<AstNode> Children => [.. Members];
}

[DebuggerDisplay("class {Name,nq}")]
public class ClassDeclarationNode(
    AstNode className, List<MemberNode> members, AstNode? parentName = null, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null, 
    List<AttributeNode>? attributes = null, ParameterListNode? parameters = null, ArgumentListNode? baseArguments = null
    ) : BasicDeclarationNode(className, members, parentName, accessModifier, modifiers, attributes, parameters, baseArguments)
{

}

[DebuggerDisplay("interface {Name,nq}")]
public class InterfaceDeclarationNode(
    AstNode name, List<MemberNode> members, AstNode? parentName = null, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null, 
    List<AttributeNode>? attributes = null
    ) : BasicDeclarationNode(name, members, parentName, accessModifier, modifiers, attributes)
{

}

[DebuggerDisplay("struct {Name,nq}")]
public class StructDeclarationNode(
    AstNode name, List<MemberNode> members, AstNode? parentName = null, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null,
    List<AttributeNode>? attributes = null, ParameterListNode? parameters = null, ArgumentListNode? baseArguments = null
    ) : BasicDeclarationNode(name, members, parentName, accessModifier, modifiers, attributes, parameters, baseArguments)
{

}

[DebuggerDisplay("enum {EnumName,nq}")]
public class EnumDeclarationNode(
    AstNode enumName, List<EnumMemberNode> members, AstNode? parentType, 
    AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null, 
    List<AttributeNode>? attributes = null) : TypeDeclarationNode(attributes)
{
    public AccessModifier AccessModifier { get; set; } = accessModifier ?? AccessModifier.Internal;
    public List<OptionalModifier> Modifiers { get; set; } = modifiers ?? [];
    public AstNode? ParentType { get; set; } = parentType;
    public AstNode EnumName { get; set; } = enumName;
    public List<EnumMemberNode> Members { get; set; } = members;

    public override List<AstNode> Children => [.. Members];
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
    List<OptionalModifier> modifiers, AstNode name, TypeNode returnType, ParameterListNode parameters, AstNode body
    ) : StatementNode
{
    public List<OptionalModifier> Modifiers { get; set; } = modifiers;
    public AstNode Name { get; set; } = name;
    public TypeNode ReturnType { get; set; } = returnType;
    public ParameterListNode Parameters { get; set; } = parameters;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => [Name, ReturnType, Parameters, Body];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{ReturnType} {Name}({Parameters})";
}

public abstract class ElementNode : AstNode
{
}

[DebuggerDisplay("{ToString(),nq}")]
public class SpreadElementNode(ExpressionNode expression) : ElementNode
{
    public ExpressionNode Expression { get; set; } = expression;

    public override List<AstNode> Children => [Expression];

    public override string ToString() => $".. {Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ExpressionElementNode(ExpressionNode expression) : ElementNode
{
    public ExpressionNode Expression { get; set; } = expression;

    public override List<AstNode> Children => [Expression];

    public override string ToString() => $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class CollectionExpressionNode(List<ElementNode> elements) : ExpressionNode
{
    public List<ElementNode> Elements { get; set; } = elements;

    public override List<AstNode> Children => [.. Elements];

    public override string ToString() => string.Join(", ", Elements);
}

[DebuggerDisplay("{ToString(),nq}")]
public class LambdaParameterNode(string identifier, TypeNode? type = null) : AstNode
{
    public TypeNode? Type { get; set; } = type;
    public string Identifier { get; set; } = identifier;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Type);

    [ExcludeFromCodeCoverage]
    public override string ToString()
        => $"{Type} {Identifier}";
}

public class LambdaExpressionNode(List<LambdaParameterNode> parameters, AstNode body) : ExpressionNode
{
    public List<LambdaParameterNode> Parameters { get; set; } = parameters;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => [Body];
}

public abstract class PatternNode : AstNode
{
}

[DebuggerDisplay("{ToString(),nq}")]
public class IsExpressionNode(PatternNode pattern) : ExpressionNode
{
    public PatternNode Pattern { get; set; } = pattern;

    public override List<AstNode> Children => [Pattern];
    public override string ToString() => $"is {Pattern}";
}

public class DeclarationPatternNode(TypeNode type, string identifier) : PatternNode
{
    public TypeNode Type { get; set; } = type;
    public string Identifier { get; set; } = identifier;

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
    public RelationalPatternOperator Operator { get; set; } = op;
    public ExpressionNode Value { get; set; } = value;

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

[DebuggerDisplay("{Value,nq}")]
public class ConstantPatternNode(ExpressionNode value) : PatternNode
{
    public ExpressionNode Value { get; set; } = value;
    public override List<AstNode> Children => [Value];
}

[DebuggerDisplay("_")]
public class DiscardPatternNode : PatternNode
{
    public override List<AstNode> Children => [];
}

public abstract class LogicalPatternNode : PatternNode
{

}


public class AndPatternNode(PatternNode lhs, PatternNode rhs) : LogicalPatternNode
{
    public PatternNode LHS { get; set; } = lhs;
    public PatternNode RHS { get; set; } = rhs;

    public override List<AstNode> Children => [LHS, RHS];
    public override string ToString() => $"{LHS} and {RHS}";
}

public class OrPatternNode(PatternNode lhs, PatternNode rhs) : LogicalPatternNode
{
    public PatternNode LHS { get; set; } = lhs;
    public PatternNode RHS { get; set; } = rhs;

    public override List<AstNode> Children => [LHS, RHS];
    public override string ToString() => $"{LHS} or {RHS}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class NotPatternNode(PatternNode pattern) : LogicalPatternNode
{
    public PatternNode Pattern { get; set; } = pattern;

    public override List<AstNode> Children => [Pattern];

    public override string ToString() => $"not {Pattern}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ParenthesizedPatternNode(PatternNode pattern) : PatternNode
{
    public PatternNode InnerPattern { get; set; } = pattern;

    public override List<AstNode> Children => [InnerPattern];

    public override string ToString() => $"({InnerPattern})";
}

public abstract class SwitchSectionNode : AstNode
{
}

[DebuggerDisplay("{ToString(),nq}")]
public class SwitchCaseNode(PatternNode casePattern, List<StatementNode> statements, ExpressionNode? whenClause = null) : SwitchSectionNode
{
    PatternNode CasePattern { get; set; } = casePattern;
    public List<StatementNode> Statements { get; set; } = statements;
    public ExpressionNode? WhenClause = whenClause;

    public override List<AstNode> Children => [.. Utils.ParamsToList<AstNode>(CasePattern, WhenClause), .. Statements];

    public override string ToString() => $"case {CasePattern}:";
}

[DebuggerDisplay("default:")]
public class SwitchDefaultCaseNode(List<StatementNode> statements) : SwitchSectionNode
{
    public List<StatementNode> Statements { get; set; } = statements;

    public override List<AstNode> Children => [.. Statements];
}

[DebuggerDisplay("{ToString(),nq}")]
public class SwitchStatementNode(ExpressionNode switchExpression, List<SwitchSectionNode> sections) : StatementNode
{
    public ExpressionNode SwitchExpression { get; set; } = switchExpression;
    public List<SwitchSectionNode> SwitchSectionNodes { get; set; } = sections;

    public override List<AstNode> Children => [SwitchExpression, .. SwitchSectionNodes];
    public override string ToString() => $"switch {SwitchExpression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class SwitchExpressionArmNode(PatternNode condition, ExpressionNode value, ExpressionNode? whenClause = null) : AstNode
{
    public PatternNode Condition { get; set; } = condition;
    public ExpressionNode Value { get; set; } = value;
    public ExpressionNode? WhenClause { get; set; } = whenClause;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Condition, Value, WhenClause);

    public override string ToString() => WhenClause is not null
        ? $"{Condition} => {Value} when {WhenClause}"
        : $"{Condition} => {Value}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class SwitchExpressionNode(ExpressionNode switchExpression, List<SwitchExpressionArmNode> arms) : ExpressionNode
{
    public ExpressionNode SwitchExpression { get; set; } = switchExpression;
    public List<SwitchExpressionArmNode> SwitchArmNodes { get; set; } = arms;

    public override List<AstNode> Children => [SwitchExpression, .. SwitchArmNodes];
    public override string ToString() => $"{SwitchExpression} switch";
}

[DebuggerDisplay("break;")]
public class BreakStatementNode : StatementNode
{

}

[DebuggerDisplay("{ToString(),nq}")]
public class TernaryExpressionNode(ExpressionNode condition, ExpressionNode trueExpr, ExpressionNode falseExpr) : ExpressionNode
{
    public ExpressionNode Condition { get; set; } = condition;
    public ExpressionNode TrueExpr { get; set; } = trueExpr;
    public ExpressionNode FalseExpr { get; set; } = falseExpr;

    public override List<AstNode> Children => [Condition, TrueExpr, FalseExpr];

    [ExcludeFromCodeCoverage]
    public override string ToString()
        => $"{Condition} ? {TrueExpr} : {FalseExpr}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TypeofExpressionNode(TypeNode type) : ExpressionNode
{
    public TypeNode Type { get; set; } = type;

    public override List<AstNode> Children => [Type];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"typeof({Type})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class NameofExpressionNode(AstNode value) : ExpressionNode
{
    public AstNode Value { get; set; } = value;
    public override List<AstNode> Children => [Value];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"nameof({Value})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class SizeofExpressionNode(TypeNode type) : ExpressionNode
{
    public TypeNode Type { get; set; } = type;
    public override List<AstNode> Children => [Type];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"sizeof({Type})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class DefaultOperatorExpressionNode(TypeNode type) : ExpressionNode
{
    public TypeNode Type { get; set; } = type;
    public override List<AstNode> Children => [Type];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"default({Type})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class AwaitExpressionNode(ExpressionNode expression) : ExpressionNode
{
    public ExpressionNode Expression { get; set; } = expression;
    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"await {Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class AttributeArgumentNode(ExpressionNode expression) : AstNode
{
    public ExpressionNode Expression { get; set; } = expression;

    public override List<AstNode> Children => [Expression];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class AttributeNode(List<AttributeArgumentNode> arguments, string? target=null) : AstNode
{
    public List<AttributeArgumentNode> Arguments { get; set; } = arguments;
    public string? Target { get; set; } = target;

    public override List<AstNode> Children => [.. Arguments];

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"[{string.Join(", ", Arguments)}]";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ThrowStatementNode(ExpressionNode? expression) : StatementNode
{
    public ExpressionNode? Expression { get; } = expression;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Expression);

    public override string ToString() => $"throw {Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class CatchClauseNode(TypeNode exceptionType, string identifier, BlockNode block, ExpressionNode? whenClause=null) : AstNode
{
    public TypeNode ExceptionType { get; set; } = exceptionType;
    public string Identifier { get; set; } = identifier;
    public BlockNode Block { get; set; } = block;
    public ExpressionNode? WhenClause { get; set; } = whenClause;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(ExceptionType, Block, WhenClause);
    public override string ToString() => $"catch ({ExceptionType} {Identifier})";
}

[DebuggerDisplay("{ToString(),nq}")]
public class FinallyClauseNode(BlockNode block) : AstNode
{
    public BlockNode Block { get; set; } = block;

    public override List<AstNode> Children => [Block];
    public override string ToString() => "finally";
}

[DebuggerDisplay("{ToString(),nq}")]
public class TryStatementNode(BlockNode block, List<CatchClauseNode>? catchClauses=null, FinallyClauseNode? finallyClause=null) : StatementNode
{
    public BlockNode Block { get; set; } = block;
    public List<CatchClauseNode> CatchClauses { get; set; } = catchClauses ?? [];
    public FinallyClauseNode? FinallyClause { get; set; } = finallyClause;

    public override string ToString() => $"try";
}