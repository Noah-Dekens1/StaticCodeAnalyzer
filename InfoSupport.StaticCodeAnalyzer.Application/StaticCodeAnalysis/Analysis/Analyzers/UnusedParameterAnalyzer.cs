﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis.FlowAnalysis.ControlFlow;
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

            ProcessFunction(projectRef, issues, method.Body, method.Parameters.Parameters);
        }

        // @todo: maybe add IFunction interface for methods & local function declarations to avoid repeated code here?
        var localFunctions = ast.Root.GetAllDescendantsOfType<LocalFunctionDeclarationNode>();

        foreach (var localFunction in localFunctions)
        {
            if (localFunction.Body is null)
                continue;

            ProcessFunction(projectRef, issues, localFunction.Body, localFunction.Parameters.Parameters);
        }

        return true;
    }

    private static void ProcessFunction(ProjectRef projectRef, List<Issue> issues, AstNode body, List<ParameterNode> parameters)
    {
        ControlFlowGraph? cfg = null;

        if (body is IStatementList)
            projectRef.SemanticModel.AnalyzeControlFlow((IStatementList)body, out cfg);

        var traverser = new UnusedParameterTraverser(projectRef.SemanticModel, parameters, cfg);
        traverser.Traverse(body);

        var unused = traverser.GetUnusedParameters();

        foreach (var unusedParameter in unused)
        {
            issues.Add(
                new Issue(
                    "unused-parameter",
                    "This parameter isn't used, remove it or use it in the function.",
                    unusedParameter.Location
                )
            );
        }
    }
}

/**
 * TODO:
 * - Deal with cases where node is assigned to before usage (DiscardedBeforeUse)
 *   -> is control flow analysis required here? because if it's conditional that's fine
 *   -> to keep it simple a Parent check for if statements/loops/blocks could be enough as well?
 * - Ignore discarded params (_)
 * - Take (primary) base/this constructors into account
 * - What about parameters that are forced from interfaces/parent classes?
 *    -> could check for override but may be more clean to just take it from the parent class instead
 *    -> especially since that wouldn't work for interfaces
 */

public class UnusedParameterTraverser(SemanticModel semanticModel, List<ParameterNode> parameters, ControlFlowGraph? cfg) : AstTraverser
{
    public SemanticModel SemanticModel { get; set; } = semanticModel;
    public List<ParameterNode> Parameters { get; set; } = parameters;
    public HashSet<ParameterNode> UsedParameters { get; set; } = [];
    public HashSet<ParameterNode> DiscardedBeforeUse { get; set; } = [];
    private ControlFlowGraph? ControlFlowGraph { get; set; } = cfg;

    protected override void Visit(AstNode node)
    {
        AstNode? possibleReference = null;
        bool skipChildren = false;

        var parentIsMemberAccess = node.Parent is MemberAccessExpressionNode;

        switch (node)
        {
            case AssignmentExpressionNode assignmentExpressionNode:
                {
                    var lhs = assignmentExpressionNode.LHS;

                    if (lhs is not MemberAccessExpressionNode)
                    {
                        var symbol = SemanticModel.SymbolResolver.GetSymbolForNode(lhs);

                        if (TryGetParameterFromSymbol(symbol, out var parameter))
                        {
                            // if already used, that's fine
                            if (!UsedParameters.Contains(parameter))
                            {
                                // if not, use the semantic model to determine whether we're unconditionally reachable
                                if (ControlFlowGraph is not null && SemanticModel.IsUnconditionallyReachable(assignmentExpressionNode, ControlFlowGraph))
                                {
                                    DiscardedBeforeUse.Add(parameter);
                                }
                            }
                        }
                    }

                    break;
                }

            case MemberAccessExpressionNode memberAccessExpressionNode when !parentIsMemberAccess:
                {
                    possibleReference = memberAccessExpressionNode.GetLeftMost();
                    break;
                }

            case IdentifierExpression identifierExpression when !parentIsMemberAccess:
                {
                    possibleReference = identifierExpression;
                    break;
                }

            case ParameterNode:
                {
                    skipChildren = true;
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

        if (!skipChildren)
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
        return Parameters
            .Except(UsedParameters)
            .Where(p => p.ParameterType != ParameterType.Out) // the compiler will force the out param to be used anyways
            .Where(p => p.Identifier != "_")                  // ignore discards
            .ToList();
    }
}
