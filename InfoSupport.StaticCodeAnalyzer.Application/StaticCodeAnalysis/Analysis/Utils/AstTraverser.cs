using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
public class AstTraverser
{
    public void Traverse(AstNode node)
    {
        Visit(node);
    }

    protected virtual void Visit(AstNode node)
    {
        foreach (var child in node.Children)
        {
            Visit(child);

            if (child is StatementNode statement && child is not BlockNode)
                VisitStatement(statement);
        }
    }

    protected virtual void VisitStatement(StatementNode statement)
    {

    }
}
