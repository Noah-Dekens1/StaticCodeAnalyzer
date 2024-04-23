using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis.FlowAnalysis.ControlFlow;

[DebuggerDisplay("{ToString(),nq}")]
public class ControlFlowNode
{
    public Guid Id { get; } = Guid.NewGuid();
    public List<StatementNode> Instructions { get; set; } = [];

    public HashSet<ControlFlowNode> Predecessors { get; set; } = [];
    public HashSet<ControlFlowNode> Successors { get; set; } = [];
    public bool IsMergeNode { get; set; } = false;
    public ExpressionNode? Condition { get; set; } = null;

    [ExcludeFromCodeCoverage]
    public override string ToString() => !IsMergeNode
        ? $"{(Condition is not null ? $"[{Condition}] " : "")}Node: {Instructions.FirstOrDefault()?.ToString() ?? "Empty"}"
        : $"Merge node {Id}";
}

public class ControlFlowGraph(AstNode node)
{
    public AstNode Node { get; set; } = node;
    public List<ControlFlowNode> Nodes { get; set; } = [];
}
