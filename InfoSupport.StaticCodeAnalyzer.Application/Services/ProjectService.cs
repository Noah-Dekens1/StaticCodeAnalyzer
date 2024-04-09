﻿using System;
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

public class ProjectService(ApplicationDbContext context) : IProjectService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Project> CreateProject(Project project)
    {
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        return project;
    }

    public async Task<Report?> CreateReport(Guid projectId, Report report)
    {
        var project = await _context.Projects.FindAsync(projectId);

        if (project is null)
            return null;

        project.Reports.Add(report);

        await _context.SaveChangesAsync();

        return report;
    }

    public async Task<Project?> DeleteProject(Guid id)
    {
        var project = await _context.Projects.FindAsync(id);

        if (project is null)
            return null;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return project;
    }

    public async Task<List<Project>> GetAllProjects()
    {
        return await _context.Projects.ToListAsync();
    }

    public async Task<Project?> GetProjectById(Guid id)
    {
        return await _context.Projects
            .Where(p => p.Id == id)
            .Include(p => p.Reports)
            .FirstOrDefaultAsync();
    }

    public async Task<Report?> StartAnalysis(Guid id)
    {
        var project = await _context.Projects.FindAsync(id);

        if (project is null)
            return null;

        var report = Runner.RunAnalysis(project);

        project.Reports.Add(report);

        foreach (var entry in _context.ChangeTracker.Entries())
        {
            Console.WriteLine($"Entity: {entry.Entity.GetType().Name}, State: {entry.State}");
        }

        await _context.SaveChangesAsync();

        return report;
    }

    public async Task<Project?> UpdateProject(Guid id, Project project)
    {
        if (project.Id != id)
            return null;

        project.Id = id;

        _context.Update(project);
        await _context.SaveChangesAsync();

        return project;
    } 
}