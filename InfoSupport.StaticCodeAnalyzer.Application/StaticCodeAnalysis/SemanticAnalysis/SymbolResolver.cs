using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;

public enum SymbolKind
{
    Namespace,
    Type,
    Method,
    Field,
    Property,
    LocalVariable,
    Parameter
}

[DebuggerDisplay("{Name} {Kind}")]
public class Symbol(string name, NamespaceSymbol? containingNamespace, AstNode node, SymbolKind kind)
{
    public string Name { get; set; } = name;
    public NamespaceSymbol? ContainingNamespace { get; set; } = containingNamespace;
    public AstNode Node { get; set; } = node;
    public SymbolKind Kind { get; set; } = kind;
}

[DebuggerDisplay("{Name}")]
public class NamespaceSymbol(string name, NamespaceSymbol? containingNamespace)
{
    public string Name { get; set; } = name;
    public NamespaceSymbol? ContainingNamespace { get; set; } = containingNamespace;
    public List<NamespaceSymbol> Namespaces { get; set; } = [];
}

public class SymbolTable
{
    public List<Symbol> Symbols { get; set; } = [];
    public List<SymbolTable> InnerScopes { get; set; } = [];
    public SymbolTable? ParentTable { get; set; } = null;

    public void AddSymbol(Symbol symbol)
    {
        Symbols.Add(symbol);
    }
}

public class CompilationUnitSymbolContainer
{
    public Dictionary<AstNode, SymbolTable> ScopedSymbolTables = [];
}

public class SymbolResolver
{
    public List<CompilationUnitSymbolContainer> CompilationUnitContainers { get; set; } = [];

    public SymbolTable Resolve(AST ast)
    {
        var ns = new NamespaceSymbol(string.Empty, null);

        return ResolveScope(ast.Root, null, ns);
    }

    private SymbolTable ResolveScope(AstNode scope, SymbolTable? parent, NamespaceSymbol ns)
    {
        // @note: how do we keep track of using directives here?
        var table = new SymbolTable
        {
            ParentTable = parent
        };

        parent?.InnerScopes.Add(table);

        foreach (var node in scope.Children)
        {
            ResolveNode(node, table, ns);
        }
        
        return table;
    }

    private static NamespaceSymbol GetOrCreateNamespace(NamespaceSymbol ns, string name)
    {
        var result = ns?.Namespaces.Find(x => x.Name == name);

        if (result is not null)
            return result;

        result = new NamespaceSymbol(name, ns);

        ns?.Namespaces.Add(result);

        return result;
    }

    private static NamespaceSymbol EnterNamespace(NamespaceSymbol parent, NamespaceNode node)
    {
        var namespaces = node.Name.Split('.');

        NamespaceSymbol current = parent;

        foreach (var ns in namespaces)
        {
            var newNs = GetOrCreateNamespace(current, ns);
            current = newNs;
        }

        return current ?? throw new InvalidOperationException(); // shouldn't be able to be empty here
    }

    private void ResolveNode(AstNode node, SymbolTable symbolTable, NamespaceSymbol ns)
    {
        // Recursively handle node.. when do we want to call back into ResolveScope?;

        // Handle adding symbols to the table
        switch (node)
        {
            case NamespaceNode namespaceNode:
                {
                    symbolTable.AddSymbol(new Symbol(namespaceNode.Name, ns, node, SymbolKind.Namespace));
                    break;
                }

            case VariableDeclaratorNode variableDeclaratorNode:
                {
                    symbolTable.AddSymbol(new Symbol(variableDeclaratorNode.Identifier, ns, node, SymbolKind.LocalVariable));
                    break;
                }

            case TypeDeclarationNode typeDeclarationNode:
                {
                    symbolTable.AddSymbol(new Symbol(typeDeclarationNode.GetName(), ns, node, SymbolKind.Type));
                    break;
                }
        }

        // Handle adding inner scopes
        switch (node)
        {
            case NamespaceNode namespaceNode:
                {
                    ResolveScope(node, parent: symbolTable, ns: EnterNamespace(ns, namespaceNode));
                    break;
                }
            case BlockNode:
                {
                    ResolveScope(node, parent: symbolTable, ns: ns);
                    break;
                }
            default:
                {
                    foreach (var child in node.Children)
                    {
                        ResolveNode(child, symbolTable, ns);
                    }

                    break;
                }
        }
    }
}