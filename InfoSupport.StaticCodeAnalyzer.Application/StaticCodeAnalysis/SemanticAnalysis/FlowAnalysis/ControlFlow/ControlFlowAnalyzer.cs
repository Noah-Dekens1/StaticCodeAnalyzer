using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis.FlowAnalysis.ControlFlow;

public class ControlFlowTraverser : AstTraverser
{
    private readonly List<StatementNode> _statements = [];
    private readonly List<ControlFlowNode> _blocks = [];
    private ControlFlowNode? _currentBasicBlock = null;

    // Can be used for continue;
    private readonly Stack<List<ControlFlowNode>> _continueFlows = [];
    // Can be used for break;
    private readonly Stack<List<ControlFlowNode>> _breakFlows = [];
    // For return; just don't add any successor nodes (maybe we'll need to clear _currentBasicBlock or start a new one)

    public ControlFlowTraverser()
    {
        _currentBasicBlock = NewBasicBlock();
    }

    public ControlFlowNode? GetStartNode()
    {
        return _blocks.FirstOrDefault();
    }

    public List<ControlFlowNode> GetNodes()
    {
        return _blocks;
    }

    private ControlFlowNode CreateBasicBlock(ControlFlowNode? predecessor = null)
    {
        var block = new ControlFlowNode();
        block.Instructions.AddRange(_statements);
        predecessor ??= _currentBasicBlock;

        if (predecessor is not null)
        {
            block.Predecessors.Add(predecessor ?? _currentBasicBlock!);
            predecessor!.Successors.Add(block);
        }
        _currentBasicBlock = block;
        _blocks.Add(block);
        _statements.Clear();
        return block;
    }

    private ControlFlowNode NewBasicBlock(ControlFlowNode? predecessor = null, bool createDetached = false)
    {
        if (_currentBasicBlock is not null && !createDetached)
        {
            _currentBasicBlock.Instructions.AddRange(_statements);
            _statements.Clear();
        }

        var block = new ControlFlowNode();
        predecessor ??= _currentBasicBlock;

        if (predecessor is not null && !createDetached)
        {
            block.Predecessors.Add(predecessor ?? _currentBasicBlock!);
            predecessor!.Successors.Add(block);
        }

        _currentBasicBlock = block;
        _blocks.Add(block);

        return block;
    }

    protected override void Visit(AstNode node)
    {
        node.ControlFlowNodeRef = _currentBasicBlock;
        bool handled = false;

        if (node is StatementNode statement && node is not BlockNode) // try to avoid statements in statements because it'll be duplicated
        {
            switch (node)
            {
                case IIterationNode iterator:
                    {
                        var predecessorBlock = _currentBasicBlock!;
                        
                        predecessorBlock.EndOfBlockCondition = iterator.Condition;

                        _breakFlows.Push([]);
                        _continueFlows.Push([]);

                        var bodyBlock = NewBasicBlock(predecessorBlock);
                        bodyBlock.IsConditional = true;
                        Visit(iterator.Body!);

                        var bodyEnd = _currentBasicBlock!;

                        var next = NewBasicBlock();

                        // create the loop
                        bodyEnd.Successors.Add(bodyBlock);
                        bodyBlock.Predecessors.Add(bodyEnd);

                        var breakFlows = _breakFlows.Pop();
                        var continueFlows = _continueFlows.Pop();

                        foreach (var flow in breakFlows)
                        {
                            // gp to end of loop
                            flow.Successors.Add(next);
                        }

                        foreach (var flow in continueFlows)
                        {
                            // go to start of loop
                            flow.Successors.Add(bodyBlock);
                        }

                        handled = true;

                        break;
                    }

                case IfStatementNode ifStatement:
                    {
                        var predecessorBlock = _currentBasicBlock!;

                        predecessorBlock.EndOfBlockCondition = ifStatement.Expression;

                        var trueBranch = NewBasicBlock(predecessorBlock);
                        trueBranch.IsConditional = true;
                        Visit(ifStatement.Body);

                        var endOfTrueBranch = _currentBasicBlock!;

                        ControlFlowNode? falseBranch = null;

                        if (ifStatement.ElseBody is not null)
                        {
                            falseBranch = NewBasicBlock(predecessorBlock);
                            falseBranch.IsConditional = true;
                            Visit(ifStatement.ElseBody);
                        }

                        var mergeBlock = NewBasicBlock(trueBranch);
                        mergeBlock.IsMergeNode = true;
                        
                        predecessorBlock.Successors.Add(trueBranch);

                        if (falseBranch is not null)
                        {
                            predecessorBlock.Successors.Add(falseBranch);
                        }
                        else
                        {
                            predecessorBlock.Successors.Add(mergeBlock);
                            mergeBlock.Predecessors.Add(predecessorBlock);
                        }

                        // if we have a false branch then the last node won't be from the true branch
                        // (the last node should always be in the scope we want due to the merge blocks)
                        if (falseBranch is not null)
                        {
                            mergeBlock.Predecessors.Add(endOfTrueBranch);
                            endOfTrueBranch.Successors.Add(mergeBlock);
                            // no need to add end of false branch since it's captured automatically
                        }

                        // we called visit so don't want to do that again
                        handled = true;

                        break;
                    }

                case BreakStatementNode breakStatement:
                    {
                        var closestParent = breakStatement.GetFirstParent(n => n is SwitchStatementNode or IIterationNode);

                        // Ignore switch statements for now
                        if (closestParent is IIterationNode)
                        {
                            var predecessor = _currentBasicBlock!;
                            var breakBlock = NewBasicBlock(createDetached: true);
                            breakBlock.Predecessors.Add(predecessor);
                            _breakFlows.Peek().Add(breakBlock);
                        }
                        break;
                    }

                case ContinueStatementNode continueStatement:
                    {
                        var predecessor = _currentBasicBlock!;
                        var continueBlock = NewBasicBlock(createDetached: true);
                        continueBlock.Predecessors.Add(predecessor);
                        _continueFlows.Peek().Add(continueBlock);
                        break;
                    }

                case ReturnStatementNode returnStatement:
                    {
                        var predecessor = _currentBasicBlock!;
                        var returnBlock = NewBasicBlock(createDetached: true);
                        returnBlock.Predecessors.Add(predecessor);
                        returnBlock.IsExitPoint = true;
                        // @note: do we want to link this up somewhere?
                        break;
                    }

                default:
                    {
                        _statements.Add(statement);
                        break;
                    }
            }

        }


        if (!handled)
        {
            base.Visit(node);
        }
    }

