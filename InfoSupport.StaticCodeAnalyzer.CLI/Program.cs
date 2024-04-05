// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Linq.Expressions;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Extensions;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing.Misc;
using InfoSupport.StaticCodeAnalyzer.Domain;


//var lexer = new Lexer(File.ReadAllText(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.Application\StaticCodeAnalysis\Parsing\Lexer.cs"));
var _ = Lexer.Lex(File.ReadAllText(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.CLI\InterpolationTest.cs"));

//return;

//var directory = @"C:\Users\NoahD\source\repos\Files\src";
var directory = @"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer";
//var directory = @"C:\Users\NoahD\source\repos\NestedStringsTesting\NestedStringsTesting";
//var directory = @"C:\Users\NoahD\source\repos\RoslynTest\RoslynTest";
//var directory = @"C:\Users\NoahD\source\repos\TestWebApp\TestWebApp";
//var directory = @"C:\Users\NoahD\source\repos\UAssetAPI";

var report = Runner.RunAnalysis(new Project("Example", directory));

var nestedTernary = true ? true : true ? false : false;
var normalTernary = true ? true : false;

foreach (var projectFile in report.ProjectFiles)
{
    var issues = projectFile.Issues;

    if (issues.Count > 0)
    {
        Console.WriteLine($"{projectFile.Name} ----");
        CodeDisplayCLI.DisplayCode(File.ReadAllText(projectFile.Path), issues.Select(i => i.Location).ToList());
    }
}

/*
var testFilePath = @"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\UnitTests\LexerTests.cs";
var testFile = File.ReadAllText(testFilePath);
var tokens = Lexer.Lex(testFile);
var ast = Parser.Parse(tokens);
CodeDisplayCLI.DisplayCode(
    testFile, 
    ast.Root
    .GetAllDescendantsOfType<MethodNode>()
    .Where(m => 
        m.HasAttribute("DataTestMethod")
    )
    .Select(s => s.Location)
    .Concat(
        ast.Root.GetAllDescendantsOfType<ObjectCreationExpressionNode>()
        .Select(s => s.Location)
    )
    .ToList()
    
    );
*/
/*

string[] paths = Directory
    .GetFiles(directory, "*.cs", SearchOption.AllDirectories);
    //.Where(f => Path.GetFileName(f) != "LexerTests.cs")
    //.ToArray();

var counter = 0;
var tokensLexed = 0;

using var timer = new ScopedTimer("Parser timer");

foreach (string path in paths)
{
    counter++;
    Console.WriteLine($"Parsing {Path.GetFileName(path)} ({counter}/{paths.Length})");
    Console.WriteLine(path);
    var file = File.ReadAllText(path);
    var tokens = Lexer.Lex(file);
    var ast = Parser.Parse(tokens);

    Console.WriteLine($"Successfully parsed {Path.GetFileName(path)} ({counter}/{paths.Length})");
    tokensLexed += tokens.Count;
}

Console.WriteLine($"Successfully parsed all {paths.Length} files in directory consisting of {tokensLexed} tokens!");
*/