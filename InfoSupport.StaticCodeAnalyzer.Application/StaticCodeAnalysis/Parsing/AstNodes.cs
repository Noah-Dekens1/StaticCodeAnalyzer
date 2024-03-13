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
    public List<TypeDeclarationNode> TypeDeclarations { get; set; } = [];

    public override List<AstNode> Children => [.. UsingDirectives, .. GlobalStatements, .. TypeDeclarations];
}

public class StatementNode : AstNode
{
    public override List<AstNode> Children => [];
}

[DebuggerDisplay("return {ToString(),nq}")]
public class ReturnStatementNode(ExpressionNode? returnExpression) : StatementNode
{
    public ExpressionNode? ReturnExpression { get; set; } = returnExpression;

    public override List<AstNode> Children => [ReturnExpression];

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

    public override string ToString() => $"{Value}";
}

[DebuggerDisplay("{ToString()}")]
public class BooleanLiteralNode(bool value) : LiteralExpressionNode
{
    public bool Value { get; set; } = value;

    public override string ToString() => $"{(Value ? "true" : "false")}";
}

[DebuggerDisplay("{ToString()}")]
public class StringLiteralNode(string value) : LiteralExpressionNode
{
    public string Value { get; set; } = value;

    public override string ToString() => $"\"{Value}\"";
}

[DebuggerDisplay("{ToString()}")]
public class ParenthesizedExpressionNode(ExpressionNode expr) : ExpressionNode
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

    // Compound
    AddAssign,
    SubtractAssign,
    MultiplyAssign,
    DivideAssign,
    ModulusAssign,
    AndAssign,
    OrAssign,

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

        BinaryOperator.AddAssign => "+=",
        BinaryOperator.SubtractAssign => "-=",
        BinaryOperator.MultiplyAssign => "*=",
        BinaryOperator.DivideAssign => "/=",
        BinaryOperator.ModulusAssign => "%=",
        BinaryOperator.AndAssign => "&=",
        BinaryOperator.OrAssign => "|=",

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
public class BracketedArgumentList(List<ArgumentNode> arguments) : ArgumentList(arguments)
{

}

[DebuggerDisplay("{ToString(),nq}")]
public class ParameterNode(string type, string identifier) : AstNode
{
    public string Type { get; set; } = type;
    public string Identifier { get; set; } = identifier;

    public override List<AstNode> Children => [];

    public override string ToString()
        => $"{Type} {Identifier}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class ParameterListNode(List<ParameterNode> parameters) : AstNode
{
    public List<ParameterNode> Parameters { get; set; } = parameters;

    public override List<AstNode> Children => [..Parameters];

    public override string ToString() => Parameters.Count >= 10
        ? $"{Parameters.Count} parameters"
        : string.Join(',', Parameters.Select(a => a.ToString()));

}

[DebuggerDisplay("{ToString(),nq}")]
public class InvocationExpressionNode(ExpressionNode lhs, ArgumentList arguments) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public ArgumentList Arguments { get; set; } = arguments;

    public override List<AstNode> Children => [LHS, Arguments];

    public override string ToString() => $"{LHS}({Arguments})";
}

// @todo: maybe add new class BracketedArgumentList that inherits from ArgumentList instead
[DebuggerDisplay("{ToString(),nq}")]
public class ElementAccessExpressionNode(ExpressionNode lhs, BracketedArgumentList arguments) : ExpressionNode
{
    public ExpressionNode LHS { get; set; } = lhs;
    public ArgumentList Arguments { get; set; } = arguments;

    public override List<AstNode> Children => [LHS, Arguments];

    public override string ToString() => $"{LHS}[{Arguments}]";
}

[DebuggerDisplay("{ToString(),nq}")]
public class IndexExpressionNode(ExpressionNode expression) : ExpressionNode
{
    public ExpressionNode Expression { get; set; } = expression;

