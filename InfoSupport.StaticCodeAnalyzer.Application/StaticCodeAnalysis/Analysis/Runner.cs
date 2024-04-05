﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
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

        foreach (var path in paths)
        {
            var file = File.ReadAllText(path);
            var projectFile = new ProjectFile(Path.GetFileName(path), path);
            var tokens = Lexer.Lex(file);
            var ast = Parser.Parse(tokens);

            foreach (var analyzer in Analyzers)
            {
                var fileIssues = new List<Issue>();
                analyzer.Analyze(project, ast, fileIssues);
                projectFile.Issues.AddRange(fileIssues);
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
