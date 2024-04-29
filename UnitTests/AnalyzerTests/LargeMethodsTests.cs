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
public class LargeMethodsTests
{
    [TestMethod]
    public void Analyze_RegularMethod_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Example()
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
            }
            """, new LargeMethodAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_LargeMethod_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            void Example()
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
            }
            """, new LargeMethodAnalyzer());

        // Review: Always verify that the test is actually testing what it should test.
        // In this case a single issue should arise of 'type' method-too-large.
        // It might be nice to also test the edge cases, in case of the LargeMethodAnalyzer create:
        // - empty method with 0 Console.WriteLine();
        // - method with 1 Console.WriteLine();
        // - method with max - 1 Console.WriteLine();
        // - method with max Console.WriteLine();
        // - method with max + 1 Console.WriteLine();
        // - method with not only Console.WriteLine but also
        // With mstest you can use the DataRow, DynamicData of DataSource attribute to create a single test but with different input.
        Assert.AreEqual(1, issues.Count);
    }
}
