using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;
using InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InfoSupport.StaticCodeAnalyzer.UnitTests.AnalyzerTests;

[TestClass]
public class SwitchCasesTests
{
    [TestMethod]
    public void Analyze_SwitchWithFewCases_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            switch (a)
            {
                case a:
                case b:
                case c:
                    break;
            }
            """, new SwitchCasesAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_SwitchWithManyCases_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            switch (a)
            {
                case a:
                case b:
                case c:
                case d:
                case e:
                case f:
                case g:
                case h:
                case i:
                case j:
                case k:
                case l:
                case m:
                case n:
                    break;
            }
            """, new SwitchCasesAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }
}
