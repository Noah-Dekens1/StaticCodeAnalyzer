using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.Domain;

var directory = args.FirstOrDefault() ?? Directory.GetCurrentDirectory();

Console.WriteLine($"- Starting analysis on \"{directory}\" -");

var report = Runner.RunAnalysis(new Project("Example", directory));

foreach (var projectFile in report.ProjectFiles)
{
    var issues = projectFile.Issues;

    if (issues.Count > 0)
    {
        CodeDisplayCLI.DisplayCode(File.ReadAllText(projectFile.Path), issues, projectFile.Name);
    }
}

Console.WriteLine($"Finished! Found {report.ProjectFiles.Sum(f => f.Issues.Count)} issues in {report.ProjectFiles.Count} files");