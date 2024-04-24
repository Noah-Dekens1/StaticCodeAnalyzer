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
public class NestedTernaryTests
{
    [TestMethod]
    public void Analyze_NestedTernary_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            var a = a ? b : c ? d : e;
            """, new NestedTernaryAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_RegularTernary_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            var a = a ? b : c;
            """, new NestedTernaryAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }
}
