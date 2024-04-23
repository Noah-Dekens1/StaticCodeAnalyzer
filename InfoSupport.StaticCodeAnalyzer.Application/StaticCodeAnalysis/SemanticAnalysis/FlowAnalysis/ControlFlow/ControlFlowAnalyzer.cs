using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis.FlowAnalysis.ControlFlow;

public class ControlFlowTraverser : AstTraverser
{
    private readonly List<StatementNode> _statements = [];
    private readonly List<ControlFlowNode> _blocks = [];
    private ControlFlowNode? _currentBasicBlock = null;

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

    private ControlFlowNode NewBasicBlock(ControlFlowNode? predecessor = null)
    {
        if (_currentBasicBlock is not null)
        {
            _currentBasicBlock.Instructions.AddRange(_statements);
            _statements.Clear();
        }

        var block = new ControlFlowNode();
        predecessor ??= _currentBasicBlock;

        if (predecessor is not null)
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
        bool handled = false;

        if (node is StatementNode statement && node is not BlockNode) // try to avoid statements in statements because it'll be duplicated
        {
            switch (node)
            {
                case IIterationNode:
                    {
                        //CreateBasicBlock();
                        break;
                    }

                case IfStatementNode ifStatement:
                    {
                        var predecessorBlock = _currentBasicBlock!;

                        predecessorBlock.Condition = ifStatement.Expression;

                        var trueBranch = NewBasicBlock(predecessorBlock);
                        Visit(ifStatement.Body);

                        var endOfTrueBranch = _currentBasicBlock!;

                        ControlFlowNode? falseBranch = null;

                        if (ifStatement.ElseBody is not null)
                        {
                            falseBranch = NewBasicBlock(predecessorBlock);
                            Visit(ifStatement.ElseBody);
                        }

                        var mergeBlock = NewBasicBlock(trueBranch);
                        mergeBlock.IsMergeNode = true;
                        
                        predecessorBlock.Successors.Add(trueBranch);
                        //trueBranch.Successors.Add(mergeBlock);

                        if (falseBranch is not null)
                        {
                            predecessorBlock.Successors.Add(falseBranch);
                            //falseBranch.Successors.Add(mergeBlock);
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
            CreateBasicBlock();
    }
}

public class ControlFlowAnalyzer
{
    private void ProcessBasicBlock(IStatementList block)
    {
        // How do we best walk the syntax tree here?
        
    }

    public static bool AnalyzeControlFlow(IStatementList block, [NotNullWhen(true)] out ControlFlowGraph? cfg)
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
}
