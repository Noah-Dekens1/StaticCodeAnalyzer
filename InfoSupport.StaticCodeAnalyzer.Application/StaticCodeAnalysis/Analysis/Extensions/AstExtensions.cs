using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
public static class AstExtensions
{
    public static List<TypeDeclarationNode> GetTypeDeclarations(this AST ast)
    {
        return ast.GetNamespaces().SelectMany(ns => ns.TypeDeclarations).ToList();
    }

    private static List<NamespaceNode> GetNamespaces(this NamespaceNode node)
    {
        var namespaces = new List<NamespaceNode>
        {
            node
        };

        foreach (var ns in node.Namespaces)
        {
            namespaces.AddRange(ns.GetNamespaces());
        }

        return namespaces;
    }

    private static List<NamespaceNode> GetNamespaces(this AST ast)
    {
        return ast.Root.GetNamespaces();
    }

    public static List<ClassDeclarationNode> GetClasses(this AST ast)
    {
        return ast.GetNamespaces().SelectMany(ns => ns.TypeDeclarations).OfType<ClassDeclarationNode>().ToList();
    }

    public static ExpressionNode GetLeftMost(this MemberAccessExpressionNode memberAccess)
    {
        if (memberAccess.LHS is MemberAccessExpressionNode ma)
            return ma.GetLeftMost();

        return memberAccess.LHS;
    }

    public static string? AsIdentifier(this AstNode node)
    {
        if (node is IdentifierExpression expr)
            return expr.Identifier;

        if (node is MemberAccessExpressionNode memberAccess)
            return ((IdentifierExpression)memberAccess.Identifier).Identifier;

        if (node is GenericNameNode genericName)
            return genericName.Identifier.AsIdentifier();

        return null;
    }

    public static string? AsLongIdentifier(this AstNode node)
    {
        if (node is IdentifierExpression expr)
            return expr.Identifier;

        if (node is GenericNameNode generiName)
            return generiName.Identifier.AsIdentifier();

        if (node is QualifiedNameNode qualifiedName)
            return $"{qualifiedName.LHS.AsLongIdentifier()}.{qualifiedName.Identifier.AsLongIdentifier()}";

        if (node is MemberAccessExpressionNode memberAccess)
            return $"{memberAccess.LHS.AsLongIdentifier()}.{memberAccess.Identifier.AsLongIdentifier()}";

        return null;
    }

    private static bool HasAttribute(List<AttributeNode> attributes, string attributeName, [NotNullWhen(true)] out AttributeNode? attribute)
    {
        attribute = null;

        foreach (var attr in attributes)
        {
            foreach (var arg in attr.Arguments)
            {
                if (arg.Expression is InvocationExpressionNode invocation)
                {
                    var name = invocation.LHS.AsIdentifier();

                    if (name == attributeName)
                    {
                        attribute = attr;
                        return true;
                    }
                }

                if (arg.Expression is IdentifierExpression identifier)
                {
                    if (identifier.AsIdentifier() == attributeName)
                    {
                        attribute = attr;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static List<T> GetAllDescendantsOfType<T>(this AstNode node, bool includeSelf = false)
        where T : AstNode
    {
        var matches = new List<T>();

        if (includeSelf && node is T match)
        {
            matches.Add(match);
        }

        foreach (var child in node.Children)
        {
            matches.AddRange(child.GetAllDescendantsOfType<T>(true));
        }

        return matches;
    }

    // It may be difficult to distinguish between
    // 1) Local function invocations
    // 2) Lambdas and potentially nested local functions in lambdas
    // 3) User method calls
    // 4) Library method calls
    // 5) Methods in different namespaces
    // 6) Overloaded methods
    public static List<MethodNode> GetAllCalledMethods(this AstNode node)
    {
        var invocations = node.GetAllDescendantsOfType<InvocationExpressionNode>();
        
        foreach (var invocation in invocations)
        {
            var lhs = invocation.LHS;
            var name = lhs.AsIdentifier();

            if (name is not null)
            {

            }
            else
            {
                Console.WriteLine($"Warning: couldn't resolve invocation {invocation}");
            }
        }

        return [];
    }

    // @todo: consider using interfaces to prevent code duplication here?

    public static bool HasAttribute(this TypeDeclarationNode node, string attributeName, [NotNullWhen(true)] out AttributeNode? attribute)
    {
        return HasAttribute(node.Attributes, attributeName, out attribute);
    }

    public static bool HasAttribute(this TypeDeclarationNode node, string attributeName)
    {
        return HasAttribute(node.Attributes, attributeName, out _);
    }

    public static bool HasAttribute(this MemberNode node, string attributeName, [NotNullWhen(true)] out AttributeNode? attribute)
    {
        return HasAttribute(node.Attributes, attributeName, out attribute);
    }

    public static bool HasAttribute(this MemberNode node, string attributeName)
    {
        return HasAttribute(node.Attributes, attributeName, out _);
    }

    public static NamespaceNode? GetNamespace(this AstNode node)
    {
        if (node.Parent is NamespaceNode ns)
        {
            return ns;
        }

        if (node.Parent is null)
        {
            return null;
        }

        return GetNamespace(node.Parent);
    }

    // @fixme: what about type arguments?
    public static string GetName(this TypeDeclarationNode node)
    {
        var name = (node is BasicDeclarationNode basicDeclaration) ? basicDeclaration.Name : ((EnumDeclarationNode)node).EnumName;
        return name.AsIdentifier()!;
    }

    public static string GetName(this MemberNode node)
    {
        if (node is MethodNode method)
            return method.MethodName.AsIdentifier()!;

        if (node is ConstructorNode constructor)
            return ((BasicDeclarationNode)constructor.Parent!).Name.AsIdentifier()!;

        if (node is FieldMemberNode field)
            return field.FieldName;

        if (node is PropertyMemberNode property)
            return property.PropertyName;

        throw new InvalidOperationException();
    }

    public static AstNode? GetFirstParent(this AstNode node, Func<AstNode, bool> predicate)
    {
        return predicate(node) 
            ? node 
            : node.Parent is not null
                ? GetFirstParent(node.Parent, predicate)
                : null;
    }

    public static T? GetFirstParent<T>(this AstNode node) where T : AstNode
    {
        return node as T ?? node.Parent?.GetFirstParent<T>();
    }

    public static ClassDeclarationNode? GetParentClass(this ClassDeclarationNode node, ProjectRef project)
    {
        foreach (var parent in node.ParentNames)
        {
            var symbol = project.SemanticModel.SymbolResolver.GetSymbolForNode(parent);

            if (symbol?.Node is ClassDeclarationNode classDeclaration)
                return classDeclaration;
        }

        return null;
    }

    public static List<InterfaceDeclarationNode> GetInterfaces(this BasicDeclarationNode node, ProjectRef project)
    {
        var interfaces = new List<InterfaceDeclarationNode>();

        foreach (var parent in node.ParentNames)
        {
            var symbol = project.SemanticModel.SymbolResolver.GetSymbolForNode(parent);

            if (symbol?.Node is InterfaceDeclarationNode interfaceDeclaration)
                interfaces.Add(interfaceDeclaration);
        }

        return interfaces;
    }

    public static bool IsNameEqual(this AstNode node, AstNode other)
    {
        if (node.GetType() != other.GetType()) 
            return false;

        return node.AsLongIdentifier() == other.AsLongIdentifier();
    }
}