    public override List<AstNode> Children => [Expression];
    public override string ToString() => $"{Expression}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class NewExpressionNode(ExpressionNode identifier, ArgumentList arguments) : ExpressionNode
{
    public ExpressionNode Identifier { get; set; } = identifier;
    public ArgumentList Arguments { get; set; } = arguments;
    public override List<AstNode> Children => [Identifier, Arguments];

    public override string ToString() => $"new {Identifier}({Arguments})";
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

public abstract class TypeDeclarationNode : AstNode
{
    
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
}

public abstract class MemberNode : AstNode
{

}

[DebuggerDisplay("{ToString(),nq}")]
public class FieldMemberNode(AccessModifier accessModifier, List<OptionalModifier> modifiers, string fieldName, string fieldType, ExpressionNode? value) : MemberNode
{
    public AccessModifier AccessModifier = accessModifier;
    public List<OptionalModifier> Modifiers = modifiers;
    public string FieldName { get; set; } = fieldName;
    public string FieldType { get; set; } = fieldType;
    public ExpressionNode? Value { get; set; } = value;

    public override List<AstNode> Children => Value is not null ? [Value] : [];

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


public class PropertyMemberNode(string propertyName, string propertyType, PropertyAccessorNode? getter, PropertyAccessorNode? setter, ExpressionNode? value) : MemberNode
{
    public string PropertyName { get; set; } = propertyName;
    public string PropertyType { get; set; } = propertyType;
    public PropertyAccessorNode? Getter { get; set; } = getter;
    public PropertyAccessorNode? Setter { get; set; } = setter;
    public ExpressionNode? Value { get; set; } = value;

    public override List<AstNode> Children => Utils.ParamsToList<AstNode>(Getter, Setter, Value);

    public override string ToString() => $"{PropertyType} {PropertyName}";
}

[DebuggerDisplay("{ToString(),nq}")]
public class EnumMemberNode(string identifier, ExpressionNode? value) : MemberNode
{
    public string Identifier { get; set; } = identifier;
    public ExpressionNode? Value { get; set; } = value;

    public override List<AstNode> Children => Value is not null ? [Value] : [];

    public override string ToString() => Value is not null
        ? $"{Identifier} = {Value}"
        : $"{Identifier}";
}

[DebuggerDisplay("{AccessModifier,nq} Constructor({Parameters,nq})")]
public class ConstructorNode(AccessModifier accessModifier, ParameterListNode parameters, AstNode body) : MemberNode
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
    string returnType,
    string methodName,
    ParameterListNode parameters,
    AstNode? body) : MemberNode
{
    public AccessModifier AccessModifier {  set; get; } = accessModifier;
    public List<OptionalModifier> Modifiers { get; set; } = modifiers;
    public string ReturnType { get; set; } = returnType;
    public string MethodName { get; set; } = methodName;
    public ParameterListNode Parameters { get; set; } = parameters;
    public AstNode? Body { get; set; } = body;

    public override List<AstNode> Children => [Parameters, Body];
}

public class BasicDeclarationNode(string name, List<MemberNode> members, string? parentName = null, AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null) : TypeDeclarationNode
{
    public AccessModifier AccessModifier { get; set; } = accessModifier ?? AccessModifier.Internal;
    public List<OptionalModifier> Modifiers { get; set; } = modifiers ?? [];
    public string? ParentName { get; set; } = parentName;
    public string Name { get; set; } = name;
    public List<MemberNode> Members { get; set; } = members;

    public override List<AstNode> Children => [.. Members];
}

[DebuggerDisplay("class {Name,nq}")]
public class ClassDeclarationNode(string className, List<MemberNode> members, string? parentName = null, AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null) 
    : BasicDeclarationNode(className, members, parentName, accessModifier, modifiers)
{

}

[DebuggerDisplay("interface {Name,nq}")]
public class InterfaceDeclarationNode(string name, List<MemberNode> members, string? parentName = null, AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null)
    : BasicDeclarationNode(name, members, parentName, accessModifier, modifiers)
{

}

[DebuggerDisplay("struct {Name,nq}")]
public class StructDeclarationNode(string name, List<MemberNode> members, string? parentName = null, AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null)
    : BasicDeclarationNode(name, members, parentName, accessModifier, modifiers)
{

}

[DebuggerDisplay("enum {EnumName,nq}")]
public class EnumDeclarationNode(string enumName, List<EnumMemberNode> members, string? parentType, AccessModifier? accessModifier = null, List<OptionalModifier>? modifiers = null) : TypeDeclarationNode
{
    public AccessModifier AccessModifier { get; set; } = accessModifier ?? AccessModifier.Internal;
    public List<OptionalModifier> Modifiers { get; set; } = modifiers ?? [];
    public string? ParentType { get; set;} = parentType;
    public string EnumName { get; set; } = enumName;
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
    List<OptionalModifier> modifiers, string name, string returnType, ParameterListNode parameters, AstNode body
    ) : StatementNode
{
    public List<OptionalModifier> Modifiers { get; set; } = modifiers;
    public string Name { get; set; } = name;
    public string ReturnType { get; set; } = returnType;
    public ParameterListNode Parameters { get; set; } = parameters;
    public AstNode Body { get; set; } = body;

    public override List<AstNode> Children => [Parameters, Body];

    public override string ToString() => $"{ReturnType} {Name}({Parameters})";
}