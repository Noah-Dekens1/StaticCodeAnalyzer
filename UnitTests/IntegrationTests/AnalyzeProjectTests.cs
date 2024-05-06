using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

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

    [TestMethod]
    public async Task IsOnline_API_ReturnsTrue()
    {
        var response = await s_httpClient.GetAsync("http://localhost:5000/api/online");
        Assert.IsTrue(response.IsSuccessStatusCode);

        var isOnline = await response.Content.ReadFromJsonAsync<bool>();
        Assert.IsTrue(isOnline);
    }

    [TestMethod]
    public async Task Analyze_CurrentProject_ReturnsValidReport()
    {
        //var response = await s_httpClient.GetAsync("http://localhost:5000");
    }
}
