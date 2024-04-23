using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;
using InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalyzerTests;

[TestClass]
public class UnusedParameterTests
{
    [TestMethod]
    public void Analyze_UsedParameter_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a)
            {
                Console.WriteLine(a);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_SimpleUnusedParameter_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a)
            {
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual(issues[0].Code, "unused-parameter");
    }

    [TestMethod]
    public void Analyze_SimpleMixedParameters_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a, int b)
            {
                Console.WriteLine(b);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual(issues[0].Code, "unused-parameter");
    }

    [TestMethod]
    public void Analyze_MultipleUnusedParameters_ReturnsMultipleIssues()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a, int b)
            {
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(2, issues.Count);
        Assert.AreEqual(issues[0].Code, "unused-parameter");
        Assert.AreEqual(issues[1].Code, "unused-parameter");
    }

    [TestMethod]
    public void Analyze_UnusedParametersButUsedIdentifier_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a)
            {
                Utils.a.Method();
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_UsedParameterByMemberAccess_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(Person person)
            {
                Console.WriteLine(person.Name);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_DiscardedParameterBeforeUse_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(Person person)
            {
                person = new Person();
                Console.WriteLine(person.Name);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_DiscardedParameterBeforeUseConditionally_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(Person person)
            {
                if (3 == 4)
                {
                    person = new Person();
                }

                Console.WriteLine(person.Name);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_DiscardedParameterBeforeUseConditionallyNoScope_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(Person person)
            {
                if (3 == 4)
                    person = new Person();

                Console.WriteLine(person.Name);
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_UsedParameterInMemberAccess_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            async Task<Report?> GetReportById(Guid id)
            {
                return await _context.Reports
                    .Where(r => r.Id == id)
                    .Include(r => r.ProjectFiles)
                    .ThenInclude(f => f.Issues)
                    .SingleOrDefaultAsync();
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_UsedParameterInLeftmostMemberAccess_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            T EmitStatic<T>(T node, CodeLocation location) where T : AstNode
                {
                    node.Location = location;
            #if DEBUG
                    node.ConstructedInEmit = true;
            #endif
                    return node;
                }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_ForcedParametersFromInterface_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            public interface IExample
            {
                public void Method(bool unusedValue);
            }
            
            public class Example : IExample
            {
                public void Method(bool unusedValue)
                {
                }
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_AssignWithSelfOnLhsAndRhs_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Example(int a, int b)
            {
                a = a;              // unused param
                b = SomeMethod(b);  // used param
            }
            """, new UnusedParameterAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }
}
