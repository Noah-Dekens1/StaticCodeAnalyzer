using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

using Microsoft.EntityFrameworkCore;

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<ProjectFile> ProjectFiles => Set<ProjectFile>();
    public DbSet<Report> Reports => Set<Report>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("Data Source=StaticCodeAnalyzer.db");
}
