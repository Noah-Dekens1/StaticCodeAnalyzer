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
public class IfElseTests
{
    [TestMethod]
    public void Analyze_FewElses_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            if (a)
            {
            }
            else if (b)
            {
            }
            else
            {
            }
            """, new IfElseAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_ManyElses_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            if (a)
            {
            }
            else if (b)
            {
            }
            else if (c)
            {
            }
            else if (d)
            {
            }
            else
            {
            }
            """, new IfElseAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }
}
