// See https://aka.ms/new-console-template for more information
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;

//var lexer = new Lexer(File.ReadAllText(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.Application\StaticCodeAnalysis\Parsing\Lexer.cs"));
var lexer = new Lexer(File.ReadAllText(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.CLI\InterpolationTest.cs"));
lexer.Lex();

return;

var directory = @"C:\Users\NoahD\source\repos\Files\src";

string[] paths = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);

var counter = 0;
var tokensLexed = 0;

foreach (string path in paths)
{
    counter++;
    var file = File.ReadAllText(path);
    var fileLexer = new Lexer(file);
    fileLexer.Lex();

    Console.WriteLine($"Successfully lexed {Path.GetFileName(path)} ({counter}/{paths.Length})");
    tokensLexed += fileLexer.GetTokens().Count;
}

Console.WriteLine($"Successfully lexed all {paths.Length} files in directory consisting of {tokensLexed} tokens!");