﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;

public class CodeDisplayCLI
{
    private static bool HighlightEnabled = false;

    private static void SetColor()
    {
        Console.Write("\u001b[33m");
    }

    private static void ResetColor()
    {
        Console.Write("\u001b[0m");
    }

    public static void DisplayCode(string fileContent, List<Issue> issues, string fileName, int contextLines=5)
    {
        var lines = fileContent.Split('\n');

        foreach (var issue in issues)
        {
            var highlight = issue.Location;
            var start = Math.Max(0, (int)highlight.Start.Line - contextLines - 1);
            var end = Math.Min(lines.Length - 1, (int)highlight.End.Line + contextLines - 1);

            Console.WriteLine("----------------------------------------------");
            Console.WriteLine($"File: {fileName} - Code: {issue.Code}");
            Console.WriteLine("----------------------------------------------");

            for (int i = start; i <= end; i++)
            {
                var line = lines[i];

                Console.Write((i + 1).ToString().PadLeft(4, ' ') + " ");

                for (int j = 0; j < line.Length; j++)
                {
                    var column = line[j];

                    var cLine = (ulong)i + 1;
                    var cColumn = (ulong)j + 1;

                    var isHighlight = cLine >= highlight.Start.Line && cLine <= highlight.End.Line;
                    isHighlight &= cLine != highlight.Start.Line || cColumn >= highlight.Start.Column;
                    isHighlight &= cLine != highlight.End.Line || cColumn <= highlight.End.Column;


                    if (isHighlight && !HighlightEnabled)
                    {
                        //Console.ForegroundColor = ConsoleColor.Yellow;
                        SetColor();
                        HighlightEnabled = true;
                    }

                    if (!isHighlight && HighlightEnabled)
                    {
                        ResetColor();
                        HighlightEnabled = false;
                    }

                    Console.Write(column);
                }

                Console.WriteLine();
            }

            Console.WriteLine("----------------------------------------------\n");
        }
    }

}
