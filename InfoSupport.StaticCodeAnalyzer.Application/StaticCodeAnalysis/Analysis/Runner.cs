using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.SemanticAnalysis;
using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;

public class Runner
{
    private static readonly Type AnalyzerType = typeof(Analyzer);
    private static List<Analyzer> Analyzers { get; } = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(s => s.GetTypes())
        .Where(AnalyzerType.IsAssignableFrom)
        .Where(p => !p.IsAbstract)
        .Select(s => (Analyzer)Activator.CreateInstance(s)!)
        .Cast<Analyzer>()
        .ToList();

    public static Report RunAnalysis(Project project)
    {
        var paths = GetFilesInProject(project);

        var projectFiles = new List<ProjectFile>();
        var projectRef = new ProjectRef();

        foreach (var path in paths)
        {
            var file = File.ReadAllText(path);
            var tokens = Lexer.Lex(file);
            var ast = Parser.Parse(tokens);

            projectRef.ProjectFiles.Add(path, ast);
            projectRef.SemanticModel.ProcessFile(ast);
        }

        projectRef.TypeLookup.GenerateTypeMappings(projectRef);
        projectRef.SemanticModel.ProcessFinished();

        foreach (var fileInfo in projectRef.ProjectFiles)
        {
            var path = fileInfo.Key;
            var projectFile = new ProjectFile(Path.GetFileName(path), path, null);

            foreach (var analyzer in Analyzers)
            {
                var fileIssues = new List<Issue>();
                analyzer.Analyze(project, fileInfo.Value, projectRef, fileIssues);
                projectFile.Issues.AddRange(fileIssues);
            }

            // If any issues present store in projectFile so it can get saved to the db
            if (projectFile.Issues.Count > 0)
            {
                projectFile.Content = File.ReadAllText(path);
            }

            projectFiles.Add(projectFile);
        }

        return new Report(project, projectFiles);
    }

    private static string[] GetFilesInProject(Project project)
    {
        return Directory.GetFiles(project.Path, "*.cs", SearchOption.AllDirectories);
    }
}
