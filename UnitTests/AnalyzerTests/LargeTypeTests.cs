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
public class LargeTypeTests
{
    [TestMethod]
    public void Analyze_RegularClass_ReturnsNoIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            class Example
            {
                private int _field0;
                private int _field1;
                private int _field2;
                private int _field3;
                private int _field4;
                private int _field5;
                private int _field6;
                private int _field7;
                private int _field8;
                private int _field9;
                private int _field10;
            }
            """, new LargeTypeAnalyzer());

        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void Analyze_LargeClass_ReturnsIssue()
    {
        var issues = AnalyzerUtils.Analyze("""
            class Example
            {
                private int _field0;
                private int _field1;
                private int _field2;
                private int _field3;
                private int _field4;
                private int _field5;
                private int _field6;
                private int _field7;
                private int _field8;
                private int _field9;
                private int _field10;

                public void Method0() {}
                public void Method1() {}
                public void Method2() {}
                public void Method3() {}
                public void Method4() {}
                public void Method5() {}
                public void Method6() {}
                public void Method7() {}
                public void Method8() {}
                public void Method9() {}
                public void Method10() {}
                public void Method11() {}
                public void Method12() {}
                public void Method13() {}
                public void Method14() {}
                public void Method15() {}

                public string Property0 { get; set; }
                public string Property1 { get; set; }
                public string Property2 { get; set; }
                public string Property3 { get; set; }
                public string Property4 { get; set; }
                public string Property5 { get; set; }
                public string Property6 { get; set; }
                public string Property7 { get; set; }
                public string Property8 { get; set; }
                public string Property9 { get; set; }
                public string Property10 { get; set; }
            }
            """, new LargeTypeAnalyzer());

        Assert.AreEqual(1, issues.Count);
    }
}
