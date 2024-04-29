using InfoSupport.StaticCodeAnalyzer.Application.Interfaces;
using InfoSupport.StaticCodeAnalyzer.Application.Services;
using InfoSupport.StaticCodeAnalyzer.Domain;
using InfoSupport.StaticCodeAnalyzer.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace InfoSupport.StaticCodeAnalyzer.WebAPI;

public class WebApiBuilder
{
    public static WebApplication Build(string[] args, bool overrideContentPath = false)
    {
        var root = AppContext.BaseDirectory;

        Console.WriteLine($"[DEBUG]: Content root: {root}");

        var builder = !overrideContentPath
            ? WebApplication.CreateBuilder(new WebApplicationOptions())
            : WebApplication.CreateBuilder(new WebApplicationOptions 
                { 
                    ContentRootPath = Path.Combine(root, "web\\"),
                    WebRootPath = Path.Combine(root, "web\\wwwroot\\")
                }
            );

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("LocalhostPolicy", policy =>
            {
                policy.WithOrigins("http://localhost:5163")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        builder.Services.AddDbContext<ApplicationDbContext>();
        builder.Services.AddScoped<IProjectService, ProjectService>();
        builder.Services.AddScoped<IReportService, ReportService>();


        // Add services to the container.

        var app = builder.Build();

        // Review: Try to avoid using the ! operator, it can lead to NullReferenceExceptions.
        // Instead, use GetRequiredService to throw an exception if the service is not found.
        // In this case you already have access to the IServiceProvider. There is no need to first call: GetService<IServiceScopeFactory>()
        using (var serviceScope = app.Services.CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();
        }

        app.UseCors("LocalhostPolicy");


        // Configure the HTTP request pipeline.

        app.MapGet("/api/projects", async (IProjectService projectService) =>
            await projectService.GetAllProjects());

        app.MapPost("/api/project", async (IProjectService projectService, Project project) =>
            await projectService.CreateProject(project));

        app.MapGet("/api/project/{id}", async (IProjectService projectService, Guid id) =>
            await projectService.GetProjectById(id));

        app.MapDelete("/api/project/{id}", async (IProjectService projectService, Guid id) =>
            await projectService.DeleteProject(id));

        app.MapPost("/api/project/{id}/analyze", async (IProjectService projectService, Guid id) =>
            await projectService.StartAnalysis(id));

        app.MapGet("/api/project/{projectId}/report/{reportId}", async (IReportService reportService, Guid projectId, Guid reportId) =>
            await reportService.GetReportById(reportId));
        
        // Suggestion for future projects: Use the Health Checks feature of dotnet core.
        // This can even be used to monitor your infrastructure dependencies (e.g. database)
        // And differentiate between readiness and liveness checks.
        // - Readiness indicates if the app is running normally but isn't ready to receive requests.
        // - Liveness indicates if an app has crashed and must be restarted.
        // https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0
        app.MapGet("/api/online", () => true);

        app.UseWebAssemblyDebugging();


        app.UseBlazorFrameworkFiles("/");

        /*
        var options = new StaticFileOptions();
        options.FileProvider = new PhysicalFileProvider(@"C:\Users\NoahD\source\repos\InfoSupport.StaticCodeAnalyzer\InfoSupport.StaticCodeAnalyzer.WebApp\bin\Release\net8.0\publish\wwwroot\");
        options.ServeUnknownFileTypes = true;
        app.UseStaticFiles(options);
        */
        app.UseStaticFiles();

        app.MapFallbackToFile("index.html");

        return app;

    }
}
