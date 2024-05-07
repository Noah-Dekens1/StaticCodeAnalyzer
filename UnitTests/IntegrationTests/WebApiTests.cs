using System.Net.Http.Json;

using InfoSupport.StaticCodeAnalyzer.Domain;
using InfoSupport.StaticCodeAnalyzer.Infrastructure.Data;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTests;

[TestClass]
public class WebApiTests
{
    private static WebApplicationFactory<Program> s_factory = default!;
    private static HttpClient s_httpClient = default!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        // Initialize the WebApplicationFactory and HttpClient
        s_factory = new WebApplicationFactory<Program>();
        s_httpClient = s_factory.CreateClient();
    }

    private static string s_cachedAnalyzerDir = string.Empty;
    private static string GetAnalyzerDirectory()
    {
        if (!string.IsNullOrEmpty(s_cachedAnalyzerDir))
            return s_cachedAnalyzerDir;

        var executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var directory = Path.GetDirectoryName(executable)!;

        for (int i = 0; i < 4; i++)
        {
            directory = Directory.GetParent(directory)!.FullName;
        }

        s_cachedAnalyzerDir = directory;
        return s_cachedAnalyzerDir;
    }

    private static async Task<Project> CreateProject()
    {
        var project = new Project("@SelfAnalysis.test", GetAnalyzerDirectory());
        var response = await s_httpClient.PostAsJsonAsync("/api/project", project);
        var newProject = await response.Content.ReadFromJsonAsync<Project>();

        Assert.IsNotNull(newProject);

        return newProject;
    }

    private static async Task DeleteProject(Guid id)
    {
        await s_httpClient.DeleteAsync($"/api/project/{id}");
    }

    [TestMethod]
    public async Task WebAPI_IsOnline_ReturnsTrue()
    {
        var response = await s_httpClient.GetAsync("/api/online");
        Assert.IsTrue(response.IsSuccessStatusCode);

        var isOnline = await response.Content.ReadFromJsonAsync<bool>();
        Assert.IsTrue(isOnline);
    }

    [TestMethod]
    public async Task WebAPI_CreateProject_ReturnsNewProject()
    {
        var project = new Project("@SelfAnalysis.test", "C:/Test");
        var response = await s_httpClient.PostAsJsonAsync("/api/project", project);
        var newProject = await response.Content.ReadFromJsonAsync<Project>();


        Assert.IsNotNull(response);
        Assert.IsNotNull(newProject);
        Assert.AreEqual(newProject.Name, project.Name);
        Assert.AreEqual(newProject.Path, project.Path);

        await DeleteProject(newProject.Id);
    }

    [TestMethod]
    public async Task WebAPI_AnalyzeProject_ReturnsValidAnalysis()
    {
        var project = await CreateProject();

        var response = await s_httpClient.PostAsync($"/api/project/{project.Id}/analyze", null);
        var report = await response.Content.ReadFromJsonAsync<Report>();

        Assert.IsNotNull(report);
        Assert.IsTrue(report.ProjectFiles.Count > 0);
        Assert.IsTrue(report.ProjectFiles.Sum(f => f.Issues.Count) > 0);

        // We could split this up in different tests, but for convenience (that is - not having to 
        // cache this long-running operation) we'll just validate everything here

        var detailsNow = await s_httpClient.GetFromJsonAsync<Project>($"/api/project/{project.Id}");

        Assert.IsNotNull(detailsNow);
        Assert.AreEqual(detailsNow.Reports.Count, 1);

        await s_httpClient.DeleteAsync($"/api/project/{project.Id}/report/{report.Id}");

        var detailsAfter = await s_httpClient.GetFromJsonAsync<Project>($"/api/project/{project.Id}");

        Assert.IsNotNull(detailsAfter);
        Assert.AreEqual(detailsAfter.Reports.Count, 0);

        await DeleteProject(project.Id);
    }

    [TestMethod]
    public async Task WebAPI_GetProjects_ReturnsProjects()
    {
        // We're doing this on the actual SQLite db so we can't guarantee it'll be empty
        // So we can only check that it returns a newly created project

        var project = await CreateProject();

        var projects = await s_httpClient.GetFromJsonAsync<List<Project>>("/api/projects");

        Assert.IsNotNull(projects);
        Assert.IsTrue(projects.SingleOrDefault(p => p.Id == project.Id) is not null);

        await DeleteProject(project.Id);
    }

    [TestMethod]
    public async Task WebAPI_DeleteAndGetProjects_ReturnsProjects()
    {
        var project = await CreateProject();

        var projects = await s_httpClient.GetFromJsonAsync<List<Project>>("/api/projects");

        Assert.IsNotNull(projects);
        Assert.IsNotNull(projects.SingleOrDefault(p => p.Id == project.Id));

        await DeleteProject(project.Id);

        projects = await s_httpClient.GetFromJsonAsync<List<Project>>("/api/projects");

        Assert.IsNotNull(projects);
        Assert.IsNull(projects.SingleOrDefault(p => p.Id == project.Id));
    }

    [TestMethod]
    public async Task WebAPI_GetProjectDetails_ReturnsValidProject()
    {
        var project = await CreateProject();

        var details = await s_httpClient.GetFromJsonAsync<Project>($"/api/project/{project.Id}");

        Assert.IsNotNull(details);

        Assert.AreEqual(details.Id, project.Id);
        Assert.AreEqual(details.Path, project.Path);
        Assert.AreEqual(details.Reports.Count, 0); // new project so no reports yet

        await DeleteProject(project.Id);
    }
}
