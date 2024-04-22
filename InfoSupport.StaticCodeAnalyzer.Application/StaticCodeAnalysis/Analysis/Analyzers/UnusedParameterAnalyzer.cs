using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;
public class UnusedParameterAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var methods = ast.Root.GetAllDescendantsOfType<MethodNode>();

        foreach (var method in methods)
        {
            if (method.Body is null)
                continue;

            var traverser = new UnusedParameterTraverser(projectRef.SemanticModel, method.Parameters.Parameters);
            traverser.Traverse(method.Body);

            var unused = traverser.GetUnusedParameters();

            Console.WriteLine($"{traverser.Parameters.Count} total parameters, {unused.Count} unused parameters");
        }

        return true;
    }
}

/**
 * TODO:
 * - Deal with cases where node is assigned to before usage (DiscardedBeforeUse)
 *   -> is control flow analysis required here? because if it's conditional that's fine
 *   -> to keep it simple a Parent check for if statements/loops/blocks could be enough as well?
 * - Ignore discarded params (_)
 * - Take (primary) base/this constructors into account
 * 
 */

public class UnusedParameterTraverser(SemanticModel semanticModel, List<ParameterNode> parameters) : AstTraverser
{
    public SemanticModel SemanticModel { get; set; } = semanticModel;
    public List<ParameterNode> Parameters { get; set; } = parameters;
    public HashSet<ParameterNode> UsedParameters { get; set; } = [];
    public HashSet<ParameterNode> DiscardedBeforeUse { get; set; } = [];

    protected override void Visit(AstNode node)
    {
        AstNode? possibleReference = null;

        switch (node)
        {
            case MemberAccessExpressionNode memberAccessExpressionNode:
                {
                    possibleReference = memberAccessExpressionNode.GetLeftMost();
                    break;
                }
            case IdentifierExpression identifierExpression:
                {
                    possibleReference = identifierExpression;
                    break;
                }
        }

        if (possibleReference is not null)
        {
            var symbol = SemanticModel.SymbolResolver.GetSymbolForNode(possibleReference);

            if (TryGetParameterFromSymbol(symbol, out var parameter))
            {
                if (!DiscardedBeforeUse.Contains(parameter))
                    UsedParameters.Add(parameter);
            }
        }

        if (node is not ParameterNode)
            base.Visit(node);
    }

    protected bool TryGetParameterFromSymbol(Symbol? symbol, [NotNullWhen(true)] out ParameterNode? parameter)
    {
        parameter = null;

        if (symbol is null)
            return false;

        if (symbol.Kind != SymbolKind.Parameter)
            return false;

        parameter = Parameters
            .FirstOrDefault(p => p == symbol.Node);

        return parameter is not null;
    }

    public List<ParameterNode> GetUnusedParameters()
    {
        return Parameters.Except(UsedParameters).ToList();
    }
}
