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

class A { }
class B : A { }
class C : B { }
class D : C { }
class E : D { }
class F : E { }
class G : F { }

public class ClassParentAnalyzer : Analyzer
{
    public override bool Analyze(Project project, AST ast, ProjectRef projectRef, List<Issue> issues)
    {
        const int maxParentsCount = 5;

        foreach (var classDecl in ast.GetClasses())
        {
            var current = classDecl;
            var count = 0;

            while (current is not null)
            {
                current = current.GetParentClass(projectRef);
                if (current is not null) count++;
            }

            if (count > maxParentsCount)
            {
                issues.Add(new Issue(
                    code: "too-many-class-parents",
                    description: "This class has too many parents",
                    location: classDecl.Location
                ));
            }
        }


        return true;
    }
}
