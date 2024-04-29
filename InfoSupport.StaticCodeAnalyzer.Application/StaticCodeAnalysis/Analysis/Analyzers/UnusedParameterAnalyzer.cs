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
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis.FlowAnalysis.ControlFlow;
using InfoSupport.StaticCodeAnalyzer.Domain;

using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;
public class UnusedParameterAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        var methods = ast.Root.GetAllDescendantsImplementing<IMethod>();
        var config = GetConfig<UnusedParametersConfig>();

        foreach (var method in methods)
        {
            if (method.Body is null)
                continue;

            bool isParameterForced = method.Modifiers.Contains(OptionalModifier.Override);
            isParameterForced |= IsRequiredByInterface(projectRef, method, config);

            if (!isParameterForced)
                ProcessFunction(projectRef, issues, method.Body, method.Parameters.Parameters);
        }

        return true;
    }

    private static bool IsRequiredByInterface(ProjectRef project, IMethod method, UnusedParametersConfig config)
    {
        var basicTypeDecl = ((AstNode)method).GetFirstParent<BasicDeclarationNode>();
        
        if (basicTypeDecl is null)
            return false;

        var interfaces = basicTypeDecl.GetInterfaces(project);

        foreach (var decl in interfaces)
        {
            var methods = decl.Members.OfType<IMethod>();
            var typeName = decl.GetName();

            foreach (var methodDefinition in methods)
            {
                if (methodDefinition.Name.IsNameEqual(method.Name))
                    return true;
            }
        }

        foreach (var parentName in basicTypeDecl.ParentNames)
        {
            foreach (var type in config.IgnoreWhenImplementingTypes)
            {
                if (type.Equals(parentName.AsLongIdentifier(), StringComparison.CurrentCultureIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static void ProcessFunction(ProjectRef projectRef, List<Issue> issues, AstNode body, List<ParameterNode> parameters)
    {
        ControlFlowGraph? cfg = null;

        // Review: C# allows you to put the typed checked variable directly in the pattern
        // You won't need the extra cast afterwards
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#declaration-and-type-patterns
        if (body is IStatementList statementList)
            projectRef.SemanticModel.AnalyzeControlFlow(statementList, out cfg);

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

    public override AnalyzerConfig GetConfig()
        => AnalyzersListConfig.UnusedParameters;
}

/**
 * TODO:
 * - Deal with cases where node is assigned to before usage (DiscardedBeforeUse)
 *   -> is control flow analysis required here? because if it's conditional that's fine (done)
 *   -> to keep it simple a Parent check for if statements/loops/blocks could be enough as well?
 * - Ignore discarded params (_) (done)
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
                    var rhs = assignmentExpressionNode.RHS;

                    if (lhs is not MemberAccessExpressionNode)
                    {
                        var symbol = SemanticModel.SymbolResolver.GetSymbolForNode(lhs);

                        if (TryGetParameterFromSymbol(symbol, out var parameter))
                        {
                            // a = a may be redundant but avoid cases like a = ProcessData(a);
                            bool isSelfAssignment = rhs is IdentifierExpression && lhs.AsLongIdentifier() == rhs.AsLongIdentifier();
                            bool selfIsPartOfExpression = !isSelfAssignment && rhs
                                .GetAllDescendantsOfType<IdentifierExpression>()
                                .Any(i => i.Identifier ==  parameter.Identifier);

                            // if already used, that's fine
                            if (!UsedParameters.Contains(parameter) && !selfIsPartOfExpression)
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
                // If we have a CFG (we may not in case of an expression body for example)
                // Make sure that the code is reachable
                if (ControlFlowGraph is null || SemanticModel.IsReachable(node, ControlFlowGraph))
                {
                    if (!DiscardedBeforeUse.Contains(parameter))
                        UsedParameters.Add(parameter);
                }
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
