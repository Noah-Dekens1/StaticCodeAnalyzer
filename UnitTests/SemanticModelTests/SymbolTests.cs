using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InfoSupport.StaticCodeAnalyzer.UnitTests.SemanticModelTests;

[TestClass]
public class SymbolTests
{
    [TestMethod]
    public void Resolve_LocalVariable_ReturnsValidSymbol()
    {
        var tokens = Lexer.Lex("""
            var a = 0;
            """);

        
    }
}
