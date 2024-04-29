using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

namespace InfoSupport.StaticCodeAnalyzer.Application.Interfaces;

public interface IProjectService
{
    public Task<Project> CreateProject(Project project, CancellationToken cancellationToken);
    public Task<List<Project>> GetAllProjects();
    public Task<Project?> GetProjectById(Guid id);
    public Task<Project?> DeleteProject(Guid id);
    public Task<Project?> UpdateProject(Guid id, Project project);

    public Task<Report?> CreateReport(Guid projectId, Report report);

    public Task<Report?> StartAnalysis(Guid id);
}
