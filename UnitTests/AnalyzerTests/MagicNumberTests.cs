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
public class MagicNumberTests
{
    [TestMethod]
    public void Analyze_MagicNumberInMethodCall_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            someMethod(30);
            """, new MagicNumberAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_NamedNumberInMethodCall_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            someMethod(count: 30);
            """, new MagicNumberAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }
}
