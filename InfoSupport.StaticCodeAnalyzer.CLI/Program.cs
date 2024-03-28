// See https://aka.ms/new-console-template for more information
using System.Diagnostics;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing.Misc;


//var lexer = new Lexer(File.ReadAllText(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.Application\StaticCodeAnalysis\Parsing\Lexer.cs"));
var _ = Lexer.Lex(File.ReadAllText(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.CLI\InterpolationTest.cs"));

//return;

//var directory = @"C:\Users\NoahD\source\repos\Files\src";
var directory = @"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.Application\StaticCodeAnalysis\Parsing";

string[] paths = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);

var counter = 0;
var tokensLexed = 0;

using var timer = new ScopedTimer("Parser timer");

foreach (string path in paths)
{
    counter++;
    var file = File.ReadAllText(path);
    var tokens = Lexer.Lex(file);
    var ast = Parser.Parse(tokens);

    Console.WriteLine($"Successfully parsed {Path.GetFileName(path)} ({counter}/{paths.Length})");
    tokensLexed += tokens.Count;
}

Console.WriteLine($"Successfully parsed all {paths.Length} files in directory consisting of {tokensLexed} tokens!");