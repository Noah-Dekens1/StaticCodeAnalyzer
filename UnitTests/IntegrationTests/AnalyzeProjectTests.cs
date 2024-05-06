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
public class AnalyzeProjectTests
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

    private static string _cachedAnalyzerDir = string.Empty;
    private static string GetAnalyzerDirectory()
    {
        if (!string.IsNullOrEmpty(_cachedAnalyzerDir))
            return _cachedAnalyzerDir;

        var executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var directory = Path.GetDirectoryName(executable)!;

        for (int i = 0; i < 4; i++)
        {
            directory = Directory.GetParent(directory)!.FullName;
        }

        _cachedAnalyzerDir = directory;
        return _cachedAnalyzerDir;
    }

    private static async Task<Project> CreateProject()
    {
        var project = new Project("@SelfAnalysis.test", GetAnalyzerDirectory());
        var response = await s_httpClient.PostAsJsonAsync("http://localhost:5000/api/project", project);
        var newProject = await response.Content.ReadFromJsonAsync<Project>();

        Assert.IsNotNull(newProject);

        return newProject;
    }

    private static async Task DeleteProject(Guid id)
    {
        await s_httpClient.DeleteAsync($"http://localhost:5000/api/project/{id}");
    }

    [TestMethod]
    public async Task IsOnline_API_ReturnsTrue()
    {
        var response = await s_httpClient.GetAsync("http://localhost:5000/api/online");
        Assert.IsTrue(response.IsSuccessStatusCode);

        var isOnline = await response.Content.ReadFromJsonAsync<bool>();
        Assert.IsTrue(isOnline);
    }

    [TestMethod]
    public async Task WebAPI_CreateProject_ReturnsNewProject()
    {
        var project = new Project("@SelfAnalysis.test", "C:/Test");
        var response = await s_httpClient.PostAsJsonAsync("http://localhost:5000/api/project", project);
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

        var response = await s_httpClient.PostAsync($"http://localhost:5000/api/project/{project.Id}/analyze", null);
        var report = await response.Content.ReadFromJsonAsync<Report>();

        Assert.IsNotNull(report);
        Assert.IsTrue(report.ProjectFiles.Count > 0);
        Assert.IsTrue(report.ProjectFiles.Sum(f => f.Issues.Count) > 0);

        await DeleteProject(project.Id);
    }
}
