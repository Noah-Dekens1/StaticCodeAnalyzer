using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;

[DebuggerDisplay("{Name}")]
public record NamespaceRepNode
{
    // Review: Sometimes not using a primary ctor but using the required and init keyword is more readable
    // This is subjective of course
    public required string Name { get; init; }
    public List<NamespaceRepNode> Namespaces { get; } = [];
    public List<TypeDeclarationNode> TypeDeclarations { get; } = [];
}

public class TypeLookup
{
    public NamespaceRepNode Root { get; set; } = new() { Name = "" };

    public void GenerateTypeMappings(ProjectRef projectRef)
    {
        foreach (var projectFile in projectRef.ProjectFiles)
        {
            var ast = projectFile.Value;
            CreateNamespace(Root, ast.Root);
        }

        Console.WriteLine();
    }

    private static NamespaceRepNode GetOrCreateNamespace(NamespaceRepNode ns, string name)
    {
        var result = ns.Namespaces.Find(x => x.Name == name);

        if (result is not null)
            return result;

        result = new NamespaceRepNode { Name = name };

        ns.Namespaces.Add(result);

        return result;
    }

    private static void HandleNamespace(NamespaceRepNode ns, NamespaceNode source, bool recurse = false)
    {
        var parts = source.Name.Split('.');

        var currentNs = ns;

        foreach (var part in parts)
        {
            var newNs = GetOrCreateNamespace(currentNs, part);
            currentNs = newNs;
        }

        currentNs.TypeDeclarations.AddRange(source.TypeDeclarations);
    }

    private static void CreateNamespace(NamespaceRepNode ns, NamespaceNode source)
    {
        //if (includeSelf)
        //    HandleNamespace(ns, source);

        // @note: a namespace may be qualified as Namespace1.Namespace2 
        // The parser sees this as a single namespace while we should see it as two
        // This way we can better understand namespaces living across multiple files

        foreach (var childNs in source.Namespaces)
        {
            HandleNamespace(ns, childNs, recurse: true);
        }
    }

    public void GetAllTypesInNamespace(string ns, bool includeChildNamespaces = true)
    {

    }

    public void FindType(string ns)
    {

    }
}
