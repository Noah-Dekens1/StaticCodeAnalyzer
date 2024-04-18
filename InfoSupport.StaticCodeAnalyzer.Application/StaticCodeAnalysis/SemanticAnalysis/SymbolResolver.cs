using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;

public enum SymbolKind
{
    Namespace,
    Type,
    Method,
    Field,
    Property,
    LocalVariable,
    Parameter,
    Constructor
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
    public List<string> UsingNamespaces { get; set; } = [];
    public NamespaceSymbol? ContainingNamespace { get; set; } = null;
    public string? TypeName { get; set; } = null;

    public List<string> GetAllUsingDirectives()
    {
        return [.. UsingNamespaces, ..(ParentTable?.GetAllUsingDirectives() ?? [])];
    }

    // @todo: maybe move to extension methods so we can keep this as a POD object
    // @fixme: what about (partially/fully) qualified names? like A.B.C (or if A is in scope then B.C)
    public Symbol? FindSymbol(string fullName)
    {
        var parts = fullName.Split('.');
        var next = parts.First();

        // Local check
        var symbol = FindSymbolLocalOrIncluded(next);

        // Check if symbol is not complete
        if (symbol is not null && parts.Length > 1)
        {
            // If namespace look for symbol table match
            if (symbol.Kind == SymbolKind.Namespace)
            {
                var nextTable = InnerScopes.Find(s => s.ContainingNamespace?.Name == symbol.Name);

                // @fixme: use array as parm instead?
                symbol = nextTable?.FindSymbol(string.Join('.', parts[1..]));
            }
            // If type look for matching TypeName
            else if (symbol.Kind == SymbolKind.Type)
            {
                var nextTable = InnerScopes.Find(s => s.TypeName == symbol.Name);

                // @fixme: use array as parm instead?
                symbol = nextTable?.FindSymbol(string.Join('.', parts[1..]));
            }
        }

        // Outer scopes check
        if (symbol is null && ParentTable is not null)
        {
            symbol = ParentTable.FindSymbol(fullName);
        }


        return symbol;
    }

    private Symbol? FindSymbolLocalOrIncluded(string name)
    {
        var local = Symbols.Find(s => s.Name == name);

        return local;
    }

    public void AddSymbol(Symbol symbol)
    {
        Symbols.Add(symbol);
    }
}

public class CompilationUnit(SymbolTable rootTable, NamespaceSymbol globalNamespace, AST ast)
{
    public SymbolTable RootTable { get; set; } = rootTable;
    public NamespaceSymbol GlobalNamespace { get; set; } = globalNamespace;
    public AST AST { get; set; } = ast;
}

public class SymbolResolver
{
    public List<CompilationUnit> CompilationUnits { get; set; } = [];

    public CompilationUnit Resolve(AST ast)
    {
        var cached = CompilationUnits.Find(u => u.AST == ast);

        if (cached is not null)
        {
            return cached;
        }

        var ns = new NamespaceSymbol(string.Empty, null);
        var table = ResolveScope(ast.Root, null, ns);

        var unit = new CompilationUnit(table, ns, ast);

        CompilationUnits.Add(unit);

        return unit;
    }

    private void MergeUsingSymbols(SymbolTable table)
    {
        
        foreach (var usingDirective in table.UsingNamespaces)
        {
            foreach (var unit in CompilationUnits)
            {
                var ns = GetNamespaceFromString(unit.GlobalNamespace, usingDirective);

                if (ns is null)
                    continue;

                var tables = FindTablesForNamespace(ns, unit.RootTable);

                foreach (var source in tables)
                {
                    table.Symbols.AddRange(source.Symbols);
                    table.InnerScopes.AddRange(source.InnerScopes);
                }
            }
        }

        foreach (var inner in table.InnerScopes)
        {
            MergeUsingSymbols(inner);
        }
    }

    public void ResolveUsings()
    {
        foreach (var unit in CompilationUnits)
        {
            var table = unit.RootTable;

            MergeUsingSymbols(table);
        }
    }


