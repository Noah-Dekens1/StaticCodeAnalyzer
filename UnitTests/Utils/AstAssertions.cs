using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

public class AstAssertions
{
    public required AST AST { get; init; }
    private AstNode? _activeNode = null;

    public static AstAssertions AssertThat(AST ast)
    {
        return new AstAssertions { AST = ast };
    }

    public AstAssertions IsValidTree()
    {
        if (AST is null || AST.Root is null)
            throw new AssertFailedException("AST is invalid");

        return this;
    }

    public AstAssertions GetGlobalStatement<T>(AST ast, int index = 0) where T : StatementNode
    {
        _activeNode = ast.Root.GlobalStatements[index].Statement;

        if (_activeNode is null)
            throw new AssertFailedException("Global statement not found");

        if (_activeNode.GetType() != typeof(T))
            throw new AssertFailedException($"Statement was of type {_activeNode.GetType()} " +
                $"while {typeof(T)} was expected");

        return this;
    }

    public AstAssertions GetChild<T>(Func<T, AstNode> func) where T : AstNode
    {
        _activeNode = func((T)_activeNode!);
        return this;
    }

    public AstAssertions GetExpression()
    {
        _activeNode = ((ExpressionStatementNode)_activeNode!).Expression;
        return this;
    }

    public AstAssertions Validate<T>(Func<T, bool> validationFunc) where T : AstNode
    {
        if (typeof(T) != _activeNode!.GetType())
            throw new AssertFailedException($"Failed to validate node, was of wrong type, " +
                $"expecting {typeof(T)} got {_activeNode.GetType()}");

        if (!validationFunc((T)_activeNode))
            throw new AssertFailedException($"Validation failed");

        return this;
    }
}
