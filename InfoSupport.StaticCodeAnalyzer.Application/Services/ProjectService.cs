using System;
using System.Collections.Generic;
using System.Linq;
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
    public async Task<Project> CreateProject(Project project)
    {
        project.Path = project.Path.Replace('\\', '/');

        if (project.Path.EndsWith('/'))
            project.Path = project.Path.TrimEnd('/');

        await context.Projects.AddAsync(project);
        await context.SaveChangesAsync();

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

    public async Task<Report?> StartAnalysis(Guid id)
    {
        var project = await context.Projects.FindAsync(id);

        if (project is null)
            return null;

        var report = Runner.RunAnalysis(project);

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
