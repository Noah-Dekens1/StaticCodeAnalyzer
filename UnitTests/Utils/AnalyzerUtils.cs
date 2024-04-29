using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.UnitTests.Utils;
public class AnalyzerUtils
{
    public static List<Issue> Analyze(string content, Analyzer analyzer, AnalyzersListConfig? customConfig = null)
    {
        var tokens = Lexer.Lex(content);
        var ast = Parser.Parse(tokens);
        var result = new List<Issue>();
        var config = customConfig ?? new AnalyzersListConfig();
        var project = new Project(string.Empty, string.Empty);
        var projectRef = new ProjectRef();

        analyzer.AnalyzersListConfig = config;


        projectRef.SemanticModel.ProcessFile(ast);
        projectRef.SemanticModel.ProcessFinished();
        
        analyzer.Analyze(project, ast, projectRef, result);

        return result;
    }
}
