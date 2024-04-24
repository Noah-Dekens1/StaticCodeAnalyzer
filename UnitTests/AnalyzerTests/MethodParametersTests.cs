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
public class MethodParametersTests
{
    [TestMethod]
    public void Analyze_TooManyMethodParameters_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a, int b, string c, string d, bool e, bool f, char g, char h)
            {

            };
            """, new MethodParameterCountAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_FewMethodParameters_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Test(int a, int b, string c, string d)
            {

            };
            """, new MethodParameterCountAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }
}
