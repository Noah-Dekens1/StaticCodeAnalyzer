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
public class TestAssertionTests
{
    [TestMethod]
    public void Analyze_NoTestAssertion_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var a = 0;
                }
            }
            """, new TestAssertionAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_TestAssertion_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var a = 0;
                    Assert.AreEqual(0, a);
                }
            }
            """, new TestAssertionAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_RegularMethod_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            [TestClass]
            public class TestClass
            {
                public void TestMethod()
                {
                    var a = 0;
                }
            }
            """, new TestAssertionAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_MethodCallsAssertionMethod_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            [TestClass]
            public class TestClass
            {
                private void Validate(int a)
                {
                    Assert.AreEqual(0, a);
                }

                [TestMethod]
                public void TestMethod()
                {
                    var a = 0;
                    Validate(a);
                }
            }
            """, new TestAssertionAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }
}
