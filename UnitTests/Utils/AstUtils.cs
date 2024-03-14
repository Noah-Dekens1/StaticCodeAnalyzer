using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

namespace InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;
internal static class AstUtils
{
    public static ExpressionNode ResolveMemberAccess(List<string> members)
    {
        var member = members[^1];
        var identifier = new IdentifierExpression(member);

        if (members.Count == 1)
            return identifier;

        members.Remove(member);

        return new MemberAccessExpressionNode(
            lhs: ResolveMemberAccess(members),
            identifier: identifier
        );
    }

    public static ExpressionNode ResolveMemberAccess(string members)
    {
        return ResolveMemberAccess(members.Split('.').ToList());
    }

    public static TypeNode SimpleNameAsType(string name)
    {
        return new TypeNode(baseType: new IdentifierExpression(name));
    }
}
