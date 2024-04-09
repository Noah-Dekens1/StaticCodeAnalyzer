

using InfoSupport.StaticCodeAnalyzer.Application.Interfaces;
using InfoSupport.StaticCodeAnalyzer.Application.Services;
using InfoSupport.StaticCodeAnalyzer.Domain;
using InfoSupport.StaticCodeAnalyzer.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

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


// Add services to the container.

var app = builder.Build();

app.UseCors("LocalhostPolicy");


// Configure the HTTP request pipeline.

app.MapGet("/projects", async (IProjectService projectService) => 
    await projectService.GetAllProjects());

app.MapPost("/project", async (IProjectService projectService, Project project) =>
    await projectService.CreateProject(project));

app.MapGet("/project/{id}", async (IProjectService projectService, Guid id) =>
    await projectService.GetProjectById(id));

app.MapPost("/project/{id}/analyze", async (IProjectService projectService, Guid id) =>
    await projectService.StartAnalysis(id));

app.Run();
