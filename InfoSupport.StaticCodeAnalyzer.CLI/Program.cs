// See https://aka.ms/new-console-template for more information
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing.Misc;

//var lexer = new Lexer(File.ReadAllText(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.Application\StaticCodeAnalysis\Parsing\Lexer.cs"));
var _ = Lexer.Lex(File.ReadAllText(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.CLI\InterpolationTest.cs"));

//return;

var directory = @"C:\Users\NoahD\source\repos\Files\src";

string[] paths = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);

var counter = 0;
var tokensLexed = 0;

using var timer = new ScopedTimer("Lexer timer");

foreach (string path in paths)
{
    counter++;
    var file = File.ReadAllText(path);
    var tokens = Lexer.Lex(file);

    Console.WriteLine($"Successfully lexed {Path.GetFileName(path)} ({counter}/{paths.Length})");
    tokensLexed += tokens.Count;
}

Console.WriteLine($"Successfully lexed all {paths.Length} files in directory consisting of {tokensLexed} tokens!");