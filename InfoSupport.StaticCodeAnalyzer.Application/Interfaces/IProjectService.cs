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
    public Task<List<Project>> GetAllProjects(CancellationToken cancellationToken);
    public Task<Project?> GetProjectById(Guid id, CancellationToken cancellationToken);
    public Task<Project?> DeleteProject(Guid id, CancellationToken cancellationToken);
    public Task<Project?> UpdateProject(Guid id, Project project, CancellationToken cancellationToken);

    public Task<Report?> CreateReport(Guid projectId, Report report, CancellationToken cancellationToken);

    public Task<Report?> StartAnalysis(Guid id, CancellationToken cancellationToken);
    public Task<string?> CreateConfiguration(Guid id, CancellationToken cancellationToken);
    public Task OpenConfiguration(Guid id, CancellationToken cancellationToken);
}