    // Resolve like member access / identifier expressions / generic names
    // to their owning symbols, taking into account method overloading, using directives, ...
    public Symbol? GetSymbolForExpression(ExpressionNode node)
    {
        // 1. Find in table recursively | but how do we match scope?
        //    -> maybe add FindFirstParentOfType<T> extension method?
        //    -> but how do we deal with non-blocknode scopes (like namespaces)
        //    -> should each ast node (or block node) can have a symbol table assigned? that doesn't seem right though
        //    -> maybe each SymbolTable should have a list of ast nodes assigned to it?
        //       -> that's way slower though
        // 2. ..?

        var table = node.SymbolTableRef ?? throw new Exception();
        var usings = table.GetAllUsingDirectives();

        string? name = null;

        if (node is IdentifierExpression identifierExpr)
        {
            name = identifierExpr.Identifier;
        }

        if (node is MemberAccessExpressionNode memberAccessExpr)
        {
             name = memberAccessExpr.AsLongIdentifier();
        }

        if (name is not null)
        {
            // Start by searching the local symbol table and its outers
            var symbol = table.FindSymbol(name);

            if (symbol is not null)
                return symbol;

            // If we have no match yet look through all the top-level compilation units
            foreach (var unit in CompilationUnits)
            {
                symbol = unit.RootTable.FindSymbol(name);

                if (symbol is not null) 
                    return symbol;
            }
        }


        return null;
    }

    private List<SymbolTable> FindTablesForQualifiedName(string qualifiedName)
    {
        var tables = new List<SymbolTable>();
        var parts = qualifiedName.Split('.');

        foreach (var unit in CompilationUnits)
        {
            var root = unit.GlobalNamespace;
            var current = root;

            bool matches = false;

            // Keep resolving namespaces until we can't
            foreach (var part in parts)
            {
                var ns = current.Namespaces.Find(n => n.Name == part);

                if (ns is null)
                {
                    break;
                }

                current = ns;
                matches = true; // but only partially
            }


            if (matches)
            {
                tables.AddRange(FindTablesForNamespace(current, unit.RootTable));
            }
        }

        return tables;
    }

    // @note: do we want to limit it to a single ns?
    private static List<SymbolTable> FindTablesForNamespace(NamespaceSymbol ns, SymbolTable symbolTable)
    {
        var result = new List<SymbolTable>();

        if (symbolTable.ContainingNamespace == ns)
            result.Add(symbolTable);

        foreach (var table in symbolTable.InnerScopes)
            result.AddRange(FindTablesForNamespace(ns, table));

        return result;
    }

    private SymbolTable ResolveScope(AstNode scope, SymbolTable? parent, NamespaceSymbol ns)
    {
        // @note: how do we keep track of using directives here?
        var table = new SymbolTable
        {
            ParentTable = parent,
            ContainingNamespace = ns,
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
    
    private static NamespaceSymbol? GetNamespaceFromString(NamespaceSymbol parent, string ns)
    {
        var namespaces = ns.Split('.');

        NamespaceSymbol current = parent;

        foreach (var n in namespaces)
        {
            var next = current.Namespaces.Find(x => x.Name == n);

            if (next is null)
                return null;

            current = next;
        }

        return current;
    }

    private readonly Dictionary<Type, SymbolKind> _memberSymbolKinds = new()
    {
        [typeof(MethodNode)] = SymbolKind.Method,
        [typeof(ConstructorNode)] = SymbolKind.Constructor,
        [typeof(FieldMemberNode)] = SymbolKind.Field,
        [typeof(PropertyMemberNode)] = SymbolKind.Property,
    };

    private void ResolveNode(AstNode node, SymbolTable symbolTable, NamespaceSymbol ns)
    {
        // Recursively handle node.. when do we want to call back into ResolveScope?;

        node.SymbolTableRef = symbolTable;

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

            case MemberNode memberNode when memberNode is not ConstructorNode:
                {
                    var kind = _memberSymbolKinds[memberNode.GetType()];
                    symbolTable.AddSymbol(new Symbol(memberNode.GetName(), ns, node, kind));
                    break;
                }

            case LocalFunctionDeclarationNode localFunctionDeclarationNode:
                {
                    symbolTable.AddSymbol(new Symbol(localFunctionDeclarationNode.Name.AsIdentifier()!, ns, node, SymbolKind.Method));
                    break;
                }

            case UsingDirectiveNode usingDirectiveNode:
                {
                    symbolTable.UsingNamespaces.Add(usingDirectiveNode.NamespaceOrType.AsIdentifier()!);
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
            case TypeDeclarationNode typeDeclarationNode: // want to keep type members in their own symbol tables
                {
                    var table = ResolveScope(node, parent: symbolTable, ns: ns);
                    table.TypeName = typeDeclarationNode.GetName();
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