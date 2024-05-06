using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.Services;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.CLI.Utils;
using InfoSupport.StaticCodeAnalyzer.Domain;
using InfoSupport.StaticCodeAnalyzer.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace InfoSupport.StaticCodeAnalyzer.CLI.Commands;
internal class AnalyzeCommand : ICommandHandler
{
    public async Task Run(ArgsUtil args)
    {
        var directory = args.GetDirectory();

        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"Directory {directory} not found");
            return;
        }

        if (args.HasOption("--output-console"))
        {
            RunAnalysisConsole(directory);
            return;
        }

        await RunAnalysis(directory, CancellationToken.None);
    }

    private static async Task RunAnalysis(string directory, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Analyzer running in directory: {directory}");
        var name = GetDirectoryName(directory);

        // Can't use automated DI here so just manually create the DbContext
        // 'Migrate' will create the database
        using var dbContext = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        dbContext.Database.Migrate();
        var projectService = new ProjectService(dbContext);

        var projects = await projectService.GetAllProjects(cancellationToken);

        var project = projects.Find(x => x.Path == directory);

        if (project is null)
        {
            Console.Write($"What name do you want this new project to have: ({name}) ");
            var newName = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(newName))
            {
                name = newName;
            }

            project = new Project(name, directory);
            project = await projectService.CreateProject(project, cancellationToken);

            Console.WriteLine($"Successfully created new project: {project.Name}");
        }
        else
        {
            Console.WriteLine($"Starting analysis for existing project: {project.Name}");
        }

        await projectService.StartAnalysis(project.Id, cancellationToken);

        Console.WriteLine("Finished analysis");
        Console.WriteLine("Launching web application with results");

        await FrontendUtil.StartIfNotRunning();
    }

    private static void RunAnalysisConsole(string directory)
    {
        Console.WriteLine($"- Starting analysis on \"{directory}\" -");

        var report = Runner.RunAnalysis(new Project(GetDirectoryName(directory) ?? "Example", directory), CancellationToken.None);

        if (report is null)
            return;

        foreach (var projectFile in report.ProjectFiles)
        {
            var issues = projectFile.Issues;

            if (issues.Count > 0)
            {
                CodeDisplayCLI.DisplayCode(File.ReadAllText(projectFile.Path), issues, projectFile.Name);
            }
        }


        Console.WriteLine($"Finished! Found {report.ProjectFiles.Sum(f => f.Issues.Count)} issues in {report.ProjectFiles.Count} files");
        
        if (!report.IsSuccess)
        {
            Console.WriteLine("Code Guard check did not pass!");
            Console.WriteLine($"Severity score exceeded threshold: {report.SeverityScore}");

            Environment.Exit(1);
        }
    }

    private static string GetDirectoryName(string directory)
    {
        var lastIndex = directory.LastIndexOf('/');

        return lastIndex != -1 
            ? directory[(lastIndex+1)..] 
            : directory;
    }
}
