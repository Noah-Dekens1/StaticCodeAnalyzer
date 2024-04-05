using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;

public class CodeDisplayCLI
{
    public static void DisplayCode(string fileContent, AST ast, CodeLocation highlight)
    {
        var lines = fileContent.Split('\n');

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
    }
}
