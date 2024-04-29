using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Application.Interfaces;
using InfoSupport.StaticCodeAnalyzer.Application.StaticCodeAnalysis.Analysis;
using InfoSupport.StaticCodeAnalyzer.Domain;
using InfoSupport.StaticCodeAnalyzer.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace InfoSupport.StaticCodeAnalyzer.Application.Services;

// Review: When using primary constructors there is no need to make a private field for the context.
// https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/primary-constructors
public class ProjectService(ApplicationDbContext context) : IProjectService
{
    public static void CreateConfigFileInternal(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream("InfoSupport.StaticCodeAnalyzer.Application.Resources.DefaultConfig.json");

            if (stream is null)
                throw new InvalidOperationException("Default config not found");

            using var fileStream = new FileStream(configFilePath, FileMode.Create, FileAccess.Write);

            stream.CopyTo(fileStream);
        }
    }

    public async Task<string?> CreateConfiguration(Guid id)
    {
        var project = await context.Projects.FindAsync(id);

        if (project is null)
            return null;

        var configFilePath = Path.Combine(project.Path, "analyzer-config.json");

        CreateConfigFileInternal(configFilePath);

        return configFilePath;
    }

    // Review: Try to always use CancellationToken when writing async methods.
    // Dotnet will give you access to the CancellationToken from the Minimal API and you can pass it to the method.
    public async Task<Project> CreateProject(Project project, CancellationToken cancellationToken)
    {
        project.Path = project.Path.Replace('\\', '/');

        if (project.Path.EndsWith('/'))
            project.Path = project.Path.TrimEnd('/');

        // Review: Instead of using AddRange you should use the non async Add variant
        // AddAsync is only relevant if for a very specific (e.g. Hi/Lo id generator) reason you need to know the id of the entity before it is saved.
        // When executing the .Add method, only the change tracker of EF will be updated, no database call will be made until the SaveChangesAsync method is called.
        // There it is important the method has a CancellationToken passed, as it will be used to cancel the (potentially long running) operation if needed.
        context.Projects.Add(project);
        await context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<Report?> CreateReport(Guid projectId, Report report)
    {
        var project = await context.Projects.FindAsync(projectId);

        if (project is null)
            return null;

        project.Reports.Add(report);

        await context.SaveChangesAsync();

        return report;
    }

    public async Task<Project?> DeleteProject(Guid id)
    {
        var project = await context.Projects.FindAsync(id);

        if (project is null)
            return null;

        context.Projects.Remove(project);
        await context.SaveChangesAsync();

        return project;
    }

    public async Task<List<Project>> GetAllProjects()
    {
        return await context.Projects.ToListAsync();
    }

    public async Task<Project?> GetProjectById(Guid id)
    {
        return await context.Projects
            .Where(p => p.Id == id)
            .Include(p => p.Reports)
            .FirstOrDefaultAsync();
    }

    public async Task OpenConfiguration(Guid id)
    {
        var project = await context.Projects.FindAsync(id) 
            ?? throw new ArgumentException("Project with id not found");

        var configFilePath = Path.Combine(project.Path, "analyzer-config.json");

        if (!File.Exists(configFilePath))
            throw new FileNotFoundException("No config file present");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(configFilePath)
        {
            UseShellExecute = true
        });
    }

    public async Task<Report?> StartAnalysis(Guid id)
    {
        var project = await context.Projects.FindAsync(id);

        if (project is null)
            return null;

        var report = Runner.RunAnalysis(project);
        if (report is null) return null;

        project.Reports.Add(report);

        await context.SaveChangesAsync();

        return report;
    }

    public async Task<Project?> UpdateProject(Guid id, Project project)
    {
        if (project.Id != id)
            return null;

        project.Id = id;

        context.Update(project);
        await context.SaveChangesAsync();

        return project;
    } 
}
