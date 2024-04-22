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
}
