using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Analyzers;
using InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;

namespace AnalyzerTests;

[TestClass]
public class PartialAssignmentTests
{
    [TestMethod]
    public void Analyze_RegularVariableDeclaration_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            var a = 0;
            """, new PartialVariableAssignmentAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_RegularMultiVariableDeclaration_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            int a = 0, b = 0;
            """, new PartialVariableAssignmentAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_VariableDeclarationPartialAssignment_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            int a, b = 0;
            """, new PartialVariableAssignmentAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }

    [TestMethod]
    public void Analyze_SingleDeclarator_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            int a;
            """, new PartialVariableAssignmentAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_MultipleDeclaratorOnly_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            int a, b;
            """, new PartialVariableAssignmentAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }
}
