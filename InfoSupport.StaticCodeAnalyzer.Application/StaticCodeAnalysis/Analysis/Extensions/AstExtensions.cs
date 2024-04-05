using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
        var namespaces = new List<NamespaceNode>();
        namespaces.AddRange(node.Namespaces);

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

    private static string? AsIdentifier(this ExpressionNode expression)
    {
        if (expression is IdentifierExpression expr)
            return expr.Identifier;

        if (expression is MemberAccessExpressionNode memberAccess)
            return ((IdentifierExpression)memberAccess.Identifier).Identifier;

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
}
