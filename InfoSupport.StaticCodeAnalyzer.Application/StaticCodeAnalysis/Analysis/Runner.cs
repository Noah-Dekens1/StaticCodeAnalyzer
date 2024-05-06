#define HANDLE_EXCEPTIONS_IN_DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
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

    public static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public static Report? RunAnalysis(Project project, CancellationToken cancellationToken)
    {
        var paths = GetFilesInProject(project);

        var projectFiles = new List<ProjectFile>();
        var projectRef = new ProjectRef();

        var configPath = Path.Join(project.Path, "analyzer-config.json");

        Configuration? config = null;

        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(configPath))
        {
            var content = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<Configuration>(content, GetOptions());
            
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

            config = new Configuration()
            {
                Analyzers = [emptyConfig],
                CodeGuard = new CodeGuardConfig(),
                Severities = new SeverityConfig()
            };
        }

        int successCount = 0;
        int errorCount = 0;

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if !DEBUG || HANDLE_EXCEPTIONS_IN_DEBUG
            try
            {
#endif
                var file = File.ReadAllText(path);
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
            var projectFile = new ProjectFile(Path.GetFileName(path), path, null);

            foreach (var analyzer in Analyzers)
            {
                if (!analyzer.GetConfig().Enabled)
                    continue;

                var fileIssues = new List<Issue>();
                analyzer.Analyze(project, fileInfo.Value, projectRef, fileIssues);
                // Copy issue locations because of the OwnsOne relation
                fileIssues.ForEach(issue => issue.Location = CodeLocation.From(issue.Location));
                projectFile.Issues.AddRange(fileIssues);
            }

            // If any issues present store in projectFile so it can get saved to the db
            if (projectFile.Issues.Count > 0)
            {
                projectFile.Content = File.ReadAllText(path);
            }

            projectFiles.Add(projectFile);
        }

        var severityScore = CalculateSeverityScore(config!.Severities, projectFiles.SelectMany(f => f.Issues).ToList());
        bool success = !config.CodeGuard.FailOnReachSeverityScore
            || severityScore <= config.CodeGuard.MaxAllowedSeverityScore;

        return new Report(project, projectFiles, success, severityScore);
    }

    private static string[] GetFilesInProject(Project project)
    {
        return Directory.GetFiles(project.Path, "*.cs", SearchOption.AllDirectories);
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
