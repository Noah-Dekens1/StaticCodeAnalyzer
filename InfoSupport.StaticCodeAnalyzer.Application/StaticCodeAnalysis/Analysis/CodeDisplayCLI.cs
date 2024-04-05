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
    public static void DisplayCode(string fileContent, AST ast, List<CodeLocation> highlights)
    {
        var lines = fileContent.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            for (int j = 0; j < line.Length; j++)
            {
                var column = line[j];

                // line/column start at 1 (but indexed on 0)
                var isAnyHighlight = false;

                foreach (var highlight in highlights)
                {
                    var cLine = (ulong)i + 1;
                    var cColumn = (ulong)j + 1;

                    var start = highlight.Start;
                    var end = highlight.End;

                    var isHighlight = cLine >= start.Line && cLine <= end.Line;
                    isHighlight &= cLine != start.Line || cColumn >= start.Column;
                    isHighlight &= cLine != end.Line || cColumn <= end.Column;

                    isAnyHighlight |= isHighlight;
                }

                if (isAnyHighlight)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }

                Console.Write(column);

                if (isAnyHighlight)
                {
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
        }
    }
}