    public void Flush()
    {
        if (_statements.Count > 0)
        {
            if (_currentBasicBlock is not null)
            {
                _currentBasicBlock.Instructions.AddRange(_statements);
                _statements.Clear();
            }
            else
            {
                CreateBasicBlock();
            }
        }
    }
}

public static class ControlFlowAnalyzer
{
    public static bool AnalyzeControlFlow(this SemanticModel _, IStatementList block, [NotNullWhen(true)] out ControlFlowGraph? cfg)
    {
        var traverser = new ControlFlowTraverser();
        traverser.Traverse((AstNode)block);
        traverser.Flush();

        cfg = new ControlFlowGraph((AstNode)block)
        {
            Nodes = traverser.GetNodes()
        };

        return true;
    }

    public static HashSet<ControlFlowNode> ComputeReachability(this SemanticModel _, ControlFlowNode entryNode)
    {
        var visited = new HashSet<ControlFlowNode>();
        var queue = new Queue<ControlFlowNode>();

        queue.Enqueue(entryNode);
        visited.Add(entryNode);

        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();

            foreach (var successor in currentNode.Successors)
            {
                if (!visited.Contains(successor))
                    queue.Enqueue(successor);

                visited.Add(successor);
            }
        }

        return visited;
    }

    public static HashSet<ControlFlowNode> ComputeReachability(this SemanticModel model, ControlFlowGraph cfg)
    {
        return cfg.Nodes.Count > 0 ? model.ComputeReachability(cfg.Nodes.First()) : [];
    }

    public static bool IsReachable(this SemanticModel model, AstNode node, ControlFlowGraph cfg)
    {
        if (node.ControlFlowNodeRef is null)
            return false;

        var reachable = model.ComputeReachability(cfg);
        return reachable.Contains(node.ControlFlowNodeRef);
    }

    public static bool IsUnconditionallyReachable(this SemanticModel _, AstNode targetNode, ControlFlowGraph cfg)
    {
        if (targetNode.ControlFlowNodeRef is null)
            return false;

        ControlFlowNode? startNode = cfg.Nodes.FirstOrDefault();

        if (startNode is null)
            return false;

        if (targetNode.ControlFlowNodeRef == startNode)
            return true;

        var queue = new Queue<ControlFlowNode>();
        var visited = new HashSet<ControlFlowNode>();

        queue.Enqueue(startNode);
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();

            foreach (var successor in currentNode.Successors)
            {
                if (!visited.Contains(successor) && !successor.IsConditional)
                {
                    if (successor == targetNode.ControlFlowNodeRef)
                        return true;

                    visited.Add(successor);
                    queue.Enqueue(successor);
                }
            }
        }

        return false;
    }

}
