using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

public abstract class AstNode
{
    public List<Token> Tokens { get; set; } = [];

    public abstract List<AstNode> Children { get; }
}

public class RootNode : AstNode
{
    public List<UsingDirectiveNode> UsingDirectives { get; set; } = [];
    public List<GlobalStatementNode> GlobalStatements { get; set; } = [];

    public override List<AstNode> Children => [.. GlobalStatements];
}

public class StatementNode : AstNode
{
    public override List<AstNode> Children => [];
}

[DebuggerDisplay("{Statement,nq}")]
public class GlobalStatementNode : AstNode
{
    public required StatementNode Statement { get; set; }

    public override List<AstNode> Children => [Statement];
}

[DebuggerDisplay("{Expression,nq}")]
public class ExpressionStatementNode : StatementNode
{
    public required ExpressionNode Expression { get; set; }
    public override List<AstNode> Children => [Expression];

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
public class NumericLiteralNode : LiteralExpressionNode
{
    public object? Value { get; set; }

    public override string ToString() => $"{Value}";
}

[DebuggerDisplay("{ToString()}")]
public class BooleanLiteralNode : LiteralExpressionNode
{
    public bool Value { get; set; }

    public override string ToString() => $"{(Value ? "true" : "false")}";
}

[DebuggerDisplay("{ToString()}")]
public class StringLiteralNode : LiteralExpressionNode
{
    public required string Value { get; set; }

    public override string ToString() => $"\"{Value}\"";
}

[DebuggerDisplay("{ToString()}")]
public class ParenthesizedExpression(ExpressionNode expr) : ExpressionNode
{
    public ExpressionNode Expression { get; set; } = expr;

    public override string ToString() => $"({Expression})";

    public override List<AstNode> Children => [Expression];
}



public enum UnaryOperator
{
    Negation,
    LogicalNot,
    Increment,
    Decrement
}

[DebuggerDisplay("{ToString()}")]
public class UnaryExpressionNode : ExpressionNode
{
    public ExpressionNode Expression { get; set; }
    public bool IsPrefix { get; set; } = true;

    public virtual UnaryOperator Operator { get; }

    public UnaryExpressionNode(ExpressionNode expr, bool isPrefix = true)
    {
        Expression = expr;
        IsPrefix = isPrefix;
    }

    private string OperatorForDbg => Operator switch
    {
        UnaryOperator.Negation => "-",
        UnaryOperator.LogicalNot => "!",
        UnaryOperator.Increment => "++",
        UnaryOperator.Decrement => "--",
        _ => throw new NotImplementedException()
    };

    public override List<AstNode> Children => [Expression];

    public override string ToString() => $"{(IsPrefix ? OperatorForDbg : "")}{Expression}{(!IsPrefix ? OperatorForDbg : "")}";
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

    Assignment,

    // ...
}

[DebuggerDisplay("{ToString()}")]
public class BinaryExpressionNode(ExpressionNode lhs, ExpressionNode rhs) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public ExpressionNode RHS { get; set; } = rhs;

    public virtual BinaryOperator Operator { get; }
    public override List<AstNode> Children => [LHS, RHS];

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

        BinaryOperator.Assignment => "=",

        _ => throw new NotImplementedException()
    };

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

[DebuggerDisplay("{Type,nq} {Identifier,nq} = {Expression,nq}")]
public class VariableDeclarationStatement(string type, string identifier, ExpressionNode expression) : StatementNode
{
    public string Type { get; set; } = type;
    public string Identifier { get; set; } = identifier;
    public ExpressionNode Expression { get; set; } = expression;
    public override List<AstNode> Children => [Expression];
}

[DebuggerDisplay(";")]
public class EmptyStatementNode : StatementNode
{

}

public class BlockNode(List<StatementNode> statements) : AstNode
{
    public List<StatementNode> Statements { get; set; } = statements;
    public override List<AstNode> Children => [.. Statements];
}

public class TernaryExpressionNode : ExpressionNode
{

}

// @FIXME is an identifier an expression and not just a part of an expression?
[DebuggerDisplay("{Identifier,nq}")]
public class IdentifierExpression : ExpressionNode
{
    public required string Identifier { get; set; }

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
    AstNode initializer, ExpressionNode condition, AstNode iteration, AstNode body
    ) : StatementNode
{
    public AstNode Initializer { get; set; } = initializer;
    public ExpressionNode Condition { get; set; } = condition;
    public AstNode IterationExpression { get; set; } = iteration;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => [Initializer, Condition, IterationExpression, Body];
}

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class ExpressionStatementListNode(List<ExpressionStatementNode> statements) : AstNode
{
    public List<ExpressionStatementNode> Statements { get; set; } = statements;

    public override List<AstNode> Children => [.. Statements];

    private string DebuggerDisplay
    {
        get => string.Join(',', Children);
    }
}

[DebuggerDisplay("foreach ({VariableType,nq} {VariableIdentifier,nq} in {Collection,nq}) ...")]
public class ForEachStatementNode(string variableType, string variableIdentifier, ExpressionNode collection, AstNode body) : StatementNode
{
    public string VariableType { get; set; } = variableType;
    public string VariableIdentifier { get; set; } = variableIdentifier;
    public ExpressionNode Collection {  get; set; } = collection;
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

[DebuggerDisplay("{LHS,nq}.{Identifier,nq}")]
public class MemberAccessExpressionNode(ExpressionNode lhs, IdentifierExpression identifier) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public IdentifierExpression Identifier { get; set; } = identifier;

    public override List<AstNode> Children => [LHS, Identifier];

    public override string ToString() => $"{LHS}.{Identifier}";
}

[DebuggerDisplay("{ToString()}")]
public class ArgumentNode(ExpressionNode expression, string? name) : AstNode
{
    public ExpressionNode Expression { get; set; } = expression;
    public string? Name { get; set; } = name;

    public override List<AstNode> Children => [Expression];

    public override string ToString() => Name is not null
        ? $"{Name}: {Expression}"
        : $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ArgumentList(List<ArgumentNode> arguments) : AstNode
{
    public List<ArgumentNode> Arguments { get; set; } = arguments;

    public override List<AstNode> Children => [ .. Arguments];

    public override string ToString() => Arguments.Count >= 10
        ? $"{Arguments.Count} arguments"
        : string.Join(',', Arguments.Select(a => a.ToString()));
}

[DebuggerDisplay("{ToString(),nq}")]
public class InvocationExpressionNode(ExpressionNode lhs, ArgumentList arguments) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public ArgumentList Arguments { get; set; } = arguments;

    public override List<AstNode> Children => [LHS, Arguments];

    public override string ToString() => $"{LHS}({Arguments})";
}

[DebuggerDisplay("{LHS,nq}.{Identifier,nq}")]
public class QualifiedNameNode(AstNode lhs, IdentifierExpression identifier) : AstNode
{
    public AstNode LHS { get; set; } = lhs;
    public IdentifierExpression Identifier { get; set; } = identifier;

    public override List<AstNode> Children => [LHS, Identifier];

    public override string ToString() => $"{LHS}.{Identifier}";

}

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class UsingDirectiveNode(AstNode ns, string? alias) : AstNode
{
    public string? Alias { get; set; } = alias;
    public AstNode Namespace { get; set; } = ns;

    public override List<AstNode> Children => [Namespace];

    private string DebuggerDisplay
    {
        get => Alias is not null ? $"using {Alias} = {Namespace}" : $"using {Namespace}";
    }
}