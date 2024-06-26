﻿using System;
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

public class ProjectService(ApplicationDbContext context) : IProjectService
{
    private readonly ApplicationDbContext _context = context;

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

    public async Task<string?> CreateConfiguration(Guid id, CancellationToken cancellationToken)
    {
        var project = await _context.Projects.FindAsync([id], cancellationToken);

        if (project is null)
            return null;

        var configFilePath = Path.Combine(project.Path, "analyzer-config.json");

        CreateConfigFileInternal(configFilePath);

        return configFilePath;
    }

    public async Task<Project> CreateProject(Project project, CancellationToken cancellationToken)
    {
        project.Path = project.Path.Replace('\\', '/');

        if (project.Path.EndsWith('/'))
            project.Path = project.Path.TrimEnd('/');

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<Report?> CreateReport(Guid projectId, Report report, CancellationToken cancellationToken)
    {
        var project = await _context.Projects.FindAsync([projectId], cancellationToken);

        if (project is null)
            return null;

        project.Reports.Add(report);

        await _context.SaveChangesAsync(cancellationToken);

        return report;
    }

    public async Task<Project?> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await _context.Projects.FindAsync([id], cancellationToken);

        if (project is null)
            return null;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<List<Project>> GetAllProjects(CancellationToken cancellationToken)
    {
        return await _context.Projects.ToListAsync(cancellationToken);
    }

    public async Task<Project?> GetProjectById(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Projects
            .Where(p => p.Id == id)
            .Include(p => p.Reports)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task OpenConfiguration(Guid id, CancellationToken cancellationToken)
    {
        var project = await _context.Projects.FindAsync([id], cancellationToken) 
            ?? throw new ArgumentException("Project with id not found");

        var configFilePath = Path.Combine(project.Path, "analyzer-config.json");

        if (!File.Exists(configFilePath))
            throw new FileNotFoundException("No config file present");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(configFilePath)
        {
            UseShellExecute = true
        });
    }

    public async Task<Report?> StartAnalysis(Guid id, CancellationToken cancellationToken)
    {
        var project = await _context.Projects.FindAsync([id], cancellationToken: cancellationToken);

        if (project is null)
            return null;

        var runner = new Runner();

        var report = runner.RunAnalysis(project, cancellationToken);
        if (report is null) return null;

        project.Reports.Add(report);

        await _context.SaveChangesAsync(cancellationToken);

        return report;
    }

    public async Task<Project?> UpdateProject(Guid id, Project project, CancellationToken cancellationToken)
    {
        if (project.Id != id)
            return null;

        project.Id = id;

        _context.Update(project);
        await _context.SaveChangesAsync(cancellationToken);

        return project;
    } 
}
