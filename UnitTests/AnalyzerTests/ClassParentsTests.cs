using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalyzerTests;

[TestClass]
public class ClassParentsTests
{
    [TestMethod]
    public void Analyze_TooManyClassParents_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            class A { }
            class B : A { }
            class C : B { }
            class D : C { }
            class E : D { }
            class F : E { }
            class G : F { }
            """, new ClassParentAnalyzer());

        Assert.AreEqual(1, issues.Count);
        Assert.IsTrue(issues.First().Code == "too-many-class-parents");
    }

    [TestMethod]
    public void Analyze_FewClassParents_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            class A { }
            class B : A { }
            class C : B { }
            """, new ClassParentAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }
}
