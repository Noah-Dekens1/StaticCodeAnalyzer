﻿#define HANDLE_EXCEPTIONS_IN_DEBUG

using System.IO.Abstractions;
using System.Text.Json;

using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis.Utils;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Parsing;
using InfoSupport.StaticCodeAnalyzer.Domain;
using DotNet.Globbing;

namespace InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;

public class Runner(IFileSystem fileSystem)
{
    private static readonly Type AnalyzerType = typeof(Analyzer);
    private static readonly List<Analyzer> Analyzers = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(s => s.GetTypes())
        .Where(AnalyzerType.IsAssignableFrom)
        .Where(p => !p.IsAbstract)
        .Select(s => (Analyzer)Activator.CreateInstance(s)!)
        .ToList();

    private readonly IFileSystem _fileSystem = fileSystem;

    public Runner() : this(new FileSystem()) { }

    public static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    private static string NormalizePath(string path)
    {
        return path.Replace("\\", "/");
    }

    public Report? RunAnalysis(Project project, CancellationToken cancellationToken)
    {
        var paths = GetFilesInProject(project);

        var projectFiles = new List<ProjectFile>();
        var projectRef = new ProjectRef();

        var configPath = _fileSystem.Path.Join(project.Path, "analyzer-config.json");

        Configuration? config = null;

        cancellationToken.ThrowIfCancellationRequested();

        if (_fileSystem.File.Exists(configPath))
        {
            var content = _fileSystem.File.ReadAllText(configPath);
            try
            {
                config = JsonSerializer.Deserialize<Configuration>(content, GetOptions());
            }
            catch (JsonException ex)
            {
                Console.WriteLine("Failed deserializing config file");
                Console.WriteLine(ex);
            }

            if (config is null || config.Analyzers.Count == 0)
            {
                Console.WriteLine("Found config file but it's invalid.. exiting");
                return null;
            }

            foreach (var analyzer in Analyzers)
            {
                analyzer.AnalyzersListConfig = config.Analyzers[0];
            }
        }
        else
        {
            var emptyConfig = new AnalyzersListConfig();

            foreach (var analyzer in Analyzers)
            {
                analyzer.AnalyzersListConfig = emptyConfig;
            }

            config = new Configuration
            {
                Analyzers = [emptyConfig],
                CodeGuard = new CodeGuardConfig(),
                Severities = new SeverityConfig()
            };
        }

        int successCount = 0;
        int errorCount = 0;

        var excludedDirectories = config.Directories.Excluded;
        var globs = excludedDirectories.Select(d => Glob.Parse(NormalizePath(d))).ToList();

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if !DEBUG || HANDLE_EXCEPTIONS_IN_DEBUG
            try
            {
#endif
            var relative = NormalizePath(path)[(project.Path.Length+0)..];
            var file = _fileSystem.File.ReadAllText(path);

            if (globs.Any(g => g.IsMatch(relative)))
                continue;

            // Strip out potential 0xFEFF byte order mark
            if (file.StartsWith((char)0xfeff))
                file = file[1..];

            var tokens = Lexer.Lex(file);
            var ast = Parser.Parse(tokens);

            projectRef.ProjectFiles.Add(path, ast);
            projectRef.SemanticModel.ProcessFile(ast);

            successCount++;
#if !DEBUG || HANDLE_EXCEPTIONS_IN_DEBUG
            }
            catch
            {
                Console.WriteLine($"Analysis failed for file: {path}");
                errorCount++;
            }
#endif
        }

        Console.WriteLine($"Successfully parsed {successCount} files ({(successCount / (double)(errorCount + successCount)):P}) ({errorCount} errors)");

        projectRef.TypeLookup.GenerateTypeMappings(projectRef);
        projectRef.SemanticModel.ProcessFinished();

        foreach (var fileInfo in projectRef.ProjectFiles)
        {
            var path = fileInfo.Key;
            var projectFile = new ProjectFile(_fileSystem.Path.GetFileName(path), path, null);

            foreach (var analyzer in Analyzers)
            {
                if (!analyzer.GetConfig().Enabled)
                    continue;

                var fileIssues = new List<Issue>();
                analyzer.Analyze(project, fileInfo.Value, projectRef, fileIssues);
                fileIssues.ForEach(issue => issue.Location = CodeLocation.From(issue.Location));
                projectFile.Issues.AddRange(fileIssues);
            }

            if (projectFile.Issues.Count > 0)
            {
                projectFile.Content = _fileSystem.File.ReadAllText(path);
            }

            projectFiles.Add(projectFile);
        }

        var severityScore = CalculateSeverityScore(config!.Severities, projectFiles.SelectMany(f => f.Issues).ToList());
        bool success = !config.CodeGuard.FailOnReachSeverityScore
            || severityScore <= config.CodeGuard.MaxAllowedSeverityScore;

        return new Report(project, projectFiles, success, severityScore);
    }

    private string[] GetFilesInProject(Project project)
    {
        return _fileSystem.Directory.GetFiles(project.Path, "*.cs", SearchOption.AllDirectories);
    }

    private static uint GetScoreForSeverity(SeverityConfig config, AnalyzerSeverity severity)
    {
        return severity switch
        {
            AnalyzerSeverity.Suggestion => config.Suggestion,
            AnalyzerSeverity.Warning => config.Warning,
            AnalyzerSeverity.Important => config.Important,
            _ => 0
        };
    }

    private static long CalculateSeverityScore(SeverityConfig config, List<Issue> issues)
    {
        return issues.Sum(i => GetScoreForSeverity(config, i.AnalyzerSeverity));
    }
}
