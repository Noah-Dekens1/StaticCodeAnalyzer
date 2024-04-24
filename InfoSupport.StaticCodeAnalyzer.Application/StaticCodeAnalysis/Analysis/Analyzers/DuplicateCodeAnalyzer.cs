using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;

public static class DuplicateCodeExtensions
{
    public static bool IsSemanticallyEquivalent(this AstNode node, AstNode other)
    {
        if (node.GetType() != other.GetType()) 
            return false;

        switch (node)
        {
            case LiteralExpressionNode literalNode:
                {
                   return literalNode.ToString() == other.ToString();
                }
        }

        return true;
    }
}

public class DuplicateCodeTraverser : AstTraverser
{
    public List<List<StatementNode>> StatementLists { get; set; } = [];

    private bool _scanning = false;

    public void Scan(AST ast)
    {
        _scanning = true;
        Visit(ast.Root);
        _scanning = false;
    }

    protected override void Visit(AstNode node)
    {
        base.Visit(node);
    }

    protected override void VisitStatement(StatementNode statement)
    {
        if (_scanning)
        { 
        }
    }
}

public class DuplicateCodeAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var methods = ast.Root.GetAllDescendantsImplementing<IMethod>();

        foreach (var method in methods)
        {

        }

        return false;
    }

    
}
